# Phase 6 - Advanced Optimization Engine

## Overview
Build on Phase 5 foundation by implementing full query analysis, real database detection, and performance optimization recommendations.

## Objectives
- Implement full Index Advisor with query AST parsing
- Real database version detection via SQL queries
- Query performance analysis and recommendations
- Cache management for optimization metadata
- Target: 200+ tests across optimization subsystems

## Phase 6 Subsystems

### 1. Query Parser & Analyzer (60 tests)
**Purpose**: Parse SQL queries to extract optimization opportunities

**Components**:
- `QueryParser.cs` - Extract WHERE, JOIN, ORDER BY, GROUP BY clauses
- `PredicateAnalyzer.cs` - Analyze filter predicates for selectivity
- `JoinAnalyzer.cs` - Analyze join relationships and depth
- `AggregationAnalyzer.cs` - Analyze GROUP BY and aggregations
- `IndexCandidateExtractor.cs` - Extract index recommendations from analysis

**Test Coverage**:
- Parse WHERE clauses with AND/OR logic
- Extract column usage patterns
- Calculate selectivity estimates
- Detect N+1 query patterns
- Identify missing indexes
- Suggest covering indexes

### 2. Database Connection & Version Detection (50 tests)
**Purpose**: Connect to databases and detect real versions/capabilities

**Components**:
- `IDbConnectionProvider.cs` - Interface for DB connections
- `SqlServerConnectionProvider.cs` - T-SQL connection handler
- `PostgreSqlConnectionProvider.cs` - ANSI SQL connection handler
- `MySqlConnectionProvider.cs` - MySQL connection handler
- `RealVersionDetector.cs` - Executes version queries

**Test Coverage**:
- Connection pooling
- Version string parsing
- Feature availability detection
- Connection error handling
- Multi-database support

### 3. Query Performance Profiler (50 tests)
**Purpose**: Analyze real query execution plans

**Components**:
- `QueryExecutionPlan.cs` - Represents query plan metadata
- `ExecutionPlanAnalyzer.cs` - Base class for plan analysis
- `SqlServerPlanAnalyzer.cs` - SQL Server EXPLAIN/SET STATISTICS
- `PostgreSqlPlanAnalyzer.cs` - PostgreSQL EXPLAIN ANALYZE
- `MySqlPlanAnalyzer.cs` - MySQL EXPLAIN output
- `PerformanceMetrics.cs` - Execution stats (rows scanned, cost, time)

**Test Coverage**:
- Parse execution plans
- Calculate query cost
- Identify table scans vs index seeks
- Recommend covering indexes
- Detect suboptimal joins

### 4. Optimization Recommendation Engine (40 tests)
**Purpose**: Synthesize analysis into actionable recommendations

**Components**:
- `OptimizationRecommendation.cs` - Optimization advice record
- `OptimizationEngine.cs` - Base orchestrator
- `DialectSpecificOptimizer.cs` - Dialect-specific implementation
- `RecommendationPrioritizer.cs` - Rank recommendations by ROI

**Test Coverage**:
- Index recommendations with ROI
- Query rewrites (CTEs, window functions)
- Join order optimization
- Materialized view suggestions
- Partitioning recommendations

## Implementation Strategy

### Phase 6.1: Query Parser (Day 1-2)
1. Create QueryParser with clause extraction
2. Implement PredicateAnalyzer
3. Create JoinAnalyzer
4. Write 60 comprehensive tests
5. Target: Fully parse 10+ query patterns

### Phase 6.2: Version Detection (Day 2-3)
1. Create connection providers
2. Implement real version detection
3. Test against 3 database types
4. Write 50 tests
5. Target: Auto-detect 5+ major features

### Phase 6.3: Execution Plan Analysis (Day 3-4)
1. Create execution plan parsers
2. Implement per-dialect analyzers
3. Calculate performance metrics
4. Write 50 tests
5. Target: Analyze 8+ plan types

### Phase 6.4: Optimization Engine (Day 4-5)
1. Synthesize all analyzers
2. Create recommendation engine
3. Prioritize by ROI
4. Write 40 tests
5. Target: Generate 15+ recommendation types

## Testing Framework

**Test Structure** (200 total):
```
QueryAnalysis/
├── QueryParserTests (20 tests)
├── PredicateAnalyzerTests (15 tests)
├── JoinAnalyzerTests (15 tests)
└── AggregationAnalyzerTests (10 tests)

VersionDetection/
├── ConnectionProviderTests (20 tests)
├── RealVersionDetectorTests (20 tests)
└── CapabilityDetectionTests (10 tests)

PerformanceProfiling/
├── ExecutionPlanTests (20 tests)
├── SqlServerPlanAnalyzerTests (15 tests)
├── PostgreSqlPlanAnalyzerTests (15 tests)
└── MySqlPlanAnalyzerTests (10 tests)

OptimizationEngine/
├── OptimizationRecommendationTests (15 tests)
├── IndexRecommendationTests (15 tests)
├── QueryRewriteTests (10 tests)
└── IntegrationTests (20 tests)
```

## Success Criteria
- 200+ tests passing across all frameworks
- Full query parsing for 10+ SQL patterns
- Real version detection working
- Execution plan analysis functional
- 15+ optimization recommendations types
- Zero breaking changes to Phase 1-5

## Dependencies
- Phase 5 core (Schema Validation, Index Advisor, Version Detection)
- System.Data.* for database connections
- Query parsing libraries or custom regex/state machines

## Risk Mitigation
- MVP database connections (no actual DB required for tests)
- Mock execution plans for plan analysis tests
- Fallback to estimated costs if real plans unavailable
- Graceful degradation if version detection fails

## Timeline
- Phase 6.1-6.2: ~24 hours
- Phase 6.3-6.4: ~24 hours
- Total: 2-3 days of development
- Target: 288+ tests passing (exceed Phase 5 count)

---

**Ready to implement Phase 6.1: Query Parser** ✅
