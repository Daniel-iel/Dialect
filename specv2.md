# Spec — Framework FluentBuilder SQL Multi-Dialeto (.NET)

## Status
Documento de alinhamento inicial. Escopo: **geração de SQL** (texto + parâmetros).
Execução/mapeamento de resultado fica fora do escopo (será feita via Dapper, em pacote separado, não tratado aqui).

---

## 1. Objetivo

Criar um framework .NET que produz `(SQL, Parâmetros)` corretos para diferentes bancos de dados de destino —
**SQL Server**, **PostgreSQL** e **MySQL** — a partir de uma única definição de query, escrita de forma
agnóstica de dialeto. O resultado é consumido pelo usuário final através do **Dapper** (execução fora do
escopo deste projeto, ver seção 2).

O framework oferece **duas opções para se chegar a esse resultado**, ambas convergindo para o mesmo formato
de saída (`CompiledQuery { Sql, Parameters }`, pronto para ser usado com Dapper):

1. **FluentBuilder** — montar a query programaticamente em C#, via API fluente.
2. **Tradução de query** — informar diretamente uma instrução SQL já escrita (ex: T-SQL) e obter o mesmo
   resultado compilado para o dialeto de destino, sem precisar reescrever a query no builder (seção 11).

Essa segunda opção existe para quem não quer trabalhar com o pattern builder. As duas opções não são
excludentes nem hierárquicas — são dois caminhos igualmente válidos até o mesmo ponto de chegada.

### Princípio central
> Nem o FluentBuilder nem a tradução de query geram SQL diretamente. Ambos preenchem a mesma árvore de
> objetos (AST) que representa a intenção da query. Só no momento de `Compile()` (builder) ou `Translate()`
> (tradução) um `Renderer` específico do dialeto de destino traduz essa árvore em `CompiledQuery`. A partir
> daí, o consumidor usa o resultado com Dapper da mesma forma, independentemente de qual caminho gerou a query.

---

## 2. Fora de escopo (explícito)

Para evitar ambiguidade de escopo no início do projeto, os itens abaixo **não** fazem parte deste framework:

- Execução de comandos contra o banco (conexão, transação, retry, pooling) — fica a cargo do consumidor via Dapper.
- Mapeamento de resultado para objetos (`Query<T>`) — responsabilidade do Dapper.
- ORM completo (change tracking, lazy loading, unit of work).
- Suporte a Oracle ou outros bancos além dos 3 definidos (arquitetura deve permitir adicionar depois, mas não é v1).
- Parser de SQL genérico para uso em produção fora dos dois usos já especificados: tradução SQL→SQL (seção 11) e conversão de projeto para FluentBuilder via CLI (seção 12).
- Geração de código C# **fora do fluxo da CLI de conversão de projeto** (seção 12) — ou seja, o método `Translate()` (seção 11) nunca gera código C#, apenas SQL. A geração de código C#/FluentBuilder existe exclusivamente como responsabilidade da CLI descrita na seção 12.

---

## 3. Extensibilidade — suporte a novos bancos no futuro

Ainda que o v1 tenha como alvo apenas **SQL Server, PostgreSQL e MySQL**, o projeto deve ser desenhado desde
o início para permitir a adição de **novos bancos de dados no futuro** (ex: Oracle, SQLite, MariaDB, CockroachDB)
sem exigir alterações no core. Essa é uma restrição de arquitetura, não uma feature — deve orientar todas as
decisões de design das seções seguintes.

### Regras de extensibilidade

- **O core nunca conhece bancos específicos.** Nenhuma classe em `MyFramework.Core` pode ter lógica condicional
  do tipo `if (provider == SqlProvider.Postgres)`. Toda diferença de comportamento é resolvida através de
  implementações de `ISqlDialect`/`IQueryRenderer`, nunca por `switch`/`if` espalhados pelo core.
- **Adicionar um banco novo = criar um pacote novo**, implementando os contratos já existentes
  (`ISqlDialect`, `IQueryRenderer`, `IRoutineRenderer` se aplicável), sem tocar em `MyFramework.Core`,
  `MyFramework.SqlServer`, `MyFramework.PostgreSql` ou `MyFramework.MySql`.
- **Contratos, não implementações concretas, são a superfície pública do core.** Tudo que varia entre bancos
  (quote de identificador, placeholder de parâmetro, paginação, funções, features suportadas) deve estar
  atrás de interface — nunca hardcoded como string fixa no core.
- **Feature negotiation (`ISqlDialect.Supports(SqlFeature)`) é o mecanismo de extensão, não enums fechados.**
  Um dialeto novo pode declarar que não suporta determinado recurso sem precisar alterar a definição de
  `SqlFeature` — o enum de features deve crescer de forma aditiva conforme necessário, sem quebrar dialetos
  já existentes.
- **`SqlProvider` (enum usado na configuração/DI) não deve ser a única forma de identificar um dialeto.**
  Um provider desconhecido pelo core (ex: um banco adicionado por um pacote de terceiros) precisa poder ser
  registrado e resolvido via DI sem exigir que o enum principal do core seja alterado — ver ajuste na seção 8.
- **Testes golden são parametrizados por dialeto, não por banco nomeado.** A suíte de testes deve rodar contra
  qualquer `ISqlDialect` registrado, incluindo dialetos de terceiros, sem exigir reescrita de testes.

### Pacote de extensão — modelo esperado

```
MyFramework.Oracle (exemplo hipotético, não faz parte do v1)
 ├── OracleDialect : ISqlDialect
 ├── OracleQueryRenderer : IQueryRenderer
 ├── OracleRoutineRenderer : IRoutineRenderer   (aqui entraria o conceito de "package", específico de Oracle)
 └── depende apenas de MyFramework.Core
```

Esse modelo é o motivo pelo qual o campo `Package` já foi reservado (nulo/ignorado) na seção 5.3 — a AST de
`RoutineCall` foi desenhada pensando num banco que ainda não existe no v1, para que sua eventual chegada não
exija remodelar a árvore.

### Impacto direto nas seções seguintes

Esta restrição de extensibilidade se conecta diretamente com decisões já descritas na spec:

- Seção 5.4 (particularidades por banco) — o catálogo de `SqlFunction` e o mecanismo de `DialectExtensions`
  já são desenhados como abertos, não como uma lista fechada dos 3 bancos atuais.
- Seção 7 (checklist de alinhamento) — toda decisão de política (ex: erro vs. emulação para feature ausente)
  deve ser resolvida na interface (`ISqlDialect`), nunca com `if` explícito por banco no core.
- Seção 15 (estrutura de projeto) — cada dialeto já nasce como pacote isolado, o que é o pré-requisito
  estrutural para que um dialeto novo seja apenas "mais um pacote", sem tocar nos existentes.

---

## 4. Arquitetura em camadas

```
SqlBuilder (API fluente)
       │
       ▼
  Query Model / AST   (SelectStatement, InsertStatement, UpdateStatement, DeleteStatement, RoutineCall...)
       │
       ▼
  ISqlDialect          (regras específicas + delega para renderers)
       │
       ├── SqlServerDialect
       ├── PostgreSqlDialect
       └── MySqlDialect
       │
       ▼
  CompiledQuery { Sql, Parameters }
```

Regras de design que sustentam essa camada:

- O **core nunca conhece nomes de bancos específicos** — só conceitos abstratos (`SqlFunction`, `SqlFeature`, `DialectExtensions`). Adicionar um 4º banco não deve exigir tocar no core.
- Toda a AST (`SelectStatement`, `InsertStatement`, etc.) é **imutável** após `.Build()` (uso de `ImmutableArray<T>`, `init` em propriedades).
- O `SqlBuilder` fluente é **thread-confined** — objeto de uso local/curta duração, nunca compartilhado entre threads antes do `.Build()`.
- `ISqlDialect` é **stateless** — registrado como singleton via DI.

---

## 5. Escopo funcional (features)

### 4.1 DML — Tier 1 (essencial para v1)

- `SELECT`: colunas, `DISTINCT`, `FROM` com alias, `WHERE` (árvore de condições AND/OR/comparações), `GROUP BY`, `HAVING`, `ORDER BY`.
- `JOIN`: `INNER`, `LEFT`, `RIGHT`, `FULL`, `CROSS` — API única (`Join(JoinType, ...)`) com métodos de conveniência (`LeftJoin`, `InnerJoin`...).
  - **Atenção**: `FULL JOIN` não existe nativamente em MySQL — decisão de política necessária (ver seção 6).
- Paginação: `Take()` / `Skip()` (nomenclatura agnóstica, nunca `Top()`/`Limit()` na API pública).
  - SQL Server: `TOP` (posicionado antes das colunas) ou `OFFSET/FETCH NEXT` (exige `ORDER BY`).
  - PostgreSQL/MySQL: `LIMIT/OFFSET` (final da query).
  - MySQL: caso especial de `OFFSET` sem `LIMIT` (exige `LIMIT` com valor alto).
- `INSERT`, `UPDATE`, `DELETE` fluentes.
- CTEs (`WITH`) e subqueries (reaproveitando `SelectStatement` como nó aninhado).
- `WhereRaw` / `SelectRaw` / `OrderByRaw` — escape hatch sempre parametrizado (nunca concatenação livre).

### 4.2 DML — Tier 2 (diferenciais)

- Window functions (`ROW_NUMBER() OVER (...)`, `RANK()`) — sintaxe quase idêntica nos 3 dialetos, custo-benefício alto.
- Suporte a JSON (`JSON_VALUE`, `->`/`->>`, `JSON_EXTRACT`) — sintaxe muito diferente por banco, maior esforço.
- `Upsert` como conceito de primeira classe na AST (não composto a partir de outras peças):
  - SQL Server → `MERGE`
  - PostgreSQL → `INSERT ... ON CONFLICT DO UPDATE`
  - MySQL → `INSERT ... ON DUPLICATE KEY UPDATE`
- Validação estática opcional schema-aware (detectar coluna/tabela inexistente em `Compile()`).
- `ToDebugString()` — preview do SQL com valores inline, apenas para debug/log (nunca para execução).
- Formatação configurável (compacta vs. pretty-print).
- Proteção contra `DELETE`/`UPDATE` sem `WHERE` (exigir `.AllowFullTableOperation()` explícito).

### 4.3 Procedures / Functions

- `RoutineCall` como raiz de AST paralela a `SelectStatement`.
- Diferenciar `Procedure` vs `Function` (`RoutineKind`).
- Parâmetros com direção (`In`/`Out`/`InOut`).
- **Não existe conceito de "package"** em SQL Server/PostgreSQL/MySQL (isso é exclusivo de Oracle). O campo `Package` deve ficar reservado/nulo nesses 3 dialetos, para não fechar a porta a um dialeto Oracle futuro.
- Diferença estrutural relevante: PostgreSQL trata funções que retornam conjunto de forma distinta (`SELECT * FROM fn(...)`) — não é um `CALL` como nos outros dois.
- **Nota de integração**: como a execução será via Dapper, boa parte da complexidade de OUT params e chamada de procedure (`CommandType.StoredProcedure`) é resolvida pelo Dapper — o framework precisa apenas produzir nome da rotina + `DynamicParameters` corretos, sem necessidade de montar `EXEC`/`CALL` como texto manualmente.

### 4.4 Particularidades por banco — estratégia de cobertura

| Tipo de particularidade | Exemplo | Mecanismo |
|---|---|---|
| Sintaxe equivalente, forma diferente | `TOP` vs `LIMIT`, quote de identificador | Renderer específico por dialeto |
| Função com nome diferente | `CONCAT()` vs `+` vs `\|\|`, `NOW()` vs `GETDATE()` | `ISqlDialect.RenderFunction(SqlFunction, args)` — catálogo de funções neutro |
| Recurso exclusivo de 1 banco | `RETURNING`, `DISTINCT ON` (Postgres) | Extension methods por dialeto + bag de `DialectExtensions` tipado na AST |
| Recurso ausente, sem equivalente direto | `FULL JOIN` no MySQL | `Compile()` lança exceção clara (padrão) **ou** emulação opt-in (avançado, v2) |
| Algo não previsto pelo builder | Qualquer SQL específico não modelado | `WhereRaw`/`SelectRaw` sempre parametrizado |
| "Isso existe nesse banco?" | Consulta de capacidade | `ISqlDialect.Supports(SqlFeature)` — feature negotiation, também usado para pular testes automaticamente |

Regra de política: erro de feature não suportada deve ocorrer em **`Compile()`**, nunca ser descoberto só em runtime no banco.

---

## 6. Modelo de dados (AST) — visão geral

```
SelectStatement
 ├── IsDistinct: bool
 ├── Columns: IReadOnlyList<Column>
 ├── From: TableReference
 ├── Joins: IReadOnlyList<JoinClause>
 ├── Where: WhereExpression?      (árvore AndNode/OrNode/ComparisonNode/InNode/RawNode)
 ├── GroupBy: IReadOnlyList<Column>
 ├── Having: WhereExpression?
 ├── OrderBy: IReadOnlyList<OrderByClause>
 ├── RowLimit: RowLimit?          (Count, Offset, WithTies — unifica TOP/LIMIT)
 └── DialectExtensions: IReadOnlyDictionary<Type, object>   (bag para recursos exclusivos de 1 dialeto)

InsertStatement / UpdateStatement / DeleteStatement — seguem o mesmo princípio de imutabilidade.

RoutineCall
 ├── Schema: string?
 ├── Package: string?   (reservado — não usado pelos 3 dialetos alvo)
 ├── Name: string
 ├── Kind: RoutineKind (Procedure | Function)
 └── Parameters: IReadOnlyList<RoutineParameter> (Name, Value, Direction, Type)
```

`WhereExpression` é reaproveitada em `WHERE`, `HAVING` e `ON` de `JOIN` — é a mesma árvore de condições, evitando duplicação de lógica de renderização.

---

## 7. Decisões de design a fechar antes de iniciar (checklist de alinhamento)

Estas são decisões que mudam a arquitetura e devem ser resolvidas **antes** de começar a implementação:

- [ ] **Parametrização obrigatória** de todo valor — inegociável, base da proteção contra SQL Injection.
- [ ] **Política para `FULL JOIN` no MySQL**: lançar exceção clara (recomendado para v1) vs. emulação automática via `UNION` (v2, opt-in).
- [ ] **Política para feature não suportada em geral**: sempre `Compile()`-time exception, nunca fallback silencioso.
- [ ] **Nomenclatura da API de paginação**: `Take()`/`Skip()` (estilo LINQ) em vez de `Top()`/`Limit()`, para manter a API neutra.
- [ ] **`OFFSET` sem `ORDER BY`**: validar em `Compile()` e lançar erro amigável (SQL Server/ANSI exigem `ORDER BY` para paginação com offset).
- [ ] **`DELETE`/`UPDATE` sem `WHERE`**: bloquear por padrão, exigir `.AllowFullTableOperation()` explícito.
- [ ] **Versionamento de dialeto**: v1 assume uma única versão "razoável" por banco (ex: SQL Server 2019+, PostgreSQL 13+, MySQL 8+). Diferenças dentro da mesma engine (ex: `MERGE` só em Postgres 15+) ficam para v2, se necessário.
- [ ] **Public API surface do escape hatch**: `Raw()` sempre parametrizado; método `...Unsafe()` deliberadamente nomeado para uso fora do padrão, exigindo fricção consciente do dev.
- [ ] **Detecção de dialeto de origem na tradução (seção 11)**: lançar exceção quando a connection string for ambígua, nunca adivinhar silenciosamente — sempre oferecer overload explícito (`SqlProvider`) como alternativa.

---

## 8. Configuração / DI

- Registro via `IServiceCollection.AddSqlFramework(...)`, com **três overloads**:
  - Simples: `AddSqlFramework(SqlProvider provider)` — cobre os bancos nativos do core (SQL Server, PostgreSQL, MySQL).
  - Completo: `AddSqlFramework(Action<SqlFrameworkOptions> configure)` — permite adicionar opções futuras (ex: `CompiledQueryCacheMaxEntries`) sem quebrar API pública.
  - **Extensível**: `AddSqlFramework(ISqlDialect customDialect)` — permite registrar um dialeto que não pertence ao enum `SqlProvider` do core, viabilizando o uso de pacotes de terceiros (ex: `MyFramework.Oracle`) sem exigir alteração no core para reconhecer o novo banco.
- Sem uso de `appsettings.json`/`IOptions<T>` **para a escolha do dialeto de destino do `SqlBuilder`** — essa configuração é feita diretamente no `Program.cs`, com validação síncrona e fail-fast no momento do registro. (Essa regra não se aplica à connection string usada pela tradução — ver subseção abaixo, onde `appsettings.json` é a fonte natural, como em qualquer aplicação .NET.)
- `ISqlDialect` registrado como **singleton** (é stateless por definição de design).
- Cenário multi-provider (ex: multi-tenant com bancos diferentes) fica como extensão futura via `ISqlDialectFactory` — não é requisito de v1, mas a API não deve impedir essa evolução. A mesma fábrica deve conseguir resolver tanto dialetos nativos quanto dialetos de terceiros registrados via `ISqlDialect customDialect`.

### Registro da tradução (`AddSqlTranslation`)

Diferente do `AddSqlFramework` (que define o dialeto de destino do `SqlBuilder`, e propositalmente **não** usa
`appsettings.json`), a tradução (seção 11) lida com **connection string**, que é dado tipicamente sensível e
variável por ambiente — nesse caso, `appsettings.json`/`IConfiguration` é a fonte natural, seguindo a
convenção padrão do .NET (`IConfiguration.GetConnectionString(...)`).

```csharp
// appsettings.json
// { "ConnectionStrings": { "LegacySqlServer": "Server=...;Database=...;Integrated Security=True;" } }

builder.Services.AddSqlFramework(SqlProvider.PostgreSql);  // dialeto de destino padrão (já existente, seção 8)
builder.Services.AddSqlTranslation(builder.Configuration, connectionStringName: "LegacySqlServer");
```

```csharp
public static class SqlTranslationServiceCollectionExtensions
{
    public static IServiceCollection AddSqlTranslation(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName)
    {
        var connectionString = configuration.GetConnectionString(connectionStringName)
            ?? throw new InvalidOperationException($"Connection string '{connectionStringName}' não encontrada.");

        services.AddSingleton<ISqlProviderDetector, SqlProviderDetector>();
        services.AddSingleton<ISqlTranslator>(sp => new SqlTranslator(
            sp.GetRequiredService<ISqlProviderDetector>(),
            sp.GetRequiredService<ISqlDialect>(),          // dialeto de destino padrão, resolvido do AddSqlFramework já registrado
            defaultSourceConnectionString: connectionString));

        return services;
    }
}
```

`AddSqlTranslation` depende de `ISqlDialect` já estar registrado (via `AddSqlFramework`) — deve lançar erro
claro no momento do registro se não estiver, em vez de falhar só na primeira chamada de `Translate()`.

O `ISqlTranslator` resolvido via DI já sabe, por padrão, qual é a connection string/dialeto de origem
configurado — o consumidor não precisa passá-la a cada chamada. Ainda assim, o método `Translate()` continua
aceitando informar a origem e o destino diretamente por parâmetro (ver seção 11), para os casos em que o
consumidor quer ignorar a configuração padrão e decidir explicitamente "de/para" naquela chamada específica —
útil por exemplo quando o mesmo serviço precisa traduzir a partir de origens diferentes em chamadas distintas.

---

## 9. Performance e concorrência

- **Cache de "shape" de query compilada**: hash estrutural da AST (ignorando valores literais, considerando estrutura) para evitar re-renderizar a mesma forma de query repetidamente. Maior ganho de performance esperado no projeto.
  - Implementado com `ConcurrentDictionary<QueryShapeKey, string>`; aceitar trabalho ocasionalmente duplicado em concorrência (mesmo padrão usado por EF Core/MemoryCache) — não usar `Lazy<T>` a menos que profiling justifique.
  - Necessário mecanismo de eviction (limite de tamanho + `Clear()` simples, ou `MemoryCache` do BCL) para evitar crescimento ilimitado quando há queries dinâmicas (`WhereRaw` variável).
- **Emissão de texto via `Span<char>`**: `SqlWriter` como `ref struct` com buffer via `ArrayPool<char>`, evitando alocações intermediárias na fase AST → string. Prioridade menor que o cache de shape — implementar apenas se profiling justificar.
- **Regras de thread-safety**:
  1. `SqlBuilder` é sempre thread-confined (documentar isso como contrato de API).
  2. Tudo que sobrevive ao `.Build()` é imutável (`SelectStatement`, `ISqlDialect`).
  3. O único estado mutável compartilhado do sistema é o cache de queries compiladas — isolado atrás de `ConcurrentDictionary`.

---

## 10. Segurança — proteção contra SQL Injection

| Camada | Mecanismo |
|---|---|
| Valores | Parametrização obrigatória e estrutural — nunca concatenação (garantido pela arquitetura). |
| Identificadores dinâmicos (ex: `sortBy` vindo de request externo) | `IdentifierAllowList` — whitelist explícita, obrigatória sempre que coluna/tabela vier de fonte externa. |
| Identificadores em geral | Validação automática (regex) dentro de `QuoteIdentifier()`, rodando em **todo** `Compile()`, sem exigir ação do dev. |
| Uso do escape hatch `Raw()` | Heurística de detecção de concatenação suspeita (aspas desbalanceadas) lançando exceção; método `...Unsafe()` como via deliberadamente marcada para casos fora do padrão. |
| Build-time | Analyzer Roslyn (`SQLF001`) detectando interpolação/concatenação em argumentos de `WhereRaw`/`SelectRaw` — falha o **build**, não só o runtime. |
| Observabilidade | Hook `OnCompiled` sinalizando queries que usam `Raw`, para auditoria. |

---

## 11. Tradução SQL → SQL entre dialetos

### Motivação

Nem todo consumidor do framework quer usar o pattern builder. Para quem prefere escrever SQL puro, esta
feature oferece um método que recebe uma instrução SQL de um dialeto de origem e devolve a mesma query já
traduzida para o dialeto de destino — convergindo para o mesmo `CompiledQuery` que o `SqlBuilder` produz,
pronto para ser usado com Dapper. Não é um subsistema novo, nem uma reestruturação do que já foi especificado
— é apenas mais um ponto de entrada, que reaproveita a AST e os renderers já desenhados.

> Escopo limitado à tradução em si: entrada é SQL, saída é SQL/`CompiledQuery`. Não inclui análise de
> projeto C#, Roslyn, nem geração de código C#/FluentBuilder — essa capacidade existe separadamente, na CLI
> de conversão de projeto (seção 12), que tem propósito e saída diferentes.

### Como funciona

```
SQL de origem (texto)
      │
      ▼
  Parser de terceiros (conforme dialeto de origem)
      │
      ▼
  Adapter -> AST própria do framework (a MESMA SelectStatement usada pelo SqlBuilder)
      │
      ▼
  IQueryRenderer do dialeto de destino (o MESMO renderer usado pelo Compile() do builder fluente)
      │
      ▼
  CompiledQuery { Sql, Parameters } — pronto para uso com Dapper, igual ao resultado do FluentBuilder
```

O parser de origem e o adapter para a AST são a única peça nova. O renderer de saída é reaproveitado sem
alteração — nenhum componente já especificado nas seções anteriores precisa ser modificado.

### Assinatura do método

`ISqlTranslator` expõe **três overloads**, para atender os diferentes cenários de uso sem forçar o
consumidor a sempre informar a origem manualmente:

```csharp
public interface ISqlTranslator
{
    // 1) Usa a connection string de origem e o dialeto de destino já configurados via AddSqlTranslation/AddSqlFramework (seção 8)
    TranslationResult Translate(string sourceSql);

    // 2) Informa uma connection string de origem específica para esta chamada (dialeto detectado a partir dela)
    TranslationResult Translate(string sourceSql, string sourceConnectionString, ISqlDialect targetDialect);

    // 3) Informa explicitamente o "de/para", sem nenhuma detecção envolvida
    TranslationResult Translate(string sourceSql, SqlProvider sourceDialect, ISqlDialect targetDialect);
}

public interface ISqlProviderDetector
{
    SqlProvider Detect(string connectionString);
}

public sealed class TranslationResult
{
    public CompiledQuery? Compiled { get; init; }          // mesmo formato usado pelo builder -> pronto para Dapper
    public SqlProvider DetectedSourceProvider { get; init; } // sempre reportado, mesmo quando a detecção é implícita
    public IReadOnlyList<string> UntranslatableConstructs { get; init; } = Array.Empty<string>();
    public bool IsFullyTranslated => UntranslatableConstructs.Count == 0;
}
```

```csharp
// Caso mais comum: origem/destino já configurados no Program.cs (appsettings + AddSqlTranslation)
var result = translator.Translate("SELECT TOP 10 Id, Name FROM Users WHERE Age > 18 ORDER BY CreatedAt DESC");

// Caso com connection string avulsa, diferente da configurada por padrão
var result2 = translator.Translate(
    "SELECT TOP 10 Id, Name FROM Users WHERE Age > 18 ORDER BY CreatedAt DESC",
    "Server=outroserver;Database=OutroBanco;Integrated Security=True;",
    SqlDialects.PostgreSql);

// Caso explícito, como era feito originalmente — sem detecção nenhuma
var result3 = translator.Translate(
    "SELECT TOP 10 Id, Name FROM Users WHERE Age > 18 ORDER BY CreatedAt DESC",
    SqlProvider.SqlServer,
    SqlDialects.PostgreSql);

// result.Compiled.Sql: "SELECT Id, Name FROM Users WHERE Age > 18 ORDER BY CreatedAt DESC LIMIT 10"
// result.DetectedSourceProvider: SqlProvider.SqlServer

// uso direto com Dapper, igual a qualquer CompiledQuery vindo do FluentBuilder:
var rows = await connection.QueryAsync<User>(result.Compiled.Sql, result.Compiled.Parameters);
```

### Detecção do dialeto de origem — heurística, não infalível

A detecção por connection string (overloads 1 e 2) é feita por `ISqlProviderDetector`, usando palavras-chave
e padrões característicos de cada driver. É importante deixar claro que essa detecção é **heurística**:
connection strings não seguem um padrão universal obrigatório, e é possível (embora raro na prática) que uma
string customizada não seja reconhecida com segurança.

| Provider | Sinais característicos na connection string |
|---|---|
| SQL Server | `Data Source=`, `Integrated Security=`, `Initial Catalog=`, ausência de `Port=` explícito |
| PostgreSQL | `Host=`, `Port=5432` (padrão), `Username=` |
| MySQL | `Server=` combinado com `Uid=`/`Pwd=`, `Port=3306` (padrão) |

Regras de política para a detecção:

- Se a heurística identificar o provider **com confiança** (match de padrão característico e inequívoco),
  segue direto para a tradução.
- Se a string for **ambígua** (ex: passou apenas `Server=x;Database=y;User=z;Password=w`, que é compatível
  com mais de um driver), o `Translate()` deve lançar `AmbiguousConnectionStringException`, pedindo que o
  consumidor use o overload explícito (`SqlProvider`) — nunca adivinhar silenciosamente e arriscar traduzir a
  partir do dialeto errado.
- O **overload explícito** (`SqlProvider sourceDialect`) sempre continua disponível, sem qualquer dependência
  de connection string ou configuração via DI — é o caminho mais simples quando o consumidor só tem o texto
  do SQL em mãos, sem acesso a uma connection string real.

### Parsing do dialeto de origem — não construir do zero

Mesma diretriz já válida para o restante do projeto: usar parsers maduros e mantidos por terceiros para
interpretar o SQL de entrada, em vez de escrever um parser SQL artesanal.

| Dialeto de origem | Parser recomendado |
|---|---|
| SQL Server (T-SQL) | `Microsoft.SqlServer.TransactSql.ScriptDom` (oficial da Microsoft) |
| PostgreSQL | `PgQuery.NET` (binding do parser real do Postgres) |
| MySQL | Parser ANTLR de MySQL ou equivalente |

### Limites — comunicar de forma honesta

A tradução cobre bem tudo que já está modelado na AST (SELECT, JOIN, WHERE, GROUP BY/HAVING, paginação,
subqueries, CTEs). Construções sem equivalente direto no destino (ex: `FULL JOIN` traduzido para MySQL,
funções proprietárias sem tradução como `OPENJSON`/`CROSS APPLY`) **não devem ser silenciosamente ignoradas
ou mal traduzidas**. Nesses casos, `TranslationResult.UntranslatableConstructs` deve listar exatamente o que
não pôde ser traduzido, para revisão manual — o mesmo princípio de fail-fast já aplicado ao `Compile()` do
builder fluente (ver seção 7).

---

## 12. CLI — conversão de projeto para FluentBuilder

### Papel da CLI

O papel da CLI é diferente do método `Translate()` (seção 11), que só converte SQL em SQL. **A CLI existe para
quando o usuário quer converter todo (ou parte de) um projeto C# existente para usar o `FluentBuilder`** — ou
seja, ela encontra SQL já escrito na aplicação (embutido em chamadas Dapper, ADO.NET, EF Core raw SQL, etc.) e
gera o código C# equivalente em FluentBuilder, no lugar do SQL cru.

> Diferença importante em relação à seção 11: `Translate()` sempre produz **SQL** como saída (útil para
> quem não quer usar o pattern builder). A CLI descrita aqui produz **código C#** como saída (para quem quer
> justamente adotar o pattern builder no projeto).

### Escopo de execução

A conversão pode ser aplicada em três granularidades, para dar controle ao usuário sobre o quanto do projeto
será convertido de uma vez:

```bash
# projeto inteiro
myframework-convert --scope project --path ./MyApp.csproj --source sqlserver

# um arquivo só
myframework-convert --scope file --path ./Repositories/UserRepository.cs --source sqlserver

# um método específico
myframework-convert --scope method --path ./Repositories/UserRepository.cs \
    --type UserRepository --method GetActiveUsers --source sqlserver
```

```csharp
public abstract record ConversionScope
{
    public sealed record Project(string CsprojPath) : ConversionScope;
    public sealed record File(string FilePath) : ConversionScope;
    public sealed record Method(string FilePath, string TypeName, string MethodName) : ConversionScope;
}
```

### Como funciona

```
Escopo (projeto / arquivo / método)
      │
      ▼
Fase 1 — Descoberta: Roslyn (SyntaxWalker + SemanticModel) localiza SQL em string literals dentro
          do código, identificando o contexto de uso (Dapper, ADO.NET puro, EF Core raw SQL).
          Concatenação de string (`+`) é marcada como "requer revisão manual", não convertida automaticamente.
      │
      ▼
Fase 2 — Parsing: parser de terceiros por dialeto de origem (ScriptDom / PgQuery / ANTLR — mesma
          diretriz da seção 11: não construir parser do zero) transforma o SQL encontrado na
          mesma AST usada pelo SqlBuilder (SelectStatement, etc.).
      │
      ▼
Fase 3 — Geração de código: FluentCodeGenerator percorre a AST e emite o código C# equivalente
          em FluentBuilder. Trechos sem mapeamento direto viram WhereRaw/SelectRaw com comentário
          // TODO: revisar, em vez de falhar a conversão inteira.
      │
      ▼
Fase 4 — Aplicação: por padrão, dry-run (gera relatório/preview, não sobrescreve arquivos).
          Sobrescrita real do arquivo (via Roslyn SyntaxNode.ReplaceNode, preservando formatação)
          é opt-in explícito via --apply.
```

### Relatório de conversão

```
Relatório de Conversão
──────────────────────────────────────────
Escopo: Projeto MyApp.csproj
Total de ocorrências de SQL encontradas: 87

✅ Totalmente convertidas:    61  (70%)
⚠️  Parcialmente convertidas:  19  (22%) — usam WhereRaw/SelectRaw, revisar
❌ Não convertidas (manual):    7  (8%)  — SQL concatenado ou parse falhou

Arquivos afetados: 14
```

Categorias de resultado por ocorrência:

```csharp
public enum ConversionFinding
{
    FullyConverted,
    PartiallyConverted,
    RequiresManualReview,
    SecurityRisk_StringConcatenation   // categoria própria, prioridade alta no relatório
}
```

SQL concatenado (`"SELECT * FROM Users WHERE Id = " + userId`) é tratado como achado de **risco de
segurança**, não apenas como "não convertido" — a CLI é também uma oportunidade de identificar esse padrão
perigoso em código legado, reaproveitando o princípio de proteção contra SQL Injection já especificado na
seção 10.

### Regras de política

- **Dry-run é o padrão.** Sobrescrever arquivos exige `--apply` explícito — nunca é o comportamento padrão.
- **Nada de silêncio em caso de falha parcial.** Trechos não convertíveis viram `Raw()` comentado, nunca são
  descartados silenciosamente nem impedem a conversão do restante do arquivo.
- **Concatenação de string nunca é convertida automaticamente** — sempre marcada para revisão manual, dado o
  risco de reconstituir incorretamente a intenção original da query.

---

## 13. Sugestão de índices (Index Advisor)

- Extrai candidatos a índice diretamente da AST já existente no `Compile()` — sem custo de parsing adicional.
- Sinais considerados: filtros de igualdade/range em `WHERE`, chaves de `JOIN`, colunas de `ORDER BY`/`GROUP BY`.
- Regra de ordenação em índices compostos: colunas de igualdade antes de colunas de range (eficiência de B-tree, válido nos 3 bancos).
- Valor real vem da **análise agregada** (frequência de uso de cada candidato através de todas as queries do app), não da análise isolada por query.
- Geração de DDL de sugestão por dialeto (`CREATE INDEX`), nunca aplicado automaticamente — sempre saída para revisão humana.
- Pontos de integração: warning opcional em dev-time no `Compile()`; comando standalone de análise (útil em CI).
- Limitação a documentar explicitamente: a ferramenta não conhece seletividade real de dados, volume de tabela, nem índices já existentes — é um assistente, não um substituto de DBA.

---

## 14. Estratégia de testes

- **Golden tests**: mesma AST/cenário de query, comparada contra o SQL esperado nos 3 dialetos lado a lado.
- Usar `ISqlDialect.Supports(SqlFeature)` para pular automaticamente cenários não aplicáveis a um dialeto, com mensagem clara de skip.
- Testes de concorrência para o `CompiledQueryCache` (validar ausência de corrupção sob `GetOrAdd` concorrente).
- (Fora do escopo desta spec, mas a decidir depois): uso de Testcontainers para testes de integração real contra os 3 engines, caso o projeto evolua para validar SQL gerado contra bancos reais.

---

## 15. Estrutura de projeto sugerida

```
MyFramework.Core          -> AST, interfaces (ISqlDialect, IQueryRenderer), SqlBuilder fluente, cache
MyFramework.SqlServer     -> ISqlDialect + renderers para SQL Server
MyFramework.PostgreSql    -> ISqlDialect + renderers para PostgreSQL
MyFramework.MySql         -> ISqlDialect + renderers para MySQL
MyFramework.Translation   -> ISqlTranslator + ISqlProviderDetector (parsers de terceiros) — biblioteca, seção 11
MyFramework.Migration     -> CLI de conversão de projeto para FluentBuilder (Roslyn + parsers + FluentCodeGenerator) — seção 12
MyFramework.Analysis      -> Index Advisor (pode ser parte do Core ou pacote próprio)
MyFramework.Tests         -> golden tests, testes de concorrência
```

Princípio: `MyFramework.Core` não carrega dependências pesadas de parsing (ScriptDom/PgQuery/ANTLR) nem
Roslyn — essas ficam isoladas em `MyFramework.Translation` (biblioteca de tradução SQL→SQL) e
`MyFramework.Migration` (CLI de conversão de projeto para código), para não onerar quem só usa o builder
fluente no dia a dia. Os dois pacotes compartilham os parsers de terceiros por dialeto de origem, mas têm
saídas diferentes: um gera SQL, o outro gera código C#.

---

## 16. Roadmap sugerido (fases)

**Fase 1 — Núcleo (MVP)**
- AST + `SqlBuilder` para `SELECT` completo (colunas, DISTINCT, WHERE, JOIN, GROUP BY/HAVING, ORDER BY, paginação).
- `ISqlDialect` + renderers para os 3 bancos.
- `INSERT`/`UPDATE`/`DELETE`.
- Escape hatch `Raw()` com proteção básica.
- DI (`AddSqlFramework`).
- Golden tests dos 3 dialetos.

**Fase 2 — Robustez**
- CTEs/subqueries.
- Cache de queries compiladas (thread-safe).
- Proteção contra `DELETE`/`UPDATE` sem `WHERE`.
- `IdentifierAllowList` + validação automática de identificadores.
- `ToDebugString()` / hook de logging.

**Fase 3 — Diferenciais**
- Window functions.
- Procedures/Functions (`RoutineCall`).
- Analyzer Roslyn de segurança (`SQLF001`).
- Index Advisor (análise por query).

**Fase 4 — Ferramentas auxiliares**
- Método `Translate()` (`ISqlTranslator`) — tradução direta de SQL entre dialetos (seção 11).
- CLI de conversão de projeto para FluentBuilder (seção 12): descoberta via Roslyn, parsing por dialeto,
  `FluentCodeGenerator`, dry-run/`--apply`, relatório com categorização de risco de segurança.
- Index Advisor agregado.
- Suporte a JSON/Upsert.

**Fase 5 — Avançado (sob demanda)**
- Validação schema-aware.
- Emulação de features ausentes (ex: `FULL JOIN` em MySQL).
- Versionamento fino por versão de engine.

---

## 17. Riscos conhecidos / pontos de atenção para o time

- `FULL JOIN` no MySQL e `TOP N PERCENT` no SQL Server não têm tradução trivial — decidir política antes de prometer suporte total a JOIN/paginação.
- Parsing de SQL de entrada na tradução (seção 11) depende inteiramente da qualidade do parser de terceiros escolhido por dialeto — não subestimar o esforço de manter o adapter atualizado conforme esses parsers evoluem.
- A CLI de conversão de projeto (seção 12) tem risco de qualidade maior que a tradução SQL→SQL: gerar código C# incorreto (ainda que compile) é mais perigoso que gerar SQL incorreto, porque o erro pode passar despercebido em revisão de código. Reforçar sempre o modo dry-run como padrão e nunca prometer 100% de cobertura automática.
- Cache de queries compiladas sem eviction pode crescer indefinidamente em cenários com muito `Raw()` dinâmico — precisa de limite desde o início.
- Diferenças de versão dentro do mesmo banco (ex: recursos exclusivos de Postgres 15+) podem gerar expectativa equivocada se o dialeto não deixar clara a versão mínima suportada.
