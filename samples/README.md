# Dialect Samples - Comprehensive Framework Examples

A comprehensive console application demonstrating all features of the Dialect SQL FluentBuilder Framework with support for SQL Server, PostgreSQL, and MySQL.

## Overview

This project provides practical examples of using the Dialect framework to build type-safe, database-agnostic SQL queries. Examples progress from basic CRUD operations to advanced query optimization and schema validation.

## Prerequisites

### Required

- .NET 8.0 or later
- Docker Desktop (for database connectivity)
- Windows, macOS, or Linux

### Optional

- Visual Studio Code or Visual Studio 2022+
- Docker Compose CLI (included with Docker Desktop)

## Quick Start

### 1. Clone and Navigate to Project

```bash
cd samples/Dialect.Samples
```

### 2. Start Docker Containers

From the project root directory:

```bash
docker-compose up -d
```

Wait for health checks to pass (30-50 seconds):

```bash
docker-compose ps
```

All three services should show `healthy` status.

### 3. Run Examples

```bash
dotnet run --framework net8.0
```

This launches an interactive menu where you can:
- Select examples by category (Basic, Intermediate, Advanced)
- View compiled SQL for all 3 dialects
- Learn about database optimization, indexing, and schema validation

## Project Structure

```
Dialect.Samples/
├── Program.cs                    # Interactive menu system
├── Dialect.Samples.csproj        # Project configuration
├── 01_Basic/                     # Fundamental operations
│   ├── SelectExamples.cs         # SELECT queries (4 examples)
│   ├── InsertExamples.cs         # INSERT operations (3 examples)
│   ├── UpdateExamples.cs         # UPDATE operations (3 examples)
│   ├── DeleteExamples.cs         # DELETE operations (3 examples)
│   └── UpsertExamples.cs         # UPSERT patterns (3 dialect-specific)
├── 02_Intermediate/              # Complex query patterns
│   ├── JoinExamples.cs           # JOIN operations (3 examples)
│   ├── CteExamples.cs            # Common Table Expressions (2 examples)
│   ├── WindowFunctionExamples.cs # Analytical functions (3 examples)
│   ├── GroupingExamples.cs       # Aggregation patterns (3 examples)
│   └── SubqueryExamples.cs       # Subqueries and derived tables (10 examples)
├── 03_Advanced/                  # Performance & analysis
│   ├── OptimizationExamples.cs   # Query analysis (3 examples)
│   ├── IndexAdvisorExamples.cs   # Index recommendations (3 examples)
│   └── SchemaValidationExamples.cs # Schema validation (3 examples)
├── 04_ErrorHandling/             # Error scenarios & debugging
├── 06_AdvancedQueries/           # Batch operations & set operations
│   └── BatchOperationsExamples.cs # INSERT/UPDATE/DELETE + UNION/INTERSECT/EXCEPT (15 examples)
├── Utilities/                    # Shared utilities
│   ├── ExampleBase.cs            # Abstract base for all examples
│   ├── DialectHelper.cs          # Dialect instance management
│   ├── OutputFormatter.cs        # Console formatting with colors
│   └── DatabaseConnections.cs    # Connection string management
└── docker/                       # Database initialization (from project root)
    ├── docker-compose.yml        # Container orchestration
    ├── sql-server/init.sql       # SQL Server schema
    ├── postgresql/init.sql       # PostgreSQL schema
    └── mysql/init.sql            # MySQL schema
```

## Example Categories

### Basic Examples (5 files, 21 examples)

**SelectExamples.cs** - SELECT fundamentals
- Simple SELECT with column projection
- WHERE clause filtering
- ORDER BY sorting
- SKIP/TAKE pagination
- **WhereExpression Examples** (15 new examples):
  - Comparison operators: `>`, `<`, `<=`, `>=`, `<>`
  - AND conditions combining multiple predicates
  - OR conditions for alternative matching
  - IN clauses filtering against value lists
  - NOT IN clauses excluding values
  - Nested AND/OR conditions for complex logic
  - LIKE pattern matching and text search
  - NOT LIKE for pattern exclusion
  - IS NULL and IS NOT NULL checks
  - BETWEEN for range filtering (dates and numbers)
  - Multi-level nested AND/OR combinations

**InsertExamples.cs** - Data insertion
- Single row insert
- Multiple row insert
- Parameterized insert

**UpdateExamples.cs** - Data modification
- Simple UPDATE statement
- UPDATE with WHERE clause
- UPDATE multiple columns

**DeleteExamples.cs** - Data removal
- DELETE with simple condition
- DELETE with parameters
- DELETE with multiple conditions

**UpsertExamples.cs** - Dialect-specific UPSERT
- SQL Server: MERGE statement
- PostgreSQL: ON CONFLICT DO UPDATE
- MySQL: ON DUPLICATE KEY UPDATE

### Intermediate Examples (4 files, 11 examples)

**JoinExamples.cs** - JOIN operations
- INNER JOIN (single join)
- LEFT JOIN (null handling)
- Multiple JOINs (complex composition)

**CteExamples.cs** - Common Table Expressions
- Single CTE with aggregation
- Multiple CTEs with dependencies

**WindowFunctionExamples.cs** - Analytical queries
- ROW_NUMBER() with partition
- RANK() with ordering
- LAG/LEAD for row comparisons

**GroupingExamples.cs** - Aggregation patterns
- GROUP BY with COUNT
- GROUP BY with HAVING filter
- Multiple aggregate functions (COUNT, SUM, AVG)

**SubqueryExamples.cs** - Subqueries and derived tables (10 examples)

**FROM Subqueries** (4 examples):
- Basic derived table with aggregation
- Filtered derived table with WHERE in subquery
- Nested aggregations (subquery aggregates, outer query processes aggregates)
- Complex aggregation with HAVING clause

**WHERE IN Subqueries** (5 examples):
- Basic IN with subquery
- NOT IN for exclusion patterns
- Aggregation in subquery with HAVING
- Chained conditions combining IN subquery with WHERE
- Multiple conditions using AND logic

**Bonus Example**:
- Using SelectBuilder directly without calling Build()

### Advanced Examples (3 files, 9 examples)

**OptimizationExamples.cs** - Query analysis
- Query parsing and structure analysis
- Predicate analysis (WHERE clause optimization)
- JOIN depth analysis (complexity assessment)

**IndexAdvisorExamples.cs** - Performance tuning
- Single-column indexes for WHERE clauses
- Foreign key indexes for JOINs
- Covering indexes (index-only scans)

**SchemaValidationExamples.cs** - Data integrity
- Naming convention validation
- Data type best practices
- Constraint and referential integrity

### Batch Operations & Set Operations (1 file, 15 examples)

**BatchOperationsExamples.cs** - Bulk operations and set combinations

**Batch Operations** (7 examples):
- INSERT single record
- INSERT multiple records
- UPDATE with WHERE clause
- UPDATE multiple columns
- DELETE safe with WHERE clause
- Soft delete pattern using UPDATE

**Set Operations** (8 examples):
- UNION - Combine results removing duplicates
- UNION ALL - Combine all rows including duplicates
- INTERSECT - Find common rows in two result sets
- EXCEPT - Find rows in left set not in right set
- UNION with WHERE clauses - Filter before combining
- UNION ALL with pagination
- INTERSECT - Multi-condition matching (e.g., premium members who purchased)
- EXCEPT - Exclusion queries (e.g., products never ordered)

## Database Schema

All three databases contain identical schemas for consistent example behavior:

### Users Table
```sql
CREATE TABLE Users (
    UserId INT PRIMARY KEY,
    Username VARCHAR(100) NOT NULL,
    Email VARCHAR(100) NOT NULL UNIQUE,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

### Products Table
```sql
CREATE TABLE Products (
    ProductId INT PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    Price DECIMAL(10, 2) NOT NULL,
    StockQuantity INT DEFAULT 0
);
```

### Orders Table
```sql
CREATE TABLE Orders (
    OrderId INT PRIMARY KEY,
    UserId INT NOT NULL,
    OrderDate TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    Total DECIMAL(10, 2) NOT NULL,
    FOREIGN KEY (UserId) REFERENCES Users(UserId)
);
```

### OrderItems Table
```sql
CREATE TABLE OrderItems (
    OrderItemId INT PRIMARY KEY,
    OrderId INT NOT NULL,
    ProductId INT NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(10, 2) NOT NULL,
    FOREIGN KEY (OrderId) REFERENCES Orders(OrderId),
    FOREIGN KEY (ProductId) REFERENCES Products(ProductId)
);
```

## Key Features

### Multi-Dialect Support

Every example compiles to all three dialects and displays side-by-side:

```
📊 SQL Server (Blue)
  SELECT o.OrderId, u.Username, o.Total
  FROM Orders o
  INNER JOIN Users u ON o.UserId = u.UserId

📊 PostgreSQL (Green)
  SELECT o.order_id, u.username, o.total
  FROM orders o
  INNER JOIN users u ON o.user_id = u.user_id

📊 MySQL (Orange)
  SELECT o.order_id, u.username, o.total
  FROM orders o
  INNER JOIN users u ON o.user_id = u.user_id
```

### ExampleBase Pattern

All examples inherit from `ExampleBase` which provides:

- `abstract void Run()` - Override to implement example logic
- `CompileForAllDialects()` - Compile query for all 3 dialects in single call
- `PrintResults()` - Format and display results with color coding
- `PrintResult()` - Display single dialect result

### Color-Coded Output

- 🔵 **SQL Server** - Blue (#33)
- 🟢 **PostgreSQL** - Green (#154)
- 🟠 **MySQL** - Orange (#208)

Colors help visually distinguish dialect-specific output and SQL syntax variations.

## Running Examples

### Interactive Menu

```bash
dotnet run --framework net8.0
```

**Menu Options:**
```
📚 BASIC EXAMPLES
  1. SELECT
  2. INSERT
  3. UPDATE
  4. DELETE
  5. UPSERT

🔄 INTERMEDIATE EXAMPLES
  6. JOINs
  7. CTEs
  8. Window Functions
  9. Grouping & Aggregation

⚙️  ADVANCED EXAMPLES
  10. Query Optimization
  11. Index Advisor
  12. Schema Validation

💡 UTILITIES
  99. Docker Setup Guide
  0. Exit
```

### Run Specific Example

Modify `Program.cs` to execute a single example:

```csharp
static void Main(string[] args)
{
    var example = new SelectExamples();
    example.Run();
}
```

### Run All Examples

Option 1: Edit `Program.cs` to loop through all examples:

```csharp
var examples = new ExampleBase[]
{
    new SelectExamples(),
    new InsertExamples(),
    // ... etc
};

foreach (var example in examples)
{
    example.Run();
    Console.WriteLine("\n" + new string('-', 80) + "\n");
}
```

Option 2: Execute menu and manually select each option (best for learning)

## Docker Management

### View Container Status

```bash
docker-compose ps
```

Expected output:
```
NAME                COMMAND                STATUS         PORTS
dialect-sqlserver   "/opt/mssql/bin/..."   Up (healthy)   0.0.0.0:1433->1433/tcp
dialect-postgresql  "docker-entrypoint..."  Up (healthy)   0.0.0.0:5432->5432/tcp
dialect-mysql       "docker-entrypoint..."  Up (healthy)   0.0.0.0:3306->3306/tcp
```

### View Container Logs

```bash
docker-compose logs [service-name]
```

### Stop Containers (Keep Data)

```bash
docker-compose stop
```

### Start Containers

```bash
docker-compose start
```

### Reset Databases

```bash
docker-compose down -v
docker-compose up -d
```

### Connect to Specific Database

**SQL Server:**
```bash
docker exec -it dialect-sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "P@ssw0rd!"
```

**PostgreSQL:**
```bash
docker exec -it dialect-postgresql psql -U postgres -d dialect_samples
```

**MySQL:**
```bash
docker exec -it dialect-mysql mysql -uroot -proot dialect_samples
```

## Extension Points

### Adding New Examples

1. Create file in appropriate category folder (01_Basic, 02_Intermediate, 03_Advanced)
2. Inherit from `ExampleBase`:

```csharp
public class MyExample : ExampleBase
{
    public MyExample() : base("Example Title", "Description")
    {
    }

    public override void Run()
    {
        OutputFormatter.PrintSubHeader("Example 1: ...");
        
        var results = CompileForAllDialects(dialect =>
            SqlBuilder.Select()
                // ... build query
                .Compile(dialect)
        );

        PrintResults(results);
    }
}
```

3. Add to Program.cs menu system
4. Run via interactive menu or direct instantiation

### Custom Database Connections

Modify `DatabaseConnections.cs` to add new databases or change connection parameters.

### Custom Output Formatting

Extend `OutputFormatter.cs` to add new display patterns (tables, JSON, CSV, etc.)

## Learning Path

**Recommended progression:**

1. **Start with Basic (15 min)**
   - SelectExamples - Learn SELECT fundamentals
   - InsertExamples - Understand INSERT operations
   - UpdateExamples - Master UPDATE patterns
   - DeleteExamples - Complete CRUD cycle
   - UpsertExamples - Compare dialect differences

2. **Progress to Intermediate (20 min)**
   - JoinExamples - Multi-table queries
   - CteExamples - Query composition
   - WindowFunctionExamples - Analytical queries
   - GroupingExamples - Aggregation patterns

3. **Explore Advanced (15 min)**
   - OptimizationExamples - Understand query analysis
   - IndexAdvisorExamples - Learn performance tuning
   - SchemaValidationExamples - Best practices

4. **Extend Learning**
   - Modify examples to use your own database
   - Create custom examples for your use cases
   - Integrate into your application

## Troubleshooting

### "Connection refused" Error

**Problem:** Examples can't connect to databases

**Solutions:**
1. Verify Docker containers are running: `docker-compose ps`
2. Wait for health checks (30-50 seconds): containers should show `healthy`
3. Check connection strings in `DatabaseConnections.cs` match docker-compose ports
4. View logs: `docker-compose logs`

### Database Schema Not Found

**Problem:** Tables don't exist in database

**Solutions:**
1. Verify initialization scripts exist in `/docker` folder
2. Check logs for SQL errors: `docker-compose logs [service-name]`
3. Reset database: `docker-compose down -v && docker-compose up -d`

### Build Errors

**Problem:** `dotnet build` fails with compilation errors

**Solutions:**
1. Ensure .NET 8.0+ installed: `dotnet --version`
2. Restore packages: `dotnet restore`
3. Check example classes inherit from ExampleBase
4. Verify all using statements are present

### Examples Show Same SQL for All Dialects

**Problem:** SQL output doesn't vary by dialect (e.g., no PostgreSQL snake_case)

**Note:** This is expected behavior. The Dialect framework currently shows how the base builder compiles. Full dialect-specific rendering (naming conventions, type mappings, etc.) is handled by the individual dialect implementations (Dialect.SqlServer, Dialect.PostgreSql, Dialect.MySql) when used in production.

## Dependencies

- **Dialect.Core** - Core SQL building framework
- **Dialect.SqlServer** - SQL Server dialect implementation
- **Dialect.PostgreSql** - PostgreSQL dialect implementation
- **Dialect.MySql** - MySQL dialect implementation
- **Microsoft.Data.SqlClient** 5.1.5 - SQL Server driver
- **Npgsql** 10.0.3 - PostgreSQL driver
- **MySqlConnector** 2.6.2 - MySQL driver
- **Microsoft.Extensions.DependencyInjection** 8.0.0 - DI framework

## Performance Notes

- Examples are optimized for clarity, not performance
- Production queries should use connection pooling
- Consider query complexity when adding examples
- Monitor database CPU/memory usage in docker: `docker stats`

## Contributing

To add new examples:

1. Create file in appropriate category folder
2. Inherit from `ExampleBase`
3. Implement `Run()` method
4. Use `CompileForAllDialects()` for consistency
5. Add menu option to Program.cs
6. Test with all 3 database targets

## Next Steps

- Explore `Dialect.Core` source code to understand query building
- Review SQL Server/PostgreSQL/MySQL dialect implementations
- Integrate examples into your application
- Create custom builders for domain-specific patterns

## Resources

- [Dialect Framework Documentation](../../../spec.md)
- [Docker Documentation](../docker/README.md)
- [Database Design Patterns](https://en.wikipedia.org/wiki/Relational_database)
- [SQL Optimization Basics](https://www.postgresql.org/docs/current/using-explain.html)

## License

Samples are provided as-is for educational purposes.
