# ✅ Checklist de Verificação - Performance Analysis

Use este checklist para rastrear o progresso das correções de performance.

---

## FASE 1: REGEX COMPILATION (Crítica)

### Análise
- [ ] Leu `PERFORMANCE_ANALYSIS.md` seção "Achados Críticos"
- [ ] Entendeu o impacto (2-10x regressão)
- [ ] Identificou os 74 patterns no código

### Implementação - Parte A: Static Fields
- [ ] Abriu `src/Dialect/QueryTranslation/DefaultSqlTranslator.cs`
- [ ] Adicionou ~30 campos `private static readonly Regex` (compilados)
- [ ] Compilou sem erros: `dotnet build -c Release`
- [ ] Testou: `dotnet test --filter "Performance" -v`

### Implementação - Parte B: Atualizar InlineCompiledParameters
- [ ] Substituiu Regex.Replace por campos estáticos em InlineCompiledParameters (3 instâncias)
- [ ] Substituiu Regex.Match por campo estático em ExtractParameterOrder
- [ ] Compilou e testou: `dotnet test --filter "TranslationResult" -v`

### Implementação - Parte C: Refatorar AttemptTextualTranslation
- [ ] Substituiu ~35 Regex.Replace/Match por campos estáticos
- [ ] Verificou todas as linhas ~360-493
- [ ] Compilou e testou: `dotnet test --filter "Fallback" -v`

### Implementação - Parte D: Refatorar AttemptTextualTranslationV2
- [ ] Substituiu ~39 Regex.Replace/Match por campos estáticos
- [ ] Combinó 12 operações sequenciais em 3-4 passes quando possível
- [ ] Compilou e testou: `dotnet test --filter "V2" -v`

### Benchmarking Fase 1
- [ ] Criou teste benchmark antes da mudança
- [ ] Executou: `dotnet run -c Release -p Dialect.Benchmarks`
- [ ] Registrou baseline (ex: "25ms/query")
- [ ] Pós-mudança: Executou novamente
- [ ] Validou melhoria: **Meta 50-70% redução tempo regex**
- [ ] Relatou: "Antes=X ms, Depois=Y ms, Melhoria=Z%"

### Testes Fase 1
- [ ] Executou suite completa: `dotnet test -c Release`
- [ ] Nenhuma regressão funcional
- [ ] Coverage mantido ou melhorado

---

## FASE 2: STRING OPERATIONS

### 2.1 - Remover .ToLower()

**Arquivo: `Query/JoinAnalyzer.cs`**
- [ ] Substituiu `joinClause.ToLower().Trim()` no método `Analyze`
- [ ] Removeu a variável `lower` local
- [ ] Compilou: `dotnet build -c Release`
- [ ] Testou: `dotnet test --filter "JoinAnalyzer" -v`

**Arquivo: `Query/QueryParser.cs`**
- [ ] Substituiu `pred.ToLower().Contains()` por `.Contains(x, StringComparison.OrdinalIgnoreCase)`
- [ ] Verificou linha ~29 em `EstimateSelectivity`
- [ ] Testou: `dotnet test --filter "QueryParser" -v`

**Arquivo: `QueryTranslation/DefaultSqlProviderDetector.cs`**
- [ ] Adicionou `StringComparison.OrdinalIgnoreCase` aos 9 `.Contains()` checks
- [ ] Testou: `dotnet test --filter "ProviderDetector" -v`

### 2.2 - Otimizar DetermineJoinType

**Arquivo: `Query/JoinAnalyzer.cs`**
- [ ] Criou campo `private static readonly Regex _joinTypeRegex` (compilado)
- [ ] Refatorou `DetermineJoinType()` para usar regex único
- [ ] Removeu 6 `.Contains()` calls sequenciais
- [ ] Compilou e testou: `dotnet test --filter "DetermineJoinType" -v`

### Benchmarking Fase 2
- [ ] Executou benchmark antes: "X ms/query"
- [ ] Pós-mudança: Executou novamente
- [ ] Validou melhoria: **Meta 50% redução alocações string**
- [ ] Relatou: "String allocations reduzidas em Z%"

### Testes Fase 2
- [ ] Executou: `dotnet test -c Release`
- [ ] Nenhuma regressão

---

## FASE 3: COLLECTION ALLOCATION

### 3.1 - Dictionary Estático

**Arquivo: `QueryTranslation/DefaultSqlTranslator.cs`**
- [ ] Adicionou `private static readonly Dictionary<string, object?> _emptyParameters = new();`
- [ ] Procurou linha ~220 onde `new Dictionary<string, object?>()` era criado
- [ ] Substituiu por `_emptyParameters`
- [ ] Compilou: `dotnet build -c Release`
- [ ] Testou: `dotnet test --filter "Dictionary" -v`

### 3.2 - StringBuilder Capacity

**Arquivo: `QueryTranslation/DefaultSqlTranslator.cs`**
- [ ] Encontrou linha ~480 com `new StringBuilder()`
- [ ] Substituiu por `new StringBuilder(capacity: 2048)`
- [ ] Testou: `dotnet test --filter "StringBuilder" -v`

### 3.3 - Split Otimizado

**Arquivo: `Query/JoinAnalyzer.cs`**
- [ ] Verificou linha ~43: `clause.Split(new[] { ' ', '\t', '\n' }, ...)`
- [ ] Substituiu por: `clause.AsSpan().Split(' ', ...)`
- [ ] (Ou mantém se usar .Split com múltiplos chars - precisará de ReadOnlySpan approach)
- [ ] Compilou e testou: `dotnet test --filter "Split" -v`

### Benchmarking Fase 3
- [ ] Executou benchmark antes
- [ ] Pós-mudança: Executou novamente
- [ ] Mediu alocações com `/collect=Gen0,Gen1,Gen2`
- [ ] Validou: **Meta 40% redução alocações Gen0**

### Testes Fase 3
- [ ] Executou: `dotnet test -c Release`
- [ ] Nenhuma regressão

---

## FASE 4: LINQ BOUNDS

### 4.1 - Adicionar .Take()

**Arquivo: `Optimization/RecommendationPrioritizer.cs`**
- [ ] Procurou por `.OrderBy(...).ToList()` sem `.Take()`
- [ ] Adicionou `.Take(10)` antes de `.ToList()`
- [ ] Procurou por `.Where(...).ToList()` sem limites
- [ ] Adicionou `.Take(n)` onde apropriado
- [ ] Compilou e testou: `dotnet test --filter "Prioritizer" -v`

### Benchmarking Fase 4
- [ ] Executou benchmark com dataset grande (10000+ items)
- [ ] Validou que enumeration para aqui em `.Take(10)`

### Testes Fase 4
- [ ] Executou: `dotnet test -c Release`
- [ ] Nenhuma regressão

---

## FASE 5: STRUCTURAL & FINAL VALIDATION

### 5.1 - Sealed Classes (Opcional)

- [ ] Revisou `Query/QueryParser.cs` - Entendeu se é ponto de extensão
- [ ] Revisou `Performance/ExecutionPlanAnalyzer.cs`
- [ ] Revisou `Optimization/OptimizationEngine.cs`
- [ ] Decidiu: Manter `abstract` ou mudar para `sealed`
- [ ] (Se mudou) Testou: `dotnet test -c Release`

### 5.2 - Benchmark Final

- [ ] Executou benchmark final de ponta-a-ponta:
  ```bash
  dotnet run -c Release -p Dialect.Benchmarks -- --job short --runtimes net8.0,net9.0,net10.0
  ```
- [ ] Capturou métricas:
  - [ ] Latência P50 (ms)
  - [ ] Latência P99 (ms)
  - [ ] Throughput (queries/segundo)
  - [ ] Allocations (MB/1000 queries)
  - [ ] GC collections (Gen0, Gen1, Gen2)

### 5.3 - Comparativo

| Métrica | Baseline | Final | Melhoria |
|---------|----------|-------|----------|
| Latência (ms) | _____ | _____ | _____ |
| Throughput (q/s) | _____ | _____ | _____ |
| Allocations (MB) | _____ | _____ | _____ |
| Gen0 (count) | _____ | _____ | _____ |

**Meta:** Atingir 30-50% melhoria overall

### 5.4 - Build Final

- [ ] Executou build limpo: `dotnet clean && dotnet build -c Release /p:TreatWarningsAsErrors=true`
- [ ] Sem erros
- [ ] Sem warnings

### 5.5 - Suite de Testes

- [ ] Executou: `dotnet test -c Release -v detailed`
- [ ] Todos os testes passam
- [ ] Coverage ≥ baseline

---

## DOCUMENTAÇÃO

- [ ] Atualizou comentários no código com explicação de otimizações
- [ ] Adicionou performance notes onde apropriado:
  ```csharp
  // Performance: Pre-compiled regex to avoid recompilation (Issue #XXXX)
  private static readonly Regex pattern = new(..., RegexOptions.Compiled);
  ```
- [ ] Criou PR com descrição clara das mudanças
- [ ] Incluiu antes/depois de benchmarks na PR

---

## SIGN-OFF

- [ ] **Desenvolvedor:** _____________ Data: _____
- [ ] **Reviewer (Benchmark):** _____________ Data: _____
- [ ] **QA (Testes):** _____________ Data: _____

---

## Notas Adicionais

```
Qualquer desvio do plano deve ser documentado aqui:

1. ____________________________________
2. ____________________________________
3. ____________________________________
```

---

## Troubleshooting Rápido

| Problema | Solução |
|----------|---------|
| "Cannot convert from 'Regex' to 'Regex'" | Verifique que field está `static readonly` |
| Teste falha com "Collection modified" | Verifique se Dictionary é realmente estático e compartilhado |
| Sem melhoria de performance | Use profiler (dotTrace) para confirmar path crítico |
| Regressão funcional | Execute `git diff` e valide lógica de conversão |

---

**Última atualização:** 8 de Setembro de 2026
