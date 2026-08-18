# Phase 7 - Index Advisor & Query Rewrite Engine

## Overview
Build on Phase 6 optimization foundation by implementing advanced index recommendations and query rewrite suggestions. Extract index candidates from AST, generate dialect-specific DDL, and suggest query optimizations.

## Objectives
- Index Advisor: analyze queries for index opportunities
- Query Rewrite suggestions: CTE, window functions, join optimization
- Dialect-specific DDL generation
- Integration with Phase 6.4 Optimization Engine
- Target: 40+ tests across all subsystems

## Phase 7 Subsystems

### 1. Index Advisor (15 tests)
**Purpose**: Analyze queries to recommend indexes

**Components**:
- `IndexCandidate.cs` - Represents a suggested index with metadata
- `IndexAdvisor.cs` - Base class analyzing query AST for index opportunities
- `SqlServerIndexAdvisor.cs` - SQL Server index advisor with clustered/non-clustered strategies
- `PostgreSqlIndexAdvisor.cs` - PostgreSQL index advisor with partial/BRIN strategies
- `MySqlIndexAdvisor.cs` - MySQL index advisor with composite key strategies

**Test Coverage**:
- Identify WHERE clause filter columns
- Detect JOIN condition columns
- Recommend composite indexes
- Suggest covering indexes
- Generate CREATE INDEX DDL
- Filter redundant suggestions

### 2. Query Rewrite Engine (12 tests)
**Purpose**: Suggest query rewrites for optimization

**Components**:
- `QueryRewrite.cs` - Record for suggested query rewrite
- `QueryRewriteAdvisor.cs` - Base class for query rewrite analysis
- `SqlServerRewriteAdvisor.cs` - SQL Server-specific rewrites
- `PostgreSqlRewriteAdvisor.cs` - PostgreSQL-specific rewrites
- `MySqlRewriteAdvisor.cs` - MySQL-specific rewrites

**Test Coverage**:
- Suggest CTE for subquery elimination
- Recommend window functions
- Detect inefficient joins
- Suggest join order changes
- Recommend UNION optimization
- Detect N+1 patterns

### 3. DDL Generation (8 tests)
**Purpose**: Generate dialect-specific index creation statements

**Components**:
- `DdlGenerator.cs` - Base DDL generator
- `SqlServerDdlGenerator.cs` - SQL Server DDL (INCLUDE, FILLFACTOR, STATISTICS)
- `PostgreSqlDdlGenerator.cs` - PostgreSQL DDL (CONCURRENTLY, WHERE, INCLUDE)
- `MySqlDdlGenerator.cs` - MySQL DDL (USING BTREE/HASH, KEY LENGTH)

**Test Coverage**:
- Generate simple index DDL
- Generate composite index DDL
- Generate covering index DDL
- Generate partial index DDL
- Add IF NOT EXISTS clauses
- Handle reserved words

### 4. Index Metadata Analysis (5 tests)
**Purpose**: Track and deduplicate index recommendations

**Components**:
- `IndexMetadata.cs` - Index statistics and properties
- `IndexDeduplicator.cs` - Remove redundant suggestions
- `IndexPrioritizer.cs` - Rank suggestions by impact

**Test Coverage**:
- Deduplicate overlapping suggestions
- Calculate index impact score
- Estimate index storage cost
- Recommend index drop candidates
- Prioritize by ROI

## Implementation Strategy

### Phase 7.1: Index Advisor (Day 1)
1. Create IndexCandidate and IndexAdvisor base
2. Implement dialect-specific advisors (3)
3. Write 10 tests
4. Target: Identify 8+ index opportunity types

### Phase 7.2: Query Rewrite Engine (Day 1-2)
1. Create QueryRewrite and RewriteAdvisor
2. Implement dialect-specific rewriters (3)
3. Write 12 tests
4. Target: Generate 6+ rewrite suggestions

### Phase 7.3: DDL Generation (Day 2)
1. Implement DdlGenerator base
2. Create dialect generators (3)
3. Write 8 tests
4. Target: Generate correct DDL for all 3 dialects

### Phase 7.4: Integration (Day 2-3)
1. Integrate with Phase 6.4 recommendations
2. Create combined advisor
3. Write 10 integration tests
4. Target: End-to-end optimization workflow

## Testing Framework

**Test Structure** (40 total):
```
IndexAdvisor/
├── IndexAdvisorTests (10 tests)
├── SqlServerIndexAdvisorTests (5 tests)
├── PostgreSqlIndexAdvisorTests (5 tests)
└── MySqlIndexAdvisorTests (5 tests)

QueryRewrite/
├── QueryRewriteAdvisorTests (8 tests)
├── SqlServerRewriteAdvisorTests (4 tests)
├── PostgreSqlRewriteAdvisorTests (4 tests)
└── MySqlRewriteAdvisorTests (4 tests)

DdlGeneration/
├── DdlGeneratorTests (8 tests)
├── SqlServerDdlGeneratorTests (4 tests)
├── PostgreSqlDdlGeneratorTests (4 tests)
└── MySqlDdlGeneratorTests (4 tests)

Integration/
├── AdvancedOptimizationWorkflowTests (10 tests)
```

## Success Criteria
- 40+ tests passing across all frameworks
- Full index advisor for 3 dialects
- Query rewrite suggestions for 6+ patterns
- Correct DDL generation for all 3 dialects
- Integration with Phase 6.4 complete
- Zero breaking changes to previous phases

## Dependencies
- Phase 6.4: Optimization Engine & Recommendation structures
- AST structures from Phase 1-3 (SelectStatement, etc.)
- Core dialect infrastructure

## Database-Specific Considerations

### SQL Server
- Clustered vs non-clustered indexes
- INCLUDE columns for covering indexes
- STATISTICS management
- FILLFACTOR optimization

### PostgreSQL
- Partial indexes (WHERE clause)
- BRIN for sequential data
- Concurrent index creation
- Index-only scans

### MySQL
- Key length restrictions
- Composite key optimization
- USING BTREE/HASH
- Generated column indexes

## Phase 7 Outputs

1. **Index Recommendations**: List of IndexCandidate with DDL
2. **Query Rewrites**: Suggested SQL changes with before/after
3. **DDL Scripts**: Ready-to-execute CREATE INDEX statements
4. **Impact Analysis**: Estimated performance gain per recommendation
5. **Integration Report**: Combined optimization workflow results

---

## Next Steps After Phase 7

**Phase 8: Compiled Query Cache & Performance**
- Query shape caching
- Eviction policies
- Concurrency testing

**Phase 9: CLI Migration Tool**
- Roslyn-based SQL discovery
- Parser integration
- Code generation

**Phase 10: Advanced Security**
- SQL injection detection
- Analyzer (Roslyn)
- Audit hooks
