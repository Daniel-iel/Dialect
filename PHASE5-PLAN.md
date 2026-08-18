# Phase 5: Schema Validation, Index Advisor & Version Negotiation

## Overview
Phase 5 extends the migration framework with intelligent schema analysis and database-version-aware SQL generation. It introduces three independent but complementary subsystems:

1. **Schema Validation** — Validate migration definitions and verify schema integrity
2. **Index Advisor** — Analyze compiled queries and recommend indexes
3. **Version Negotiation** — Detect target DB version and adapt SQL generation accordingly

---

## 1. Schema Validation Module

### Purpose
Catch schema errors early — before migrations are executed — by validating column definitions, constraints, and naming conventions against configurable rules and target database capabilities.

### Architecture

#### `src/Dialect.Core/Schema/SchemaValidator.cs` (Abstract)
```csharp
public abstract class SchemaValidator
{
    public abstract ValidationResult Validate(Migration migration, ISqlDialect dialect);
    public abstract ValidationResult ValidateColumn(ColumnDef column, ISqlDialect dialect);
    public abstract ValidationResult ValidateTable(string tableName, ColumnDef[] columns);
}
```

#### `src/Dialect.Core/Schema/ValidationRules.cs` (Rules Engine)
Rules to implement:
- **NameConvention**: Table/column naming must follow patterns (e.g., PascalCase, snake_case, mixed)
- **ColumnConstraint**: Primary keys, NOT NULL columns, auto-increment rules
- **DataTypeValid**: DataType exists in target dialect
- **LengthRequired**: VARCHAR/CHAR must have length; DECIMAL must have precision
- **UniqueConstraints**: Primary key column uniqueness
- **ForeignKeyRef**: Referenced table/column must exist in migration context
- **DefaultValue**: DEFAULT values must be type-compatible
- **IdentifierLength**: Identifiers must not exceed dialect's max length (SQL Server: 128, PostgreSQL: 63, MySQL: 64)

#### `src/Dialect.Core/Schema/ValidationResult.cs`
```csharp
public record ValidationResult(
    bool IsValid,
    IReadOnlyList<ValidationError> Errors,
    IReadOnlyList<ValidationWarning> Warnings
);

public record ValidationError(string RuleId, string Message, string AffectedEntity);
public record ValidationWarning(string RuleId, string Message, string AffectedEntity);
```

#### Dialect-Specific Validators (New)
- `src/Dialect.SqlServer/Schema/SqlServerSchemaValidator.cs`
- `src/Dialect.PostgreSql/Schema/PostgreSqlSchemaValidator.cs`
- `src/Dialect.MySql/Schema/MySqlSchemaValidator.cs`

Each validates against dialect-specific constraints:
- **SQL Server**: IDENTITY syntax, reserved keywords, max 128-char identifiers
- **PostgreSQL**: SERIAL/BIGSERIAL, implicit indexes on PKs, max 63-char identifiers
- **MySQL**: AUTO_INCREMENT, FULLTEXT indexes, max 64-char identifiers

---

## 2. Index Advisor Engine

### Purpose
Analyze compiled SELECT queries to recommend indexes that would improve performance.

### Architecture

#### `src/Dialect.Core/Indexing/IndexAdvisor.cs` (Abstract)
```csharp
public abstract class IndexAdvisor
{
    public abstract IndexRecommendations Analyze(
        CompiledQuery query, 
        QueryModel model,  // SelectStatement, etc.
        ISqlDialect dialect
    );
}
```

#### `src/Dialect.Core/Indexing/IndexRecommendation.cs`
```csharp
public record IndexRecommendation(
    string TableName,
    string[] ColumnNames,
    IndexType Type,          // SingleColumn, Composite, Covering, FullText, Filtered
    decimal EstimatedBenefit, // 0.0 to 1.0, impact on query cost
    string Reasoning,
    IndexPriority Priority    // Critical, High, Medium, Low
);

public enum IndexType { SingleColumn, Composite, Covering, FullText, Filtered }
public enum IndexPriority { Critical, High, Medium, Low }
```

#### Analysis Rules
The advisor examines:
- **WHERE clauses**: Columns in predicates are candidates for single-column or composite indexes
- **JOIN conditions**: Join columns benefit from indexes
- **ORDER BY**: Sorting on indexed columns can eliminate sort operations
- **GROUP BY**: Grouping columns benefit from indexes
- **Covering indexes**: SELECT list + WHERE can be combined into covering index
- **Filtered indexes**: WHERE predicates with static conditions (e.g., `status = 'active'`)

#### Scoring Algorithm
```
score(column) = (selectivity × 0.5) + (query_frequency × 0.3) + (join_depth × 0.2)
  where:
    selectivity = distinct_values / total_rows  (higher = better for indexing)
    query_frequency = estimated queries per hour using this predicate
    join_depth = nesting level in join tree
```

#### Dialect-Specific Advisors
- `src/Dialect.SqlServer/Indexing/SqlServerIndexAdvisor.cs`
  - Understands INCLUDE (covering) syntax
  - Detects filtered index opportunities
  - Suggests clustered index for PK
  
- `src/Dialect.PostgreSql/Indexing/PostgreSqlIndexAdvisor.cs`
  - Handles partial indexes (filtered)
  - BRIN indexes for large sequential data
  - Expression indexes for function-based queries
  
- `src/Dialect.MySql/Indexing/MySqlIndexAdvisor.cs`
  - FULLTEXT index recommendations
  - HASH vs BTREE trade-offs
  - Composite index ordering (DESC handling in MySQL 8+)

---

## 3. Version Negotiation Protocol

### Purpose
Adapt SQL generation based on detected or configured target database version, enabling forward-compatible query generation.

### Architecture

#### `src/Dialect.Core/Versioning/DatabaseVersion.cs`
```csharp
public record DatabaseVersion(
    SqlProvider Provider,      // SqlServer, PostgreSQL, MySQL
    int MajorVersion,          // 2019, 2022, 13, 14, 15, 8, 9
    int MinorVersion,          // 0 for most
    string FullVersion         // "SQL Server 2022 (16.0.1000.6)"
);
```

#### `src/Dialect.Core/Versioning/VersionCapabilities.cs`
Maps version → supported features:
```csharp
public abstract record VersionCapabilities(
    DatabaseVersion Version,
    SqlFeature[] SupportedFeatures,
    SqlDataType[] SupportedDataTypes,
    SqlFunction[] SupportedFunctions,
    int MaxIdentifierLength,
    int MaxParameterCount,
    bool SupportsCommonTableExpressions,
    bool SupportsWindowFunctions,
    bool SupportsJson,
    bool SupportsUpsert,
    bool SupportsGeneratedColumns
);
```

#### Feature Negotiation Rules
Each dialect-specific `VersionCapabilities` implementation defines:

**SQL Server:**
- 2019: CTE, Window Functions, UPSERT (MERGE), Temporal Tables
- 2022: + JSON Path functions, STRING_AGG enhancements

**PostgreSQL:**
- 13: CTE, Window Functions, JSON operators, UPSERT (ON CONFLICT)
- 14: + Generated Columns, JSON type hints
- 15: + MERGE statement, UNIQUE NULLS NOT DISTINCT

**MySQL:**
- 8.0: CTE, Window Functions, JSON functions, ON DUPLICATE KEY UPDATE
- 8.0.20+: Derived table merging optimization hints
- 9.0: WINDOW clause syntax refinements

#### `src/Dialect.Core/Versioning/VersionAwareRenderer.cs`
Wraps `IQueryRenderer` with version awareness:
```csharp
public abstract class VersionAwareRenderer
{
    public CompiledQuery Render(
        SelectStatement statement,
        ISqlDialect dialect,
        DatabaseVersion targetVersion
    )
    {
        var capabilities = dialect.GetVersionCapabilities(targetVersion);
        
        if (statement.UsesWindowFunctions && !capabilities.SupportsWindowFunctions)
            throw new VersionNotSupportedException(...);
            
        return RenderInternal(statement, dialect, capabilities);
    }
    
    protected abstract CompiledQuery RenderInternal(
        SelectStatement statement,
        ISqlDialect dialect,
        VersionCapabilities capabilities
    );
}
```

#### Version Detection Service
`src/Dialect.Core/Versioning/VersionDetector.cs`:
```csharp
public abstract class VersionDetector
{
    // Would call SELECT @@version (SQL Server), version() (PostgreSQL), SELECT VERSION() (MySQL)
    // Returns DatabaseVersion with major/minor parsed from output
    public abstract Task<DatabaseVersion> DetectAsync(
        string connectionString,
        ISqlDialect dialect,
        CancellationToken cancellationToken = default
    );
}
```

---

## 4. Integration Points

### MigrationBuilder + SchemaValidator
```csharp
var migration = new MigrationBuilder("001_CreateUsers", "1.0.0")
    .CreateTable("Users")
        .Column("id").AsInt().WithIdentity().AsPrimaryKey()
        .Column("email").AsString(255).NotNull()
        .Column("created_at").AsDateTime().WithDefault("GETUTCDATE()")
    .WithTable(tableBuilder)
    .Build();

var validator = dialect.CreateSchemaValidator();
var result = validator.Validate(migration, dialect);

if (!result.IsValid)
    foreach (var error in result.Errors)
        Console.WriteLine($"ERROR: {error.RuleId} - {error.Message}");
```

### QueryBuilder + IndexAdvisor
```csharp
var query = SqlBuilder.Select("Orders")
    .Columns("id", "customer_id", "total")
    .Where("status", "=", "completed")
    .Where("created_date", ">", "@minDate")
    .OrderBy("created_date DESC")
    .Build();

var advisor = dialect.CreateIndexAdvisor();
var recommendations = advisor.Analyze(query, query.Model, dialect);

foreach (var rec in recommendations.OrderByDescending(r => r.EstimatedBenefit))
    Console.WriteLine($"{rec.Priority}: Create {rec.Type} on {rec.TableName} ({string.Join(", ", rec.ColumnNames)})");
```

### VersionAwareRenderer
```csharp
var detectionService = dialect.CreateVersionDetector();
var targetVersion = await detectionService.DetectAsync(connectionString, dialect);

var renderer = dialect.CreateVersionAwareRenderer();
var compiled = renderer.Render(statement, dialect, targetVersion);
// SQL adapts to detected version (e.g., avoids JSON_QUERY if version < 2022)
```

---

## 5. Test Coverage

### SchemaValidator Tests (36 tests × 3 dialects = 108 tests)
- `SchemaValidatorTests.cs` in each dialect folder
- Test coverage:
  - Valid columns/tables (positive cases)
  - Naming convention violations
  - Data type mismatches per dialect
  - Identifier length exceeded
  - FK references to non-existent tables
  - Auto-increment on nullable columns (error)
  - Default values with wrong types

### IndexAdvisor Tests (30 tests × 3 dialects = 90 tests)
- `IndexAdvisorTests.cs` in each dialect folder
- Test coverage:
  - Single-column index recommendations from WHERE
  - Composite index recommendations from multi-column WHERE
  - Covering index detection (SELECT + WHERE)
  - No recommendation for small tables
  - JOIN column index prioritization
  - ORDER BY index benefit scoring

### VersionNegotiation Tests (24 tests × 3 dialects = 72 tests)
- `VersionNegotiationTests.cs`
- Test coverage:
  - Feature availability per version
  - Version-specific error handling (e.g., JSON on SQL Server 2019)
  - Fallback rendering for unsupported features
  - Version detection parsing (@@version format variations)

### Integration Tests (18 tests)
- `Phase5IntegrationTests.cs`
- End-to-end workflows combining all three subsystems

**Total Phase 5 Tests: 288 tests (36 + 30 + 24 + 18 = 108 + 90 + 72 + 18)**

---

## 6. Implementation Timeline (Estimate)

1. **Schema Validation** (3-4 hours)
   - Core validation rules engine
   - Dialect-specific validators (3 implementations)
   - Validation tests (108 total)

2. **Index Advisor** (4-5 hours)
   - Query analysis logic
   - Scoring algorithm
   - Dialect-specific advisors (3 implementations)
   - Advisor tests (90 total)

3. **Version Negotiation** (3-4 hours)
   - DatabaseVersion record and VersionCapabilities
   - Version-aware renderers (3 implementations)
   - Version detection services (3 implementations)
   - Version tests (72 total)

4. **Integration & Documentation** (2-3 hours)
   - End-to-end integration tests (18 tests)
   - README updates
   - API documentation

**Total Estimated: 12-16 hours of focused development**

---

## 7. Success Criteria

✅ All 288 Phase 5 tests passing across 3 frameworks (net8.0, net9.0, net10.0)  
✅ Zero breaking changes to Phase 1-4 APIs  
✅ Index recommendations generated in < 50ms for typical queries  
✅ Schema validation complete in < 100ms for typical migrations  
✅ Version detection working for SQL Server 2019/2022, PostgreSQL 13-16, MySQL 8.0-9.0  
✅ 100% test coverage for new classes

---

## 8. Next Steps

**Immediate (Phase 5 Start):**
1. Create base classes and interfaces in `src/Dialect.Core`
2. Implement schema validation rules engine
3. Implement index advisor scoring algorithm
4. Implement version capabilities mapping
5. Create dialect-specific implementations (3 × each)
6. Build comprehensive test suite

**After Phase 5 Complete:**
- Phase 6: CLI Tool (migrate, validate, advise commands)
- Phase 7: Performance profiling & query optimization hints
- Phase 8: Documentation & example projects
