# 🛠️ Guia de Implementação - Otimizações de Performance

## Início Rápido

Este guia fornece instruções passo-a-passo para implementar as 5 fases de otimização de performance identificadas na análise.

---

## FASE 1: REGEX COMPILATION (CRÍTICA)

### Passo 1.1: Criar campos Regex estáticos

**Arquivo:** `src/Dialect/QueryTranslation/DefaultSqlTranslator.cs`

Adicione no início da classe (após campos privados existentes):

```csharp
public sealed class DefaultSqlTranslator : ISqlTranslator
{
    private readonly ISqlProviderDetector _providerDetector;
    private readonly IReadOnlyDictionary<SqlProvider, SqlParserAdapter> _parserAdapters;
    private readonly ISqlDialect? _defaultTargetDialect;

    // ===== NOVOS CAMPOS REGEX (COMPILADOS) =====
    
    // InlineCompiledParameters - Postgres
    private static readonly Regex _postgresParameterPattern = 
        new(@"\$(\d+)", RegexOptions.Compiled);
    
    // InlineCompiledParameters - SQL Server
    private static readonly Regex _sqlServerParameterPattern = 
        new(@"@p(\d+)\b", RegexOptions.Compiled);
    
    // InlineCompiledParameters - MySQL
    private static readonly Regex _mysqlParameterPattern = 
        new(@"\?", RegexOptions.Compiled);
    
    // Parameter extraction
    private static readonly Regex _parameterOrderPattern = 
        new(@"(\d+)$", RegexOptions.Compiled);
    
    // Identifier unquoting patterns
    private static readonly Regex _identifierQuotePattern = 
        new(@"\[([^\]]+)\]|`([^`]+)`|""([^""]+)""", RegexOptions.Compiled);
    
    // AttemptTextualTranslation patterns
    private static readonly Regex _selectTopPattern = 
        new(@"^\s*SELECT\s+TOP\s+(\d+)\s+(.*?)\s*;?$", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex _selectTopPattern2 = 
        new(@"^\s*SELECT\s+TOP\s+(\d+)\s+(.*)$", RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex _insertIntoPattern = 
        new(@"INSERT\s+INTO", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex _scopeIdentityPattern = 
        new(@"SCOPE_IDENTITY\(\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex _isnullPattern = 
        new(@"ISNULL\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex _updateFromPattern = 
        new(@"UPDATE\s+(\w+)\s*\n?\s*SET\s+(.*?)\s*\n?\s*FROM\s+(\S+)\s+(\w+)\s*\n?\s*JOIN\s+(\S+)\s+(\w+)\s+ON\s+(.*?)\s*\n?\s*WHERE\s+(.*);?$", 
            RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    // AttemptTextualTranslationV2 patterns (DDL)
    private static readonly Regex _createTablePattern = 
        new(@"^\s*(CREATE|ALTER)\s+TABLE\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex _unicodeStringPattern = 
        new(@"\bN'([^']*)'", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex _getdatePattern = 
        new(@"\bGETDATE\s*\(\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex _lenPattern = 
        new(@"\bLEN\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex _sqlServerParameterPattern2 = 
        new(@"@([A-Za-z0-9_]+)", RegexOptions.Compiled);
    
    private static readonly Regex _offsetFetchPattern = 
        new(@"OFFSET\s+(\d+)\s+ROWS\s+FETCH\s+NEXT\s+(\d+)\s+ROWS\s+ONLY", 
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex _outputInsertedPattern = 
        new(@"\bOUTPUT\s+INSERTED\.[a-zA-Z0-9_]+\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex _offsetRowsPattern = 
        new(@"OFFSET\s+(\d+)\s+ROWS", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex _bigintIdentityPattern = 
        new(@"\bBIGINT\s+IDENTITY\s*\(\s*1\s*,\s*1\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex _intIdentityPattern = 
        new(@"\bINT\s+IDENTITY\s*\(\s*1\s*,\s*1\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex _nvarcharMaxPattern = 
        new(@"\bNVARCHAR\s*\(\s*MAX\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex _varcharMaxPattern = 
        new(@"\bVARCHAR\s*\(\s*MAX\s*\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex _nvarcharPattern = 
        new(@"\bNVARCHAR\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex _datetime2Pattern = 
        new(@"\bDATETIME2?\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    private static readonly Regex _bitPattern = 
        new(@"\bBIT\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    
    // === FIM DE CAMPOS REGEX ===
```

### Passo 1.2: Atualizar método InlineCompiledParameters

Encontre a linha ~270 e substitua a implementação:

```csharp
private static CompiledQuery InlineCompiledParameters(CompiledQuery compiledQuery, ISqlDialect targetDialect)
{
    if (compiledQuery.Parameters.Count == 0)
        return compiledQuery;

    var sql = compiledQuery.Sql;

    if (IsPostgreSql(targetDialect))
    {
        sql = _postgresParameterPattern.Replace(sql, m =>
        {
            var index = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            var key = $"p{index}";
            if (!compiledQuery.Parameters.TryGetValue(key, out var value))
                return m.Value;
            return ToSqlLiteral(value, targetDialect);
        });
    }
    else if (IsSqlServer(targetDialect))
    {
        sql = _sqlServerParameterPattern.Replace(sql, m =>
        {
            var idx = m.Groups[1].Value;
            if (compiledQuery.Parameters.TryGetValue($"p{idx}", out var value) ||
                compiledQuery.Parameters.TryGetValue($"@p{idx}", out value))
            {
                return ToSqlLiteral(value, targetDialect);
            }
            return m.Value;
        });
    }
    else if (IsMySql(targetDialect))
    {
        var ordered = compiledQuery.Parameters
            .OrderBy(kvp => ExtractParameterOrder(kvp.Key))
            .Select(kvp => kvp.Value)
            .ToList();

        var valueIndex = 0;
        sql = _mysqlParameterPattern.Replace(sql, _ =>
        {
            if (valueIndex >= ordered.Count)
                return "?";

            return ToSqlLiteral(ordered[valueIndex++], targetDialect);
        });
    }

    return new CompiledQuery(sql, new Dictionary<string, object?>());
}

private static int ExtractParameterOrder(string key)
{
    var match = _parameterOrderPattern.Match(key ?? string.Empty);
    if (!match.Success)
        return int.MaxValue;
    return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
}
```

### Passo 1.3: Refatorar AttemptTextualTranslation

Substitua os Regex.Match/Replace diretos por campos estáticos. Exemplo para linha ~395:

```csharp
private static string? AttemptTextualTranslation(string sql, SqlProvider sourceProvider, ISqlDialect targetDialect)
{
    // ... início do método ...
    
    // ANTES: var mTop = Regex.Match(s, @"^\s*SELECT\s+TOP\s+(\d+)\s+(.*?)\s*;?$", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    // DEPOIS:
    var mTop = _selectTopPattern.Match(s);
    
    // ... resto do método ...
}
```

### Passo 1.4: Refatorar AttemptTextualTranslationV2

Similar ao passo anterior, substitua todas as chamadas de Regex.Replace/Match por campos estáticos.

### Validação Fase 1

```bash
cd c:\Projetos\Daniel-iel\Dialect
dotnet build -c Release
dotnet test -c Release --filter "Category=Regex" -v detailed
```

Esperado: Sem erros de compilação, testes passando.

---

## FASE 2: STRING OPERATIONS

### Passo 2.1: Otimizar JoinAnalyzer.cs

**Arquivo:** `src/Dialect/Query/JoinAnalyzer.cs`

Substitua o método `DetermineJoinType` (linha ~31):

```csharp
private static readonly Regex _joinTypeRegex = 
    new(@"(?<type>INNER|LEFT|RIGHT|FULL|CROSS)\s+(?:OUTER\s+)?JOIN", 
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

private static string DetermineJoinType(string clause)
{
    var match = _joinTypeRegex.Match(clause);
    if (match.Success)
    {
        return $"{match.Groups["type"].Value.ToUpper()}_JOIN";
    }
    return "UNKNOWN_JOIN";
}
```

Substitua o método `Analyze` (linha ~11):

```csharp
public JoinAnalysis Analyze(string joinClause)
{
    // ✅ SEM .ToLower() - usar StringComparison
    var joinType = DetermineJoinType(joinClause);
    var (leftTable, rightTable) = ExtractTableNames(joinClause);
    var (joinColumn, joinCondition) = ExtractJoinCondition(joinClause);
    var isOptimal = IsOptimalJoin(joinType, joinCondition);

    return new JoinAnalysis(
        JoinClause: joinClause,
        JoinType: joinType,
        LeftTable: leftTable,
        RightTable: rightTable,
        JoinColumn: joinColumn,
        JoinCondition: joinCondition,
        IsOptimal: isOptimal,
        Recommendation: GenerateRecommendation(joinType, isOptimal)
    );
}

private static (string leftTable, string rightTable) ExtractTableNames(string clause)
{
    // Usar Span para evitar alocação de char[]
    var words = clause.AsSpan().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    // ... resto do método ...
}
```

### Passo 2.2: Corrigir QueryParser.cs

**Arquivo:** `src/Dialect/Query/QueryParser.cs` (linha ~29)

```csharp
protected decimal EstimateSelectivity(string[] predicates)
{
    if (predicates.Length == 0) return 1.0m;

    decimal selectivity = 1.0m;

    foreach (var pred in predicates)
    {
        // ✅ SEM .ToLower() - usar StringComparison
        if (pred.Contains("=", StringComparison.OrdinalIgnoreCase) && 
            !pred.Contains("<", StringComparison.OrdinalIgnoreCase) && 
            !pred.Contains(">", StringComparison.OrdinalIgnoreCase))
            selectivity *= 0.1m;
        else if (pred.Contains("between", StringComparison.OrdinalIgnoreCase))
            selectivity *= 0.2m;
        else if (pred.Contains("in", StringComparison.OrdinalIgnoreCase))
            selectivity *= 0.3m;
        else
            selectivity *= 0.5m;
    }

    return Math.Max(selectivity, 0.001m);
}
```

### Passo 2.3: Otimizar DefaultSqlProviderDetector.cs

**Arquivo:** `src/Dialect/QueryTranslation/DefaultSqlProviderDetector.cs`

Adicione StringComparison a todos os .Contains() (exemplo linha ~inicio):

```csharp
private SqlProvider? DetectFromConnectionString(string connStr)
{
    // Exemplo: antes de "if (connStr.Contains("port=3306")"
    
    if (connStr.Contains("port=3306", StringComparison.OrdinalIgnoreCase) ||
        connStr.Contains("mysql", StringComparison.OrdinalIgnoreCase))
        return SqlProvider.MySql;

    if (connStr.Contains("host=", StringComparison.OrdinalIgnoreCase) ||
        connStr.Contains("postgres", StringComparison.OrdinalIgnoreCase) ||
        connStr.Contains(".rds.amazonaws.com", StringComparison.OrdinalIgnoreCase))
        return SqlProvider.PostgreSql;

    if (connStr.Contains("server=", StringComparison.OrdinalIgnoreCase) ||
        connStr.Contains("data source=", StringComparison.OrdinalIgnoreCase) ||
        connStr.Contains(".database.windows.net", StringComparison.OrdinalIgnoreCase) ||
        connStr.Contains("sqlserver", StringComparison.OrdinalIgnoreCase))
        return SqlProvider.SqlServer;

    return null;
}
```

### Validação Fase 2

```bash
dotnet test -c Release --filter "Category=String" -v detailed
```

---

## FASE 3: COLLECTION ALLOCATION

### Passo 3.1: Eliminar Dictionary allocation

**Arquivo:** `src/Dialect/QueryTranslation/DefaultSqlTranslator.cs`

No início da classe, adicione:

```csharp
private static readonly Dictionary<string, object?> _emptyParameters = 
    new Dictionary<string, object?>();
```

Procure pela linha ~220 onde se cria novo Dictionary:

```csharp
// ❌ ANTES:
return new TranslationResult
{
    Compiled = new CompiledQuery(fallback, new Dictionary<string, object?>()),
    // ...
};

// ✅ DEPOIS:
return new TranslationResult
{
    Compiled = new CompiledQuery(fallback, _emptyParameters),
    // ...
};
```

### Passo 3.2: Otimizar StringBuilder

**Arquivo:** `src/Dialect/QueryTranslation/DefaultSqlTranslator.cs` (linha ~480)

```csharp
// ❌ ANTES:
var sb = new System.Text.StringBuilder();

// ✅ DEPOIS:
var sb = new System.Text.StringBuilder(capacity: 2048);
```

### Passo 3.3: Otimizar .Split() (se .NET 5+)

Qualquer linha com `.Split(char[])` pode ser substituída:

```csharp
// ❌ ANTES:
var parts = text.Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries);

// ✅ DEPOIS:
var parts = text.AsSpan().Split('|', StringSplitOptions.RemoveEmptyEntries);
// ou para múltiplos chars, usar ReadOnlySpan approach
```

### Validação Fase 3

```bash
dotnet test -c Release --filter "Category=Collections" -v detailed
```

Executar teste de alocações:
```bash
dotnet run -c Release -p Dialect.Tests -- --benchmark allocation
```

---

## FASE 4: LINQ BOUNDS

### Passo 4.1: Adicionar .Take() limits

**Arquivo:** `src/Dialect/Optimization/RecommendationPrioritizer.cs`

Encontre qualquer consulta LINQ que não tenha limite e adicione `.Take()`:

```csharp
// ❌ ANTES:
var topRecommendations = recommendations
    .OrderBy(x => x.Priority)
    .ToList();

// ✅ DEPOIS:
var topRecommendations = recommendations
    .OrderBy(x => x.Priority)
    .Take(10)
    .ToList();
```

Procure por padrões como:
- `.OrderBy(...).ToList()` → `.OrderBy(...).Take(N).ToList()`
- `.OrderByDescending(...).ToList()` → `.OrderByDescending(...).Take(N).ToList()`
- `.Where(...).ToList()` sem Take → adicionar Take se apropriado

### Validação Fase 4

```bash
dotnet test -c Release --filter "Category=Linq" -v detailed
```

---

## FASE 5: STRUCTURAL & FINAL VALIDATION

### Passo 5.1: Revisar sealed classes (Opcional)

**Arquivos:**
- `src/Dialect/Query/QueryParser.cs`
- `src/Dialect/Performance/ExecutionPlanAnalyzer.cs`
- `src/Dialect/Optimization/OptimizationEngine.cs`

Se essas classes não são ponto de extensão público, considere marcar como `sealed`:

```csharp
// ❌ ANTES:
public abstract class QueryParser
{
    // ...
}

// ✅ DEPOIS (se apropriado):
public sealed class QueryParserImpl : QueryParser
{
    // ...
}
```

### Passo 5.2: Benchmark final

Crie arquivo de teste de performance:

**Arquivo novo:** `src/Dialect.Tests/PerformanceTests.cs`

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace Dialect.Tests;

[MemoryDiagnoser]
public class TranslationBenchmarks
{
    private const string SampleSqlServer = "SELECT TOP 100 * FROM [Orders] WHERE [Status] = @status AND [Amount] > @amount";
    private const string SamplePostgres = "SELECT * FROM orders WHERE status = $1 AND amount > $2 LIMIT 100";
    private const string SampleMySql = "SELECT * FROM orders WHERE status = ? AND amount > ? LIMIT 100";
    
    private ISqlTranslator _translator = default!;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        services.AddSqlFramework().AddAllDialects();
        var sp = services.BuildServiceProvider();
        _translator = sp.GetRequiredService<ISqlTranslator>();
    }

    [Benchmark(Baseline = true)]
    public void TranslateSqlServer()
    {
        _translator.Translate(SampleSqlServer, SqlProvider.SqlServer, SqlProvider.PostgreSql);
    }

    [Benchmark]
    public void TranslatePostgres()
    {
        _translator.Translate(SamplePostgres, SqlProvider.PostgreSql, SqlProvider.MySql);
    }

    [Benchmark]
    public void TranslateMySql()
    {
        _translator.Translate(SampleMySql, SqlProvider.MySql, SqlProvider.SqlServer);
    }
}
```

Executar:
```bash
cd src/Dialect.Tests
dotnet run -c Release --benchmark
```

Esperado: 30-50% melhoria em throughput vs. baseline.

---

## Checklist de Conclusão

- [ ] Fase 1: Todos os Regex estáticos compilados
- [ ] Fase 2: Todos .ToLower() removidos, StringComparison aplicado
- [ ] Fase 3: Dictionary estático, StringBuilder com capacity, Split otimizado
- [ ] Fase 4: .Take() adicionado onde apropriado
- [ ] Fase 5: Benchmarks executados, validação de performance OK
- [ ] Testes unitários passando: `dotnet test -c Release`
- [ ] Build sem warnings: `dotnet build -c Release /p:TreatWarningsAsErrors=true`
- [ ] Documentação atualizada (se aplicável)
- [ ] PR criada com métricas de performance

---

## Troubleshooting

### "Object reference not set" após mudanças Regex

Certifique-se de inicializar campos estáticos na classe (não em método).

### Testes falhando após mudanças StringComparison

Valide que StringComparison.OrdinalIgnoreCase é o correto para seu caso de uso (case-insensitive sem locale-specific rules).

### Sem melhoria de performance observada

Verifique se o caminho crítico está sendo exercitado nos testes. Use profiler (dotTrace, PerfView) para confirmar que as mudanças estão ativas.

---

**Última atualização:** 8 de Setembro de 2026
