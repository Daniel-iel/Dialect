# Spec — Framework FluentBuilder SQL Multi-Dialeto (.NET)

## Status
Documento de alinhamento inicial. Escopo: **geração de SQL** (texto + parâmetros).
Execução/mapeamento de resultado fica fora do escopo (será feita via Dapper, em pacote separado, não tratado aqui).

---

## 1. Objetivo

Criar um framework .NET que permite montar instruções SQL através de uma API fluente (FluentBuilder),
produzindo `(SQL, Parâmetros)` corretos para diferentes bancos de dados de destino — **SQL Server**, **PostgreSQL**
e **MySQL** — a partir de uma única definição de query, escrita de forma agnóstica de dialeto.

### Princípio central
> O FluentBuilder nunca gera SQL diretamente. Ele monta uma árvore de objetos (AST) que representa a
> intenção da query. Só no momento de `Compile()` um `Renderer` específico do dialeto traduz essa árvore em
> texto SQL + parâmetros.

---

## 2. Fora de escopo (explícito)

Para evitar ambiguidade de escopo no início do projeto, os itens abaixo **não** fazem parte deste framework:

- Execução de comandos contra o banco (conexão, transação, retry, pooling) — fica a cargo do consumidor via Dapper.
- Mapeamento de resultado para objetos (`Query<T>`) — responsabilidade do Dapper.
- ORM completo (change tracking, lazy loading, unit of work).
- Suporte a Oracle ou outros bancos além dos 3 definidos (arquitetura deve permitir adicionar depois, mas não é v1).
- Parser de SQL genérico para uso em produção (parsing existe apenas como ferramenta de migração offline/CLI).

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
- Seção 14 (estrutura de projeto) — cada dialeto já nasce como pacote isolado, o que é o pré-requisito
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

---

## 8. Configuração / DI

- Registro via `IServiceCollection.AddSqlFramework(...)`, com **três overloads**:
  - Simples: `AddSqlFramework(SqlProvider provider)` — cobre os bancos nativos do core (SQL Server, PostgreSQL, MySQL).
  - Completo: `AddSqlFramework(Action<SqlFrameworkOptions> configure)` — permite adicionar opções futuras (ex: `CompiledQueryCacheMaxEntries`) sem quebrar API pública.
  - **Extensível**: `AddSqlFramework(ISqlDialect customDialect)` — permite registrar um dialeto que não pertence ao enum `SqlProvider` do core, viabilizando o uso de pacotes de terceiros (ex: `MyFramework.Oracle`) sem exigir alteração no core para reconhecer o novo banco.
- Sem uso de `appsettings.json`/`IOptions<T>` — configuração é feita diretamente no `Program.cs`, com validação síncrona e fail-fast no momento do registro.
- `ISqlDialect` registrado como **singleton** (é stateless por definição de design).
- Cenário multi-provider (ex: multi-tenant com bancos diferentes) fica como extensão futura via `ISqlDialectFactory` — não é requisito de v1, mas a API não deve impedir essa evolução. A mesma fábrica deve conseguir resolver tanto dialetos nativos quanto dialetos de terceiros registrados via `ISqlDialect customDialect`.

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
| Legado (migração) | Categoria própria de achado `SecurityRisk_StringConcatenation` na ferramenta de migração — não tratar apenas como "não migrado". |

---

## 11. Ferramenta de migração (CLI) — visão geral

Objetivo: assistir a migração de sistemas existentes (SQL espalhado em código C#) para o FluentBuilder, com granularidade de escopo.

- **Escopo de execução**: projeto inteiro / arquivo único / método único (`MigrationScope`).
- **Fase 1 — Descoberta**: usar **Roslyn** (`SyntaxWalker` + `SemanticModel`) para localizar SQL em string literals dentro do código, identificando o contexto de uso (Dapper, ADO.NET puro, EF Core raw SQL). Casos de concatenação (`+`) devem ser marcados como "requer revisão manual" / risco de segurança, não parseados automaticamente.
- **Fase 2 — Parsing do SQL encontrado**: **não construir parser SQL do zero**. Usar parsers maduros já existentes por dialeto de origem:
  - SQL Server → `Microsoft.SqlServer.TransactSql.ScriptDom` (oficial).
  - PostgreSQL → `PgQuery.NET` (binding do parser real do Postgres).
  - MySQL → parser ANTLR de MySQL ou equivalente.
- **Fase 3 — Adapter**: mapear a AST rica do parser de terceiros para a AST própria do framework (`SelectStatement`, etc.).
- **Fase 4 — Geração de código**: `FluentCodeGenerator` produz o código C# equivalente em FluentBuilder. Trechos não mapeáveis viram `WhereRaw`/`SelectRaw` com comentário `// TODO: revisar`, em vez de falhar a query inteira.
- **Aplicação da mudança**: por padrão, **dry-run** (gera relatório/preview, não sobrescreve arquivos). Sobrescrita automática via Roslyn (`ReplaceNode`) deve ser opt-in explícito (`--apply`).
- **Relatório**: total de queries encontradas, classificadas em totalmente migradas / parcialmente migradas / requer revisão manual / risco de segurança.

---

## 12. Sugestão de índices (Index Advisor)

- Extrai candidatos a índice diretamente da AST já existente no `Compile()` — sem custo de parsing adicional.
- Sinais considerados: filtros de igualdade/range em `WHERE`, chaves de `JOIN`, colunas de `ORDER BY`/`GROUP BY`.
- Regra de ordenação em índices compostos: colunas de igualdade antes de colunas de range (eficiência de B-tree, válido nos 3 bancos).
- Valor real vem da **análise agregada** (frequência de uso de cada candidato através de todas as queries do app), não da análise isolada por query.
- Geração de DDL de sugestão por dialeto (`CREATE INDEX`), nunca aplicado automaticamente — sempre saída para revisão humana.
- Pontos de integração: warning opcional em dev-time no `Compile()`; comando dedicado na CLI de migração; comando standalone de análise (útil em CI).
- Limitação a documentar explicitamente: a ferramenta não conhece seletividade real de dados, volume de tabela, nem índices já existentes — é um assistente, não um substituto de DBA.

---

## 13. Estratégia de testes

- **Golden tests**: mesma AST/cenário de query, comparada contra o SQL esperado nos 3 dialetos lado a lado.
- Usar `ISqlDialect.Supports(SqlFeature)` para pular automaticamente cenários não aplicáveis a um dialeto, com mensagem clara de skip.
- Testes de concorrência para o `CompiledQueryCache` (validar ausência de corrupção sob `GetOrAdd` concorrente).
- (Fora do escopo desta spec, mas a decidir depois): uso de Testcontainers para testes de integração real contra os 3 engines, caso o projeto evolua para validar SQL gerado contra bancos reais.

---

## 14. Estrutura de projeto sugerida

```
MyFramework.Core          -> AST, interfaces (ISqlDialect, IQueryRenderer), SqlBuilder fluente, cache
MyFramework.SqlServer     -> ISqlDialect + renderers para SQL Server
MyFramework.PostgreSql    -> ISqlDialect + renderers para PostgreSQL
MyFramework.MySql         -> ISqlDialect + renderers para MySQL
MyFramework.Migration     -> CLI de migração (Roslyn + parsers de terceiros) — dependências pesadas, pacote isolado
MyFramework.Analysis      -> Index Advisor (pode ser parte do Core ou pacote próprio)
MyFramework.Tests         -> golden tests, testes de concorrência
```

Princípio: `MyFramework.Core` não deve carregar dependências pesadas (Roslyn, parsers de terceiros) — essas ficam isoladas nos pacotes de migração/análise, para não onerar quem só quer usar o builder no dia a dia.

---

## 15. Roadmap sugerido (fases)

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
- CLI de migração (descoberta via Roslyn + parsers de terceiros + geração de código).
- Index Advisor agregado (integrado à CLI).
- Suporte a JSON/Upsert.

**Fase 5 — Avançado (sob demanda)**
- Validação schema-aware.
- Emulação de features ausentes (ex: `FULL JOIN` em MySQL).
- Versionamento fino por versão de engine.

---

## 16. Riscos conhecidos / pontos de atenção para o time

- `FULL JOIN` no MySQL e `TOP N PERCENT` no SQL Server não têm tradução trivial — decidir política antes de prometer suporte total a JOIN/paginação.
- Parsing de SQL livre (fora do contexto de migração controlada) é um projeto por si só — não subestimar caso o escopo cresça nessa direção.
- Cache de queries compiladas sem eviction pode crescer indefinidamente em cenários com muito `Raw()` dinâmico — precisa de limite desde o início.
- Diferenças de versão dentro do mesmo banco (ex: recursos exclusivos de Postgres 15+) podem gerar expectativa equivocada se o dialeto não deixar clara a versão mínima suportada.
