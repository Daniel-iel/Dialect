<div align="center">

# Dialect — Multi-Dialect SQL Query Builder for .NET

[![Contributors](https://img.shields.io/github/contributors/Daniel-iel/Dialect)](https://github.com/Daniel-iel/Dialect/graphs/contributors)
[![Activity](https://img.shields.io/github/commit-activity/m/Daniel-iel/Dialect)](https://github.com/Daniel-iel/Dialect/graphs/commit-activity)
[![CI](https://github.com/Daniel-iel/Dialect/actions/workflows/ci.yml/badge.svg)](https://github.com/Daniel-iel/Dialect/actions/workflows/ci.yml)
[![Documentation](https://github.com/Daniel-iel/Dialect/actions/workflows/ci-documentation.yml/badge.svg)](https://github.com/Daniel-iel/Dialect/actions/workflows/ci-documentation.yml)
[![Benchmarks](https://github.com/Daniel-iel/Dialect/actions/workflows/ci-benchmark.yml/badge.svg)](https://github.com/Daniel-iel/Dialect/actions/workflows/ci-benchmark.yml)

[![NuGet](https://img.shields.io/nuget/v/Dialect.Core)](https://www.nuget.org/packages/Dialect.Core/)
[![Downloads](https://img.shields.io/nuget/dt/Dialect.Core)](https://www.nuget.org/packages/Dialect.Core/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE.md)
[![Code Factor](https://www.codefactor.io/repository/github/Daniel-iel/Dialect/badge)](https://www.codefactor.io/repository/github/Daniel-iel/Dialect)
[![codecov](https://codecov.io/github/Daniel-iel/Dialect/graph/badge.svg?token=YOUR_TOKEN)](https://codecov.io/github/Daniel-iel/Dialect)
[![Known Vulnerabilities](https://snyk.io/test/github/daniel-iel/dialect/badge.svg)](https://snyk.io/test/github/daniel-iel/dialect)
[![Buy Me A Coffee](https://img.shields.io/badge/Buy%20Me%20A%20Coffee-support-yellow?style=flat&logo=buy-me-a-coffee)](https://buymeacoffee.com/danieliel)

[📖 Documentation](https://github.com/Daniel-iel/Dialect/wiki) | [🚀 Quick Start](#quick-start) | [💡 Examples](https://github.com/Daniel-iel/Dialect/tree/main/examples) | [📚 API Reference](https://github.com/Daniel-iel/Dialect/wiki/API-Reference)

> **A fluent, type-safe SQL query builder that compiles to SQL Server, PostgreSQL, and MySQL from a single dialect-agnostic definition.**

</div>

---

## 📚 Table of Contents

- [What is Dialect?](#what-is-dialect)
- [Key Features](#key-features)
- [Supported Databases](#supported-databases)
- [Installation](#installation)
- [Quick Start](#quick-start)
- [Architecture](#architecture)
- [Advanced Features](#advanced-features)
- [CLI Tools](#cli-tools)
- [Extensibility](#extensibility)
- [Project Structure](#project-structure)
- [Contributing](#contributing)
- [Support](#support)
- [Roadmap](#roadmap)
- [License](#license)

---

## What is Dialect?

**Dialect** is a .NET framework that lets you build SQL queries once and compile them for multiple database engines. No more rewriting T-SQL for PostgreSQL or MySQL — your fluent API definitions become correct SQL for any supported dialect.

### The Core Principle

```
Build (Fluent API) → AST (Abstract Syntax Tree) → Render (Dialect-Specific SQL)
```

The fluent API never generates SQL directly. Instead, it builds an **Abstract Syntax Tree** representing your query's intent. Only at compile time does a dialect-specific renderer translate the AST into proper SQL with correct parameter syntax, quoting, and functions.

### Why Dialect?

✅ **Write Once, Compile for Many** — Single query definition for multiple databases  
✅ **Type-Safe** — Compile-time validation and intellisense support  
✅ **Zero Runtime Overhead** — Query caching for optimal performance  
✅ **Extensible** — Add new database support without modifying core  
✅ **Integration Ready** — Works seamlessly with Dapper and ADO.NET  
✅ **Proven** — 394+ comprehensive tests covering all features  

---

## Key Features

### 🔨 Fluent Query Builder

Write queries naturally in C# using a chainable, fluent API:

```csharp
var query = SqlBuilder
    .Select("id", "name", "email")
    .From("users")
    .Where("status = @status")
    .OrderBy("created_at DESC")
    .Limit(10)
    .Compile(SqlProvider.PostgreSQL);

// Returns: parameterized SQL ready for Dapper execution
Console.WriteLine(query.Sql);      // SELECT "id", "name", "email" FROM "users" 
                                    // WHERE status = $1 ORDER BY "created_at" DESC LIMIT 10
Console.WriteLine(query.Parameters); // { "@status" => value }
```

### 🎯 Multi-Dialect Support

Single query definition, multiple targets — no rewriting needed:

```csharp
var builder = SqlBuilder.Select("*").From("orders").Where("total > @amount");

var sqlServer = builder.Compile(SqlProvider.SqlServer);   // T-SQL with @params
var postgres  = builder.Compile(SqlProvider.PostgreSQL);  // PostgreSQL with $n
var mysql     = builder.Compile(SqlProvider.MySQL);       // MySQL with ?
```

### 🛠️ Supported Operations

- **SELECT** — filtering, ordering, pagination, window functions, CTEs
- **INSERT** — single & batch, UPSERT with conflict handling
- **UPDATE** — conditional updates, FROM clauses, multi-table joins
- **DELETE** — safe deletion with WHERE filtering
- **Transactions** — transaction scoping and management
- **Migrations** — DDL operations (CREATE TABLE, ALTER, DROP, etc.)
- **Routines** — stored procedure & function calls

### 📊 Performance & Intelligence

- **Index Advisor** — analyzes queries and recommends missing indexes
- **Query Optimization** — detects inefficient patterns and suggests rewrites
- **Execution Plans** — parses SQL Server, PostgreSQL, MySQL execution plans
- **Schema Validation** — validates queries against known schema
- **Query Caching** — automatic compiled query caching for performance

### 🔌 Query Translation (SQL-to-SQL)

Convert existing SQL from one dialect to another:

```csharp
var sourceQuery = "SELECT TOP 10 * FROM users WHERE id = @id";  // T-SQL
var translated = Translator
    .FromDialect(SqlProvider.SqlServer)
    .ToDialect(SqlProvider.PostgreSQL)
    .Translate(sourceQuery);

// Result: SELECT * FROM users WHERE id = $1 LIMIT 10
```

---

## Supported Databases

| Database | Version | Features | Package |
|----------|---------|----------|---------|
| **SQL Server** | 2016+ | `[id]` quoting, `@param`, TOP/OFFSET, TVPs, JSON | `Dialect.SqlServer` |
| **PostgreSQL** | 9.6+ | `"id"` quoting, `$n` params, LIMIT/OFFSET, CTEs | `Dialect.PostgreSql` |
| **MySQL** | 5.7+ | `` `id` `` quoting, `?` params, LIMIT, JSON | `Dialect.MySql` |

### SQL Server (2016+)
✅ T-SQL syntax with `[identifier]` quoting  
✅ Parameter prefix `@parameter`  
✅ TOP/OFFSET pagination  
✅ Table-Valued Parameters (TVPs)  
✅ Execution plan JSON parsing  
✅ Index recommendations  

### PostgreSQL (9.6+)
✅ PostgreSQL syntax with `"identifier"` quoting  
✅ Parameter prefix `$n` (e.g., `$1`, `$2`)  
✅ LIMIT/OFFSET pagination  
✅ Common Table Expressions (CTEs)  
✅ JSON operators and functions  
✅ Array functions  

### MySQL (5.7+)
✅ MySQL syntax with `` `identifier` `` quoting  
✅ Parameter prefix `?` (placeholder-based)  
✅ LIMIT pagination  
✅ Generated columns support  
✅ Full-text search  

---

## Installation

### Prerequisites
- .NET 6.0 or higher
- Your target database client library (optional for query building)

### Via NuGet CLI

```bash
# Core framework
dotnet add package Dialect.Core

# Add dialect packages as needed
dotnet add package Dialect.SqlServer
dotnet add package Dialect.PostgreSql
dotnet add package Dialect.MySql
```

### Or via Package Manager Console

```powershell
Install-Package Dialect.Core
Install-Package Dialect.SqlServer
Install-Package Dialect.PostgreSql
Install-Package Dialect.MySql
```

---

## Quick Start

### 1. Basic SELECT Query

```csharp
using Dialect.Core.Fluent;
using Dialect.Core.Dialects;

// Build a SELECT query
var query = SqlBuilder
    .Select("id", "name", "email")
    .From("users")
    .Where("created_at >= @minDate")
    .OrderBy("name")
    .Compile(SqlProvider.PostgreSQL);

Console.WriteLine(query.Sql);
// Output: SELECT "id", "name", "email" FROM "users" 
//         WHERE created_at >= $1 ORDER BY "name"

var parameters = query.Parameters;  // { "@minDate" => value }
```

### 2. Using with Dapper

```csharp
using Dapper;

// Build query
var query = SqlBuilder
    .Select("*")
    .From("orders")
    .Where("status = @status")
    .Compile(SqlProvider.MySql);

// Execute with Dapper
using var connection = new MySqlConnection(connectionString);
var orders = connection.Query<Order>(
    query.Sql, 
    new { status = "completed" }
).ToList();
```

### 3. INSERT Query

```csharp
var insertQuery = SqlBuilder
    .Insert()
    .Into("users")
    .Columns("name", "email", "created_at")
    .Values("@name", "@email", "@now")
    .Compile(SqlProvider.SqlServer);

// T-SQL: 
// INSERT INTO [users] ([name], [email], [created_at]) 
// VALUES (@name, @email, @now)
```

### 4. UPSERT (INSERT ... ON CONFLICT)

```csharp
var upsertQuery = SqlBuilder
    .Upsert()
    .Into("users")
    .Columns("id", "name", "email")
    .Values("@id", "@name", "@email")
    .OnConflict("id")
    .UpdateColumns("name", "email")
    .Compile(SqlProvider.PostgreSQL);

// PostgreSQL: 
// INSERT INTO users (id, name, email) 
// VALUES ($1, $2, $3)
// ON CONFLICT (id) DO UPDATE SET name=$2, email=$3
```

### 5. UPDATE with WHERE

```csharp
var updateQuery = SqlBuilder
    .Update()
    .Table("orders")
    .Set("status = @status", "updated_at = @now")
    .Where("id = @orderId")
    .Compile(SqlProvider.PostgreSQL);

// PostgreSQL:
// UPDATE orders SET status = $1, updated_at = $2 
// WHERE id = $3
```

### 6. DELETE Safely

```csharp
var deleteQuery = SqlBuilder
    .Delete()
    .From("orders")
    .Where("status = @status AND created_at < @cutoffDate")
    .Compile(SqlProvider.MySql);

// MySQL:
// DELETE FROM orders 
// WHERE status = ? AND created_at < ?
```

---

## Architecture

### Layered Design

```
┌─────────────────────────────────────┐
│   Your Application Code             │
├─────────────────────────────────────┤
│   Fluent API (SqlBuilder)           │  ← You write queries here
├─────────────────────────────────────┤
│   AST (Abstract Syntax Tree)        │  ← Intermediate representation
├─────────────────────────────────────┤
│   ISqlDialect Interface             │  ← Dialect abstraction layer
├────────────┬────────────┬───────────┤
│ SqlServer  │ PostgreSQL │ MySQL     │  ← Dialect implementations
│ Renderer   │  Renderer  │ Renderer  │
└────────────┴────────────┴───────────┘
```

### Core Principle

The fluent API never generates SQL directly. It builds an AST, and only at compile time does a dialect-specific renderer translate the AST into proper SQL with:
- ✅ Correct parameter syntax (`@`, `$n`, `?`)
- ✅ Dialect-specific quoting (`[id]`, `"id"`, `` `id` ``)
- ✅ Function translation (CONCAT vs || vs +)
- ✅ Feature support negotiation (LIMIT/TOP/FETCH)

---

## Advanced Features

### Query Optimization & Recommendations

Dialect analyzes your queries and suggests optimizations:

```csharp
var optimizer = new SqlServerOptimizer();
var recommendations = optimizer.GenerateRecommendations(
    query: "SELECT * FROM orders WHERE status = 'pending'",
    executionMetrics: executionPlan
);

foreach (var rec in recommendations)
{
    Console.WriteLine($"{rec.Category}: {rec.Description}");
    // Output: Index → Consider adding index on (orders.status)
}
```

### Index Advisor

Analyze table scan operations and get missing index recommendations:

```csharp
var advisor = dialect.CreateIndexAdvisor();
var candidates = advisor.AnalyzeQuery(
    "SELECT * FROM large_table WHERE computed_column = @val"
);

// Returns: List<IndexCandidate>
// - TableName: "large_table"
// - Columns: ["computed_column"]
// - EstimatedImprovement: "~35% faster"
```

### Schema Validation

Validate queries against your schema before execution:

```csharp
var validator = dialect.CreateSchemaValidator();
validator.LoadSchema(schemaDefinition);

var result = validator.ValidateQuery(sqlQuery);
if (!result.IsValid)
{
    foreach (var error in result.Errors)
        Console.WriteLine($"Error: {error}");
}
```

### Execution Plan Analysis (SQL Server)

Parse and analyze SQL Server JSON execution plans:

```csharp
var planner = new SqlServerPlanAnalyzer();
var analysis = planner.AnalyzePlan(jsonExecutionPlan);

Console.WriteLine($"Table Scans: {analysis.TableScanCount}");
Console.WriteLine($"Index Seeks: {analysis.IndexSeekCount}");
Console.WriteLine($"Estimated Cost: {analysis.TotalCost}");

foreach (var tip in analysis.OptimizationTips)
    Console.WriteLine($"  💡 {tip}");
```

---

## CLI Tools

The `Dialect.Cli` package provides command-line tools for SQL discovery and analysis.

### SQL String Discovery

Automatically discover potential SQL strings in your codebase:

```bash
dotnet tool install --global Dialect.Cli

# Discover SQL strings
dialect discover-sql ./src

# Output:
# Found 15 suspicious SQL strings:
#   ✓ Program.cs:42: "SELECT * FROM users WHERE..."
#   ✓ Services/OrderService.cs:108: "INSERT INTO orders..."
```

### Query Analysis

Analyze queries for potential improvements:

```bash
dialect analyze-query \
  --dialect postgresql \
  --query "SELECT * FROM users WHERE created_at > '2024-01-01'"

# Output:
# ✓ Syntax: Valid PostgreSQL
# ⚠ Performance: Consider index on (created_at)
# ⚠ Pattern: Use parameter instead of literal date
```

---

## Project Structure

```
Dialect.sln
├── src/
│   ├── Dialect.Core/                    # Core framework
│   │   ├── AST/                         # Abstract Syntax Tree definitions
│   │   ├── Fluent/                      # Builder classes (SelectBuilder, etc.)
│   │   ├── Compilation/                 # Compilation engine & caching
│   │   ├── Dialects/                    # Dialect interface contracts
│   │   ├── Indexing/                    # Index analysis & recommendations
│   │   ├── Optimization/                # Query optimization logic
│   │   ├── Performance/                 # Performance analysis
│   │   └── Schema/                      # Schema validation
│   │
│   ├── Dialect.SqlServer/               # SQL Server dialect
│   │   ├── SqlServerDialect.cs
│   │   ├── Rendering/                   # T-SQL rendering
│   │   ├── Performance/                 # T-SQL optimization
│   │   └── Indexing/                    # T-SQL index advisor
│   │
│   ├── Dialect.PostgreSql/              # PostgreSQL dialect
│   │   ├── PostgreSqlDialect.cs
│   │   ├── Rendering/                   # PostgreSQL rendering
│   │   └── (similar structure)
│   │
│   ├── Dialect.MySql/                   # MySQL dialect
│   │   ├── MySqlDialect.cs
│   │   ├── Rendering/                   # MySQL rendering
│   │   └── (similar structure)
│   │
│   └── Dialect.Cli/                     # Command-line tools
│       ├── SqlStringDiscovery.cs        # Find SQL strings in code
│       └── QueryAnalyzer.cs             # Query analysis
│
├── tests/
│   └── Dialect.Tests/                   # Comprehensive test suite (394+ tests)
│       ├── Performance/
│       ├── Optimization/
│       ├── Integration/
│       ├── Schema/
│       ├── Versioning/
│       └── Indexing/
│
├── docs/                                # Documentation
│   ├── getting-started.md
│   ├── api-reference.md
│   └── examples/
│
├── spec.md                              # Original specification (Portuguese)
├── specv2.md                            # Updated specification (Portuguese)
└── README.md                            # This file
```

---

## Extensibility

### Adding Support for New Databases

Dialect is designed to be extended without modifying core code. Adding a new database (e.g., Oracle, SQLite) requires:

#### 1. Create New Package

```bash
mkdir src/Dialect.Oracle
cd src/Dialect.Oracle
dotnet new classlib -n Dialect.Oracle
```

#### 2. Implement ISqlDialect

```csharp
public class OracleDialect : ISqlDialect
{
    public char IdentifierQuote => '"';
    public string ParameterPrefix => ":";
    
    public bool Supports(SqlFeature feature) => 
        feature switch
        {
            SqlFeature.WindowFunctions => true,
            SqlFeature.CommonTableExpressions => true,
            SqlFeature.JsonOperators => true,
            _ => false
        };
    
    public string RenderFunction(string name, IReadOnlyList<string> args) =>
        name.ToUpper() switch
        {
            "CONCAT" => $"CONCAT({string.Join(", ", args)})",
            _ => $"{name}({string.Join(", ", args)})"
        };
    
    public IQueryRenderer CreateQueryRenderer() => new OracleQueryRenderer();
    public IRoutineRenderer CreateRoutineRenderer() => new OracleRoutineRenderer();
    public IMigrationRenderer CreateMigrationRenderer() => new OracleMigrationRenderer();
    // ... other interface methods
}
```

#### 3. Implement Renderers

- `IQueryRenderer` — for SELECT, INSERT, UPDATE, DELETE
- `IRoutineRenderer` — for stored procedures and functions
- `IMigrationRenderer` — for DDL (CREATE TABLE, ALTER, etc.)

#### 4. Register in DI (Optional)

```csharp
services.RegisterDialect<OracleDialect>();
```

**Result:** Zero modifications to core code. Total isolation.

---

## Contributing

We welcome contributions! Please follow these guidelines:

### Development Setup

```bash
# Clone the repository
git clone https://github.com/Daniel-iel/Dialect.git
cd Dialect

# Restore dependencies
dotnet restore

# Build solution
dotnet build Dialect.sln

# Run all tests
dotnet test tests/Dialect.Tests -c Release
```

### Create Feature Branch

```bash
git checkout -b feature/your-feature-name
git commit -m "feat: add your feature"
git push origin feature/your-feature-name
```

### Testing Requirements

- ✅ All new features must include unit tests
- ✅ Maintain >80% code coverage
- ✅ Run full test suite before submitting:

```bash
dotnet test tests/Dialect.Tests -c Release --logger "console;verbosity=detailed"
```

### Code Style

- Use 4-space indentation
- Follow [C# Naming Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Add XML documentation to public APIs
- Use nullable reference types (`#nullable enable`)

### Pull Request Process

1. Fork the repository
2. Create feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'feat: add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open Pull Request with description

---

## Support

### Getting Help

- 📖 **[Documentation](https://github.com/Daniel-iel/Dialect/wiki)** — Comprehensive guides
- 💬 **[GitHub Discussions](https://github.com/Daniel-iel/Dialect/discussions)** — Ask questions
- 🐛 **[GitHub Issues](https://github.com/Daniel-iel/Dialect/issues)** — Report bugs or request features
- 📧 **Email** — [your-email@example.com](mailto:your-email@example.com)

### Additional Resources

- [spec.md](spec.md) — Detailed technical specification (Portuguese)
- [specv2.md](specv2.md) — Updated specification with SQL translation (Portuguese)
- [Examples](https://github.com/Daniel-iel/Dialect/tree/main/examples) — Code examples
- [API Reference](https://github.com/Daniel-iel/Dialect/wiki/API-Reference) — Complete API documentation

---

## What's NOT Included

These fall outside Dialect's scope (by design):

- **Command Execution** — Use Dapper, Entity Framework Core, or ADO.NET
- **Result Mapping** — Dapper handles `Query<T>` mapping
- **ORM Features** — Change tracking, lazy loading, unit of work patterns
- **Connection Management** — Pooling, transactions, retry logic

**Dialect is query generation only** and plays well with Dapper, EF Core, and other data access frameworks.

---

## Roadmap

### ✅ Completed (v1)
- Fluent builders (SELECT, INSERT, UPDATE, DELETE, UPSERT)
- Multi-dialect compilation (SQL Server, PostgreSQL, MySQL)
- AST-based rendering
- Query translation (SQL-to-SQL)
- Index advisor
- Query optimization
- CLI tools
- Comprehensive test suite (394+ tests)
- Schema validation

### 🔄 In Progress (v1.1)
- Performance benchmarks
- Enhanced error messages
- Query caching improvements

### 🔲 Planned (v2+)
- Oracle database support
- SQLite database support
- Advanced query caching and statistics
- GUI query builder
- Entity Framework Core integration
- Async execution helpers
- Code generation from SQL files

---

## License

This project is licensed under the **MIT License** — see [LICENSE.md](LICENSE.md) for details.

---

## Acknowledgments

Thanks to all our [contributors](https://github.com/Daniel-iel/Dialect/graphs/contributors) for their support and dedication to making Dialect better!

---

## Sponsorship

If you find Dialect helpful, consider supporting the project:

- ⭐ Star the repository on GitHub
- 🐛 Report issues and suggest features
- 📝 Contribute code and documentation
- ☕ [Buy me a coffee](https://buymeacoffee.com/danieliel)

---

<div align="center">

### Em Português / Portuguese

**Dialect** é um framework .NET que permite construir queries SQL uma única vez e compilá-las para múltiplos bancos de dados (SQL Server, PostgreSQL, MySQL). Não precisa mais reescrever suas queries para cada banco — uma definição agnóstica de dialeto compila para qualquer banco suportado.

#### 🚀 Recursos Principais

- 🔨 **API Fluente** — escreva queries naturalmente em C#
- 🎯 **Multi-Dialeto** — compile para SQL Server, PostgreSQL e MySQL
- 📊 **Análise & Otimização** — recomendações de índices, otimizações de query
- 🔌 **Tradução SQL-para-SQL** — converta queries de um dialeto para outro
- 🛠️ **Ferramentas CLI** — descubra e analise strings SQL
- ✅ **394+ Testes** — suite de testes abrangente

#### 📚 Documentação

- [GitHub Discussions](https://github.com/Daniel-iel/Dialect/discussions) — Dúvidas em Português

#### 📦 Instalação

```bash
dotnet add package Dialect
dotnet add package Dialect.SqlServer
dotnet add package Dialect.PostgreSql
dotnet add package Dialect.MySql
```

#### 💡 Exemplo Rápido

```csharp
var query = SqlBuilder
    .Select("id", "nome", "email")
    .From("usuarios")
    .Where("status = @status")
    .OrderBy("data_criacao")
    .Compile(SqlProvider.PostgreSQL);

// Resultado: SQL parameterizado pronto para Dapper
```

**Contribuições são bem-vindas!** Veja [CONTRIBUTING.md](CONTRIBUTING.md) para detalhes.

---

**Made with ❤️ by [Daniel Iel](https://github.com/Daniel-iel)**

</div>
