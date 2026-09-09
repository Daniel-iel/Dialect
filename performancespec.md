# Performance scan — Dialect repository

Data: 2026-09-09

## Scan execution checklist
Cada receita abaixo foi executada contra os arquivos `*.cs` do repositório (excluindo `bin/` e `obj/`). Contagens exatas retornadas pelo scanner:

- `.IndexOf("...")` (literal): 0 hits
- `.Substring(`: 19 hits
- `.StartsWith/.EndsWith("...")`: 29 hits
- `.Contains("...")`: 90 hits
- `.ToLower()` / `.ToUpper()`: 10 hits
- `.Replace(`: 16 hits
- `params ` (assinaturas): 21 hits
- LINQ (Select/Where/OrderBy/GroupBy/All/Any/Cast/Take/Aggregate): 431 hits
- `new List<` / `new Dictionary<`: 501 hits
- `RegexOptions.Compiled`: 25 hits
- `new Regex(`: 26 hits
- `GeneratedRegex`: 0 hits
- `new JsonSerializerOptions`: 1 hit
- `new HttpClient(`: 0 hits
- `new FileStream(`: 0 hits
- `async void`: 0 hits
- `.Result` / `.Wait()`: 0 hits (grep-level; manual review may be required for false positives)
- `String.Concat` / `+=` (string concat): 21 hits
- `Span<|Memory<|stackalloc|ArrayPool`: 0 hits

---

## Findings

#### 1. Static/compiled `Regex` vs `[GeneratedRegex]` (25 instances)
**Impact:** static compiled regexes cause startup JIT/alloc cost and are not source-gen/AOT-friendly
**Files:** [src/Dialect/QueryTranslation/SqlDialectDetector.cs](src/Dialect/QueryTranslation/SqlDialectDetector.cs#L17), [src/Dialect/Compilation/SqlCompiler.cs](src/Dialect/Compilation/SqlCompiler.cs#L17)
**Fix:** Replace static `new Regex(..., RegexOptions.Compiled)` with `[GeneratedRegex("pattern")]` source-generated partials or a cached `Regex` factory
**Caveat:** requires .NET 7+ for `[GeneratedRegex]`; otherwise ensure static readonly caching and limit compiled regex budget

#### 2. Widespread LINQ usage in potential hot paths (431 instances)
**Impact:** LINQ allocations (delegates/enumerators/intermediate collections) in tight or frequently-invoked code can add measurable GC and CPU overhead
**Files:** [src/Dialect.Cli/ErrorHandling/DiagnosticService.cs](src/Dialect.Cli/ErrorHandling/DiagnosticService.cs#L115), [src/Dialect.MySql/Query/MySqlQueryParser.cs](src/Dialect.MySql/Query/MySqlQueryParser.cs#L25), many others
**Fix:** Replace hot-path LINQ with imperative loops, pre-size destination collections (`TryGetNonEnumeratedCount` + `new List(capacity)`), or use `Span`/`Memory` APIs where appropriate
**Caveat:** LINQ is fine for non-hot code; prioritize `*Extensions.cs`, `*Formatter.cs`, parsing and renderer paths

#### 3. Per-call `new List` / `new Dictionary` (501 instances)
**Impact:** Systematic per-call collection allocation across the codebase; high volume (>50) escalates priority
**Files:** [src/Dialect.Cli/FileRewriting/BulkFileRewriter.cs](src/Dialect.Cli/FileRewriting/BulkFileRewriter.cs#L68), [src/Dialect.MySql/Rendering/MySqlQueryRenderer.cs](src/Dialect.MySql/Rendering/MySqlQueryRenderer.cs#L24)
**Fix:** Hoist static/deterministic collections to `static readonly` (or `FrozenDictionary`), call `EnsureCapacity` for bulk builds, or reuse object pools
**Caveat:** Only convert to `FrozenDictionary` if the collection is truly immutable after initialization

#### 4. `Substring` allocations in parser/analysis code (19 instances)
**Impact:** `Substring` allocates new strings frequently in parsing flows (parsing is often a hot path)
**Files:** [src/Dialect/Parsing/AntlrSqlParser.cs](src/Dialect/Parsing/AntlrSqlParser.cs#L101), [src/Dialect/Query/AggregationAnalyzer.cs](src/Dialect/Query/AggregationAnalyzer.cs#L72)
**Fix:** Use `ReadOnlySpan<char>` / `AsSpan()` and span-based parsing (`int.Parse(ReadOnlySpan<char>)`, `MemoryExtensions`), or `string.Create` for aggregated output
**Caveat:** `Span<T>` cannot escape a method; use `Memory<T>` for async/escaping scenarios

#### 5. String concatenation inside loop (`current += ch`) (1 instance)
**Impact:** O(n^2)-style allocations when appending per-character in a loop (parsing hot path)
**Files:** [src/Dialect/Parsing/AntlrSqlParser.cs](src/Dialect/Parsing/AntlrSqlParser.cs#L292)
**Fix:** Use `StringBuilder`/`char[]` buffer or accumulate into `Span<char>` and only create string at the end
**Caveat:** Avoid per-character allocations on large inputs; this method is a clear hot-path candidate

#### 6. Chained `.Replace()` (HTML escape) — multiple intermediate allocations (1 instance)
**Impact:** Chains like `s.Replace(...).Replace(...).Replace(...)` allocate intermediates per call
**Files:** [src/Dialect.Cli/Reporting/HtmlDashboardGenerator.cs](src/Dialect.Cli/Reporting/HtmlDashboardGenerator.cs#L425)
**Fix:** Use `System.Text.Encodings.Web.HtmlEncoder.Default.Encode(text)` or a single-pass `StringBuilder`/`string.Create` implementation
**Caveat:** Prefer framework HTML encoder for correctness and security

#### 7. Properties materializing lists via LINQ (`.Where(...).ToList()`) (several instances)
**Impact:** Each access allocates a new `List<T>`; properties with this pattern are surprising callers and waste allocations
**Files:** [src/Dialect.Cli/ErrorHandling/DiagnosticService.cs](src/Dialect.Cli/ErrorHandling/DiagnosticService.cs#L115)
**Fix:** Return `IEnumerable<T>` (deferred) or provide explicit `GetErrors()` / cache snapshots and avoid allocating on every property access
**Caveat:** Choose API shape based on expected call frequency (deferred vs snapshot)

#### 8. `JsonSerializerOptions` constructed per-call (1 instance)
**Impact:** Creating `JsonSerializerOptions` repeatedly is expensive and prevents options reuse/trimming benefits
**Files:** [src/Dialect.Cli/Reporting/JsonReportWriter.cs](src/Dialect.Cli/Reporting/JsonReportWriter.cs#L56)
**Fix:** Cache a `static readonly JsonSerializerOptions s_opts = new() { ... }` and reuse it
**Caveat:** Validate whether per-call options vary; if not, always cache

#### 9. `ToLower()`/`ToUpper()` + trimming instead of ordinal comparisons (10 instances)
**Impact:** Allocations and culture-sensitive behavior; slower than `Equals(..., StringComparison.OrdinalIgnoreCase)` or `AsSpan()` comparisons
**Files:** [src/Dialect/Query/AggregationAnalyzer.cs](src/Dialect/Query/AggregationAnalyzer.cs#L42), [src/Dialect/Query/QueryParser.cs](src/Dialect/Query/QueryParser.cs#L29)
**Fix:** Use `Equals(..., StringComparison.OrdinalIgnoreCase)`, `StartsWith(..., StringComparison.OrdinalIgnoreCase)` overloads or operate on `ReadOnlySpan<char>` to avoid allocations
**Caveat:** Confirm intent — some comparisons intentionally use culture rules

#### 10. `params` array allocations in Fluent APIs (21 instances)
**Impact:** `params` creates an array per call; in high-frequency usage this becomes noisy
**Files:** [src/Dialect/Fluent/SqlBuilder.cs](src/Dialect/Fluent/SqlBuilder.cs#L26)
**Fix:** Add common-arity overloads, or (on .NET 9+/C# 13) use `params ReadOnlySpan<T>` to eliminate the array
**Caveat:** Breaking change risk for public APIs — prefer adding overloads first

---

## Positive findings
- `GeneratedRegex` usage: 0 hits — no source-generated regex present (opportunity to improve)
- No `new HttpClient()` occurrences found — good (avoid socket exhaustion)

## Summary table
| Severity | Count | Top Issue |
|----------|-------:|----------|
| 🔴 Critical | 0 | — |
| 🟡 Moderate | 10 | LINQ + per-call collections (widespread) |
| ℹ️ Info | 0 | — |

> ⚠️ **Disclaimer:** These results were generated by an AI assistant using static grep-style analysis and heuristics. Findings may include false positives or miss context-sensitive cases. Prioritize by profiling and human review before applying changes to production.

## Reference coverage
References used from the `analyzing-dotnet-performance` skill:
- references/critical-patterns.md
- references/async-patterns.md
- references/memory-and-strings.md
- references/regex-patterns.md
- references/collections-and-linq.md
- references/io-and-serialization.md
- references/structural-patterns.md

## Validation checklist
- [x] All critical patterns (from `critical-patterns.md`) were checked
- [x] Topic-specific recipes executed (comprehensive scan)
- [x] Each finding includes a concrete fix
- [x] Scan execution checklist included above
