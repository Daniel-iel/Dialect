# Dialect v3.0 - Especificação: SQL Translation CLI

**Data**: 2026-09-09  
**Status**: FINAL  
**Versão**: 3.0  

---

## 📋 Visão Geral

Adicionar capacidade ao **Dialect CLI** de:
1. **Detectar** strings SQL em arquivos C# via keyword-based regex
2. **Parsear** SQL em AST agnóstico de dialecto
3. **Transformar** AST entre dialetos (SQL Server ↔ PostgreSQL ↔ MySQL)
4. **Reescrever** arquivos C# com SQL transformado, preservando formatting
5. **Gerar relatório** detalhado de transformações

**Use Case Principal**: Migração de codebase C# entre dialetos de banco de dados sem refatorar para FluentBuilder.

**Exemplo**:
```bash
dialect translate-files --path ./src --source sqlserver --target postgresql \
  --output ./src_pg --include-backups --report migrate.json
```

**Resultado**: Codebase com SQL Server SQL convertida para PostgreSQL SQL, backups preservados, relatório com status.

---

## 🎯 Requisitos Funcionais

### RF1: Detecção de SQL em Arquivos C#

**Escopo**: Varrer diretório recursivamente, encontrar strings SQL inline

**Patterns Suportados**:
- ✅ Verbatim strings: `@"SELECT * FROM Users"`
- ✅ Normal strings: `"SELECT * FROM Users"`
- ✅ Method calls: `.Execute("SELECT ...")`
- ✅ Variable assignments: `var sql = "SELECT ..."`
- ✅ Multi-line verbatim: collapsar e transformar
- ⚠️ Concatenated strings: reconstituir antes de transformar
- ❌ String interpolation: detectar e marcar como manual review
- ❌ Runtime SQL: ignorar (não detectável)

**Detecção**: Keyword-based regex
- Procurar por: SELECT, FROM, WHERE, INSERT, UPDATE, DELETE, WITH, CREATE, ALTER, DROP, MERGE, EXEC, JOIN, GROUP BY, ORDER BY, HAVING
- Heurística: Deve ter pelo menos 1 keyword principal (SELECT/INSERT/UPDATE/DELETE) + 1 keyword secundário (FROM/WHERE/JOIN)
- Confidence score (0-100): Avaliar por tamanho, keywords encontradas, estrutura

**Output**: `List<SqlStringMatch>` com:
- LineNumber, ColumnStart, ColumnEnd
- OriginalSql (conteúdo extraído)
- StringType (Verbatim, Normal, MethodCall)
- FullMatch (texto completo com @"", etc.)
- VariableName (contexto)
- MethodName (contexto)
- Confidence (0-100)

---

### RF2: Parsing SQL para AST

**Escopo**: Converter SQL texto em Abstract Syntax Tree agnóstico

**Suportados**:
- ✅ SELECT com todas clauses (FROM, WHERE, JOIN, GROUP BY, HAVING, ORDER BY, DISTINCT, LIMIT/OFFSET)
- ✅ INSERT (simples e INSERT...SELECT)
- ✅ UPDATE (com SET, WHERE, FROM para SQL Server)
- ✅ DELETE (com WHERE)
- ✅ CTEs (WITH clauses)
- ✅ Window Functions (ROW_NUMBER, RANK, etc.)
- ✅ Subqueries (FROM, WHERE IN)
- ✅ Joins (INNER, LEFT, RIGHT, FULL, CROSS)
- ✅ UPSERT (MERGE, ON CONFLICT, ON DUPLICATE KEY)

**Entrada**: String SQL + SqlProvider source (auto-detect ou explicit)  
**Saída**: SelectStatement | InsertStatement | UpdateStatement | DeleteStatement | RoutineCall

**Dialects Suportados**:
- SQL Server (T-SQL): @parameters, [brackets], TOP/OFFSET FETCH, OUTPUT, MERGE
- PostgreSQL: $1 params, "quotes", LIMIT/OFFSET, RETURNING, ON CONFLICT
- MySQL: ? params, backticks, LIMIT/OFFSET, ON DUPLICATE KEY UPDATE

**Detecção Automática**: SqlDialectDetector via syntax analysis
- T-SQL patterns: @variables, [brackets], TOP, OFFSET...ROWS FETCH
- PostgreSQL patterns: $1 params, "quotes", RETURNING
- MySQL patterns: ? params, backticks, LIMIT/OFFSET com OFFSET sem TOP

**Parser Strategy**: Hybrid
- Regex + manual parsing para queries simples (80% casos)
- Fallback a null com erro claro se parsing falha
- Graceful degradation

---

## 🛡️ Segurança (Resumo de Implementação)

O módulo de validação de segurança foi adicionado ao CLI (Phase 7). Principais pontos:

- Path validation: `SecurityValidator.IsPathSafe(path, baseDirectory)` resolve caminhos completos e evita path traversal (`..`) comparando com o base path resolvido.
- Acesso: `IsDirectoryAccessible` e `IsFileAccessible` verificam existência e evitam diretórios/arquivos `Hidden` por padrão.
- Patterns seguros: `GetSafeGlobPatterns` filtra globs que contenham `..` ou caminhos absolutos.
- Sanitização de SQL para logging: `SanitizeSqlForLogging(sql)` mascara padrões sensíveis (`password`, `api_key`) e trunca SQL muito longos antes de incluir em relatórios/logs.
- Integração CLI: `TranslateFilesCommand.ValidateSecurityOptions` usa as rotinas acima para recusar entradas inseguras e para permitir diretórios absolutos legítimos quando acessíveis.

Testes abrangentes de unidade e integração foram adicionados em `tests/Dialect.Tests/Cli/` cobrindo `SecurityValidator` e o comportamento do CLI em cenários de input inseguro.

---

---

### RF3: Transformação Entre Dialetos (Bidirecional)

**Escopo**: Converter AST entre qualquer par de dialetos

**Suportado**: 9 combinações (3 x 3)
```
SQL Server  → PostgreSQL ✅
SQL Server  → MySQL ✅
PostgreSQL  → SQL Server ✅
PostgreSQL  → MySQL ✅
MySQL       → SQL Server ✅
MySQL       → PostgreSQL ✅
(+ self-transformations para validação)
```

**Função**: Compilar AST para SQL target com roteamento de features

**Mapping de Features**:
- Date functions: DATEADD → INTERVAL, DATE_ADD
- String functions: CONCAT, SUBSTRING (normalizados)
- Aggregates: GROUP_CONCAT → STRING_AGG
- UPSERT: MERGE → ON CONFLICT → ON DUPLICATE KEY UPDATE
- OUTPUT/RETURNING: Preservar ou avisar se não suportado
- Window Functions: ROW_NUMBER, RANK, etc. (universais)
- CTEs: WITH (universais, MySQL 8.0+)

**Suporte a Stored Procedures & Functions**:
- SQL Server: CREATE PROCEDURE, CREATE FUNCTION → PostgreSQL: CREATE FUNCTION, CREATE OR REPLACE FUNCTION → MySQL: CREATE PROCEDURE, CREATE FUNCTION
- Mapear parâmetros: @param → $1, $2 ou :param → ?
- Mapear tipos: INT → INTEGER, NVARCHAR → VARCHAR, etc.
- Mapear control flow: IF...ELSE → IF...THEN...END IF, BEGIN...END → BEGIN...END ou equivalentes
- Suportar: DDL (CREATE), DML (SELECT/INSERT/UPDATE/DELETE dentro de procedures), lógica procedural básica
- Limitar a: Procedures/Functions com SQL (sem CLR, nem lógica de negócio complexa)
- Avisos para: Dynamic SQL, cursores (limitado), tipos específicos dialecto (JSON, XML)

**Detecção de Não-Translatáveis**:
- MERGE sem destino → Avisar "reescrever manualmente"
- JSONB (PostgreSQL) → JSON com warning "mapeamento manual"
- FULLTEXT (MySQL) → CONTAINS (SQL Server) "sintaxe diferente"
- Recursive CTE + CYCLE → Avisar "CYCLE será perdido"
- CLR Procedures (SQL Server) → ❌ Impossível, marcar skip (não SQL puro)
- Cursores avançados com lógica complexa → ⚠️ Manual review
- String interpolation → ⚠️ Manual review

**Output**: SqlTransformationResult
```csharp
{
  Success: bool,
  OriginalSql: string,
  TransformedSql: string,
  Warnings: List<string>,
  Errors: List<string>
}
```

---

### RF4: Reescrita de Arquivos C#

**Escopo**: Substituir SQL strings transformadas em arquivo C# original

**Preserve**:
- ✅ Indentation (2 spaces, 4 spaces, tabs)
- ✅ String type (@"", "", backticks)
- ✅ Encoding (UTF-8, ASCII, etc.)
- ✅ Line endings (CRLF vs LF)
- ✅ Comments & blank lines
- ✅ Contexto do arquivo (variáveis, métodos)

**Rewriting Strategy**:
1. Parse arquivo com SqlStringDetector
2. Para cada match com transformação bem-sucedida:
   - Obter SQL transformado
   - Reenvolver com tipo string apropriado (@"", "", etc.)
   - Substituir no arquivo preservando posição

**Safety**:
- Validação pós-reescrita: Recompilar SQL transformado
- Backup antes de escrever (opcional --include-backups)
- Rollback se encoding/line-ending inválido
- Verificação de integridade: arquivo antes/depois (checksum, tamanho)

---

### RF5: Geração de Relatório (JSON, Markdown, HTML)

**Escopo**: Documentar transformações realizadas em múltiplos formatos

**Formatos Suportados**: 
1. **JSON** (primário): Estrutura de dados completa para integração
2. **Markdown** (opcional): Leitura em texto
3. **HTML** (novo): Dashboard interativo visual com Three.js

---

#### Formato JSON

**Conteúdo JSON**:
```json
{
  "summary": {
    "processingTime": "2.543s",
    "filesScanned": 15,
    "filesWithSql": 12,
    "filesModified": 10,
    "sqlsFound": 45,
    "transformationResults": {
      "successful": 40,
      "withWarnings": 3,
      "failed": 2
    }
  },
  "details": [
    {
      "file": "src/UserRepository.cs",
      "method": "GetUsers",
      "status": "Modified",
      "transformations": [
        {
          "line": 42,
          "confidence": 95,
          "original": "SELECT TOP 10 * FROM Users",
          "transformed": "SELECT * FROM Users LIMIT 10",
          "status": "Success"
        },
        {
          "line": 156,
          "method": "MergeOrderData",
          "original": "MERGE INTO Orders...",
          "error": "MERGE not supported in PostgreSQL — manual review required"
        }
      ]
    }
  ],
  "manualReviewItems": [
    {
      "file": "src/OrderService.cs",
      "method": "ProcessOrders",
      "line": 18,
      "issue": "Dynamic SQL (string interpolation) — cannot transform"
    }
  ]
}
```

---

#### Formato Markdown (--report-markdown)

Conteúdo:
- Header com summary e timeline
- Seção "✅ Successful Transformations" (tabulado com antes/depois)
- Seção "⚠️ Warnings" (requer revisão)
- Seção "❌ Errors" (manual review obrigatório)
- Seção "📝 Manual Review Items" (checklist com arquivo+método)

---

#### Formato HTML Interativo (--report-html) ⭐ **NOVO**

**Objetivo**: Dashboard visual, navegável, com efeitos e exploração 3D

**Opção CLI**:
```bash
--report-html <filepath>
```

**Tecnologias**:
- HTML5 + CSS3
- Three.js para visualização 3D interativa
- D3.js ou Chart.js para estatísticas
- Bootstrap 5 para responsividade
- Animações CSS para transições suaves
- Local storage para persistência de preferências

**Paleta de Cores** (extraída de `docs/documentation.html`):
- Primary: `#0066cc` (azul)
- Success: `#28a745` (verde)
- Warning: `#ffc107` (amarelo)
- Error: `#dc3545` (vermelho)
- Background: `#f8f9fa` (cinza claro)
- Text: `#212529` (cinza escuro)
- Border: `#dee2e6` (cinza médio)

**Layout do Relatório HTML**:

```
┌─────────────────────────────────────────────────────────────┐
│ 📊 Dialect SQL Translation Report                       [≡]  │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│ 🎯 DASHBOARD                                                │
│ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐          │
│ │ 📁 Files: 15 │ │ ✅ Success:40│ │ ⚠️ Warnings:3│          │
│ │ 🔍 SQL: 45   │ │ ❌ Failed: 2 │ │ ⏱️ 2.543s    │          │
│ └──────────────┘ └──────────────┘ └──────────────┘          │
│                                                               │
│ 📈 VISUALIZAÇÃO (Three.js 3D)                              │
│ ┌──────────────────────────────────────────────────────────┐│
│ │ [Grafo 3D interativo mostrando:                         ││
│ │ - Nós: Arquivos (cores by status)                      ││
│ │ - Arestas: Relacionamento entre métodos transformados ││
│ │ - Zoom/Pan/Rotação com mouse]                         ││
│ └──────────────────────────────────────────────────────────┘│
│                                                               │
│ 🔍 FILTROS E NAVEGAÇÃO                                      │
│ ┌──────────────────────────────────────────────────────────┐│
│ │ [Filtro por Status] ▼ [Buscar arquivo...] 🔎             ││
│ │ ☑️ Sucesso ☑️ Warnings ☑️ Erros ☑️ Manual Review          ││
│ └──────────────────────────────────────────────────────────┘│
│                                                               │
│ 📋 TRANSFORMAÇÕES DETALHADAS                               │
│ ┌──────────────────────────────────────────────────────────┐│
│ │ src/UserRepository.cs                            ✅       ││
│ │ ├─ GetUsers()          Line 42                          ││
│ │ │  Before:  SELECT TOP 10 * FROM Users                ││
│ │ │  After:   SELECT * FROM Users LIMIT 10             ││
│ │ │  Status:  ✅ Success (Confidence: 95%)             ││
│ │ │  [Copy] [Show Diff]                               ││
│ │ │                                                    ││
│ │ ├─ MergeOrderData()    Line 156                       ││
│ │ │  Before:  MERGE INTO Orders...                    ││
│ │ │  Error:   ❌ MERGE not supported in PostgreSQL    ││
│ │ │  Action:  [Manual Review Required]                ││
│ │ │  [View Context] [Show All Code]                   ││
│ │ │                                                    ││
│ │ src/OrderService.cs                            ⚠️      ││
│ │ ├─ ProcessOrders()     Line 18                        ││
│ │ │  Issue:   ⚠️ Dynamic SQL (string interpolation)   ││
│ │ │  Action:  [Manual Fix Required]                   ││
│ │ │  [View Code Context] [Email Report]              ││
│ │                                                      ││
│ └──────────────────────────────────────────────────────────┘│
│                                                               │
│ 📊 ESTATÍSTICAS                                             │
│ ┌──────────────────────────────────────────────────────────┐│
│ │ [Gráfico: Conversão por Tipo de SQL]    [Gráfico: Taxa] ││
│ │ ┌────────────────────┐                ┌──────────────┐  ││
│ │ │ SELECT: 25         │                │ 88.9% Taxa   │  ││
│ │ │ INSERT: 10         │                │ Sucesso      │  ││
│ │ │ UPDATE: 7          │                │              │  ││
│ │ │ DELETE: 3          │                │              │  ││
│ │ └────────────────────┘                └──────────────┘  ││
│ └──────────────────────────────────────────────────────────┘│
│                                                               │
│ 🎬 AÇÕES                                                    │
│ [📥 Download JSON] [📥 Download HTML] [🖨️ Print]           │
│ [✉️ Email] [📋 Copy Summary] [🔄 Refresh]                  │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

**Seções do HTML Report**:

**1️⃣ Header com Resumo**
```html
<header>
  <h1>Dialect SQL Translation Report</h1>
  <p>Gerado em: 2026-09-08 14:23:45</p>
  <div class="summary-cards">
    <card status="info">Arquivos Escaneados: 15</card>
    <card status="success">Conversões Bem-Sucedidas: 40</card>
    <card status="warning">Com Avisos: 3</card>
    <card status="danger">Erros: 2</card>
    <card status="primary">Tempo Total: 2.543s</card>
  </div>
</header>
```

**2️⃣ Visualização 3D (Three.js)**
```html
<section id="visualization">
  <div id="three-container"></div>
  <controls>
    <button>Zoom In</button>
    <button>Reset View</button>
    <button>Modo 2D/3D</button>
    <button>Exportar como Imagem</button>
  </controls>
  <legend>
    🔵 Arquivo com Sucesso
    🟡 Arquivo com Warnings
    🔴 Arquivo com Erro
  </legend>
</section>
```

**3️⃣ Filtros e Busca**
```html
<section id="filters">
  <input type="search" placeholder="Buscar por arquivo ou método..." />
  <select>
    <option value="all">Todos os Status</option>
    <option value="success">✅ Sucesso</option>
    <option value="warnings">⚠️ Warnings</option>
    <option value="errors">❌ Erros</option>
    <option value="manual">📝 Manual Review</option>
  </select>
  <multi-select>
    <checkbox>SELECT</checkbox>
    <checkbox>INSERT</checkbox>
    <checkbox>UPDATE</checkbox>
    <checkbox>DELETE</checkbox>
  </multi-select>
</section>
```

**4️⃣ Transformações Detalhadas (Accordion Expandível)**
```html
<section id="transformations">
  <div class="file-group">
    <header class="file-header" onclick="toggleExpand()">
      <span class="status-icon">✅</span>
      <span class="filename">src/UserRepository.cs</span>
      <span class="stats">(3 conversões, 0 erros)</span>
      <span class="toggle">▼</span>
    </header>
    
    <div class="method-group">
      <header class="method-header" onclick="toggleExpand()">
        <span class="method-name">GetUsers()</span>
        <span class="line">Line 42</span>
        <span class="status">✅ Success</span>
      </header>
      
      <div class="transformation-details">
        <div class="before-after-comparison">
          <div class="before-panel">
            <h4>Antes (SQL Server)</h4>
            <pre class="code sql-highlight">
SELECT TOP 10 * FROM Users
WHERE Status = @status
ORDER BY CreatedDate DESC
            </pre>
            <span class="badge">Confiança: 95%</span>
          </div>
          
          <div class="arrow">→</div>
          
          <div class="after-panel">
            <h4>Depois (PostgreSQL)</h4>
            <pre class="code sql-highlight">
SELECT * FROM Users
WHERE Status = $1
ORDER BY CreatedDate DESC
LIMIT 10
            </pre>
            <span class="badge success">Convertido com Sucesso</span>
          </div>
        </div>
        
        <div class="actions">
          <button class="btn-copy">📋 Copiar SQL Transformado</button>
          <button class="btn-diff">📊 Ver Diff Detalhado</button>
          <button class="btn-context">🔍 Ver Contexto no Código</button>
        </div>
      </div>
    </div>

    <div class="method-group error">
      <header class="method-header">
        <span class="method-name">MergeOrderData()</span>
        <span class="line">Line 156</span>
        <span class="status">❌ Error</span>
      </header>
      
      <div class="transformation-details">
        <div class="error-info">
          <h4>❌ Motivo da Falha</h4>
          <p class="error-message">
            MERGE statements are not supported in PostgreSQL.
            Consider using ON CONFLICT...DO UPDATE instead.
          </p>
          <div class="error-context">
            <strong>Código Original:</strong>
            <pre class="code">MERGE INTO Orders AS target...</pre>
          </div>
        </div>
        
        <div class="suggestion">
          <strong>💡 Sugestão Manual:</strong>
          <pre class="code suggestion-code">ON CONFLICT (id) 
DO UPDATE SET ...</pre>
        </div>
        
        <div class="actions">
          <button class="btn-manual-review">📝 Marcar para Manual Review</button>
          <button class="btn-show-context">🔍 Ver Código Completo</button>
          <button class="btn-share">📤 Compartilhar com Time</button>
        </div>
      </div>
    </div>
  </div>
</section>
```

**5️⃣ Estatísticas com Gráficos**
```html
<section id="statistics">
  <div class="chart-grid">
    <div class="chart-item">
      <h4>Distribuição por Tipo de SQL</h4>
      <canvas id="chart-by-type"></canvas>
    </div>
    
    <div class="chart-item">
      <h4>Taxa de Sucesso por Arquivo</h4>
      <canvas id="chart-success-rate"></canvas>
    </div>
    
    <div class="chart-item">
      <h4>Timeline de Conversão</h4>
      <canvas id="chart-timeline"></canvas>
    </div>
    
    <div class="chart-item">
      <h4>Resumo Geral</h4>
      <div class="stat-summary">
        <stat>
          <label>Taxa de Sucesso Geral</label>
          <value class="success">88.9%</value>
        </stat>
        <stat>
          <label>Tempo Médio por SQL</label>
          <value>56.5ms</value>
        </stat>
      </div>
    </div>
  </div>
</section>
```

**6️⃣ Rodapé com Ações**
```html
<footer>
  <div class="action-buttons">
    <button class="btn-export">📥 Exportar como PDF</button>
    <button class="btn-download-json">📥 Download JSON</button>
    <button class="btn-email">✉️ Enviar por Email</button>
    <button class="btn-share">📤 Compartilhar Link</button>
  </div>
  
  <div class="metadata">
    <p>Relatório gerado em: 2026-09-08 14:23:45</p>
    <p>Versão: Dialect v3.0 | Build: 1.0.0</p>
  </div>
</footer>
```

**Interatividade com Three.js**:
- Nós = Arquivos (cor por status: verde=sucesso, amarelo=warning, vermelho=erro)
- Arestas = Métodos relacionados com transformações
- Click no nó = Expande aquele arquivo no painel esquerdo
- Hover = Mostra tooltip com resumo
- Zoom/Pan = Navegação livre
- Filtros = Atualizam grafo em tempo real
- Exportar = Salva imagem do grafo

**Features Interativas**:
- ✅ Busca em tempo real (frontend, sem server)
- ✅ Filtros por status/tipo SQL
- ✅ Accordion expandível por arquivo
- ✅ Visualização antes/depois lado a lado
- ✅ Copiar SQL com um clique
- ✅ Ver diff detalhado entre antes/depois
- ✅ Tema claro/escuro (salvo em localStorage)
- ✅ Responsivo (desktop, tablet, mobile)
- ✅ Impressão otimizada

**Componente Novo** (para geração):
- `Dialect.Cli.Reporting/HtmlReportGenerator.cs` (~600 linhas)
  - Gerar HTML estruturado
  - Embutir CSS e JavaScript
  - Gerar dados JSON para Three.js
  - Gerar gráficos com Chart.js

---



## ⚙️ Requisitos Não-Funcionais

### RNF1: Performance
- Detecção: < 100ms por arquivo .cs (mesmo grandes 10k linhas)
- Transformação: < 50ms por SQL string
- Total para projeto 100 arquivos, 500 SQLs: < 30s
- Caching de ASTs para queries repetidas (TTL: 1 hora)

### RNF2: Segurança
- Nenhuma execução de SQL real (análise estática apenas)
- Validação de encoding/paths (prevent path traversal)
- Backup obrigatório antes de --output in-place
- Sem credentials/secrets em logs/relatórios

### RNF3: Usabilidade
- Dry-run mode (preview sem escrever)
- Verbose logging (--verbose para debug)
- **Colored output** (success/warning/error com cores ANSI)
- Clear error messages com contexto (linha, coluna, snippet)
- **Progress display em tempo real**:
  - Barra de progresso ASCII animada
  - Contador: [45/387] SQLs processadas
  - Velocidade: 12.3 SQLs/s
  - ETA: ~28 segundos restantes
  - Status atual: "Transformando src/OrderService.cs..."
  - Símbolos visuais: ✓ (sucesso), ⚠️ (warning), ❌ (erro)
  - Cores: Verde (sucesso), Amarelo (warning), Vermelho (erro), Azul (info)

### RNF4: Robustez
- Graceful failure: Parsing falha → report como ⚠️ manual review
- Rollback: Se reescrita falha → restaurar backup
- Idempotência: Reexecutar comando = mesmo resultado
- Compatibilidade: .NET 8.0+

### RNF5: Testabilidade
- Unit tests para cada componente (detector, transformer, rewriter)
- Integration tests com Docker databases
- Test matrix: 6 combinações SQL Server→PG→MySQL
- Coverage > 80%

---

## 🏗️ Arquitetura

### Layers

```
┌─────────────────────────────────────┐
│ CLI Layer                           │
│ - TranslateFilesCommand            │
│ - ParseOptions, Route to handlers  │
└──────────────┬──────────────────────┘
               ↓
┌─────────────────────────────────────┐
│ Orchestration Layer                 │
│ - SqlFileTransformationEngine       │
│ - Coordinate detect→parse→transform │
└──────────────┬──────────────────────┘
               ↓
┌─────────────────────────────────────┐
│ Business Logic Layer                │
│ - SqlStringDetector (keyword regex) │
│ - SqlTransformationEngine           │
│ - CSharpFileRewriter                │
│ - TransformationReporter            │
└──────────────┬──────────────────────┘
               ↓
┌─────────────────────────────────────┐
│ Core Layer (Existente)              │
│ - ISqlTranslator, ISqlCompiler      │
│ - SqlParserAdapters                 │
│ - Query Renderers                   │
│ - AST Types                         │
└─────────────────────────────────────┘
```

### Componentes Novos

| Componente | Namespace | Responsabilidade | Linhas Est. |
|-----------|-----------|------------------|------------|
| SqlStringDetector | Dialect.Cli.Scanning | Regex detection, keyword analysis | 400 |
| SqlExtractor | Dialect.Cli.Scanning | Normalize, extract with metadata | 300 |
| SqlTransformationEngine | Dialect.Cli.Transformation | Parse + Transform + Compile | 250 |
| CSharpFileRewriter | Dialect.Cli.Rewriting | Replace strings, preserve format | 350 |
| TransformationReporter | Dialect.Cli.Reporting | JSON/Markdown report generation | 250 |
| HtmlReportGenerator | Dialect.Cli.Reporting | HTML + Three.js visualization | 600 |
| SqlFileTransformationEngine | Dialect.Cli.Services | Orchestrate file-by-file transformation | 300 |
| TranslateFilesCommand | Dialect.Cli.Commands | CLI entry point, option parsing | 400 |

**Total Novo Código**: ~2,850 linhas

### Modificações Existentes (Pequenas)

- `SqlParserAdapter.cs`: Expandir com ParseToAst() implementations
- `SqlServerParserAdapter.cs`, `PostgreSqlParserAdapter.cs`, `MySqlParserAdapter.cs`: Implement ParseToAst()
- `Program.cs` (CLI): Adicionar comando "translate-files"

---

## 📦 Estrutura de Diretórios

```
src/Dialect.Cli/
├── Scanning/
│   ├── SqlStringDetector.cs         [NEW]
│   ├── SqlStringMatch.cs            [NEW]
│   ├── SqlStringType.cs             [NEW]
│   └── SqlExtractor.cs              [NEW]
├── Transformation/
│   ├── SqlTransformationEngine.cs   [NEW]
│   └── SqlTransformationResult.cs   [NEW]
├── Rewriting/
│   ├── CSharpFileRewriter.cs        [NEW]
│   └── RewritingOptions.cs          [NEW]
├── Reporting/
│   ├── TransformationReporter.cs    [NEW]
│   ├── HtmlReportGenerator.cs       [NEW - com Three.js]
│   ├── FileTransformationResult.cs  [NEW]
│   └── ReportOptions.cs             [NEW]
├── Reporting/Assets/               [NEW - HTML/CSS/JS]
│   ├── report-template.html
│   ├── styles.css
│   ├── three-setup.js
│   ├── chart-setup.js
│   └── interactions.js
├── Services/
│   └── SqlFileTransformationEngine.cs [NEW]
├── Commands/
│   └── TranslateFilesCommand.cs     [NEW]
└── Program.cs                       [MODIFIED - add command routing]
```

---

## 🎮 CLI Interface

### Comando Principal

```bash
dialect translate-files [OPTIONS]
```

### Opções

| Opção | Tipo | Obrigatório | Default | Descrição |
|-------|------|------------|---------|-----------|
| `--path` | string | ✅ | N/A | Caminho para varrer (*.cs files recursively) |
| `--source` | enum | ❌ | auto | Source dialect (sqlserver, postgresql, mysql, auto) |
| `--target` | enum | ✅ | N/A | Target dialect (sqlserver, postgresql, mysql) |
| `--output` | string | ❌ | ./ | Output directory (default: overwrite in-place) |
| `--include-backups` | flag | ❌ | false | Backup originals antes de reescrever |
| `--report` | string | ❌ | N/A | JSON report filepath |
| `--report-markdown` | string | ❌ | N/A | Markdown report filepath |
| `--report-html` | string | ❌ | N/A | HTML interactive report filepath (with Three.js visualization) |
| `--dry-run` | flag | ❌ | false | Preview apenas (sem escrever) |
| `--exclude` | string | ❌ | N/A | Exclude patterns (glob, comma-separated) |
| `--only-parse` | flag | ❌ | false | Apenas detectar SQL, não transformar |
| `--check-only` | flag | ❌ | false | Reportar issues, sem transformar |
| `--verbose` | flag | ❌ | false | Detailed logging |
| `--parallel` | int | ❌ | 4 | Número de arquivos em paralelo |

### Exemplos de Uso

```bash
# 1. Preview transformação (dry-run)
dialect translate-files --path ./src --target postgresql --dry-run

# 2. Converter com backup e relatórios completos
dialect translate-files --path ./src --target postgresql \
  --output ./src_pg --include-backups \
  --report report.json \
  --report-markdown report.md \
  --report-html report.html

# 3. Converter MySQL para SQL Server com exclusões
dialect translate-files --path . --source mysql --target sqlserver \
  --exclude "*/bin/*,*/obj/*,*/node_modules/*" --verbose

# 4. Apenas detectar SQL (sem transformar)
dialect translate-files --path ./src --only-parse --report detected.json

# 5. Apenas reportar issues com HTML interativo
dialect translate-files --path ./src --target postgresql --check-only \
  --report-html validation-report.html

# 6. Relatório HTML com todos os detalhes (3D + Gráficos)
dialect translate-files --path ./AppData --source sqlserver --target postgresql \
  --report-html migration-report.html --verbose
```

### Console Output Exemplo

**Execução em tempo real** (com --verbose):

```
╔════════════════════════════════════════════════════════════════╗
║         Dialect SQL Translation CLI v3.0                       ║
║         Source: SQL Server (T-SQL)                            ║
║         Target: PostgreSQL                                    ║
║         Mode: Transform & Rewrite                             ║
╚════════════════════════════════════════════════════════════════╝

⏱️  Started at: 2026-09-08 14:32:15
📁 Path: ./src
🔍 Scanning .cs files...
   Found: 45 files (2.3 MB)

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📊 Phase 1/4: SQL Detection
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[████████████████░░░░░░░░░░░░░░░░░░░░░░] 45% | 142/387 SQLs
⏱️  Elapsed: 3.2s | ETA: ~4s | Speed: 44.4 SQLs/s

🔄 Processing: src/UserRepository.cs
   ✓ Found 12 SQL strings (confidence: 92-98%)
   ✓ GetUsers() [Line 42]
   ✓ GetUserById() [Line 56]
   ...

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ Detection Complete
   └─ 387 SQL strings detected
   └─ 328 high-confidence (≥75%)
   └─ 45 manual review needed (50-74%)
   └─ 14 filtered out (<50%, likely false positives)

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📊 Phase 2/4: SQL Parsing
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[████████████████████████░░░░░░░░░░░░░] 65% | 226/387 SQLs
⏱️  Elapsed: 5.8s | ETA: ~3s | Speed: 39.0 SQLs/s

🔄 Parsing: src/OrderService.cs::ProcessOrders() [Line 18]
   Query: SELECT TOP 100 * FROM Orders WHERE...
   ✓ SELECT statement parsed (8 columns, 3 joins, subquery)

🔄 Parsing: src/OrderService.cs::MergeOrderData() [Line 156]
   Query: MERGE INTO Orders AS target...
   ⚠️  Warning: MERGE statement (not fully supported in PostgreSQL)
   └─ Action: Marked for manual review

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ Parsing Complete
   └─ 364 successful parses
   └─ 19 warnings (unsupported features)
   └─ 4 errors (complex/dynamic SQL - need manual review)

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📊 Phase 3/4: SQL Transformation
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[███████████████████████████░░░░░░░░░] 78% | 283/364 SQLs
⏱️  Elapsed: 8.2s | ETA: ~2s | Speed: 34.5 SQLs/s

🔄 Transforming: src/UserRepository.cs::GetUsers()
   T-SQL:     SELECT TOP 10 * FROM Users ORDER BY CreatedDate DESC
   ✓ PostgreSQL: SELECT * FROM Users ORDER BY CreatedDate DESC LIMIT 10
   └─ Time: 12ms | Features: LIMIT (1), ORDER BY (1)

🔄 Transforming: src/ReportService.cs::GenerateUserReport()
   T-SQL:     SELECT u.*, STRING_AGG(p.PermissionName, ',') FROM...
   ✓ PostgreSQL: SELECT u.*, string_agg(p.name, ',') FROM...
   └─ Time: 8ms | Features: STRING_AGG (1), JOIN (2), GROUP BY (1)

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ Transformation Complete
   └─ 364 successful transformations (100%)
   └─ 0 errors

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📊 Phase 4/4: File Rewriting
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

[██████████████████████████████░░░░░░░░] 88% | 40/45 Files
⏱️  Elapsed: 10.1s | ETA: ~1s | Speed: 4.0 Files/s

🔄 Rewriting: src/UserRepository.cs
   ✓ Backup created: src/UserRepository.cs.backup-20260908-143215
   ✓ 12 SQL strings replaced (123 → 119 bytes)
   ✓ Formatting preserved (CRLF, indentation)
   ✓ File written: 2.4 KB

🔄 Rewriting: src/OrderService.cs
   ⚠️  Warning: 1 SQL string skipped (manual review needed)
   ✓ 8 SQL strings replaced
   ✓ Backup created

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ Rewriting Complete
   └─ 44 files rewritten
   └─ 44 backups created (in-place .backup-TIMESTAMP)
   └─ 1 file with warnings (manual review needed)

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
📋 SUMMARY
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

✅ Success: 364/364 transformations (100%)
⚠️  Warnings: 1 (MERGE statement in OrderService.cs::MergeOrderData)
❌ Errors: 0

📊 Statistics:
   • Files scanned:      45
   • SQL strings found:  387
   • SQL parsed:         364
   • SQL transformed:    364
   • Files rewritten:    44
   • Backups created:    44
   • Total time:         10.3s
   • Average speed:      37.5 SQLs/s

📄 Reports Generated:
   • JSON:      report.json (3.2 KB)
   • Markdown:  report.md (5.7 KB)
   • HTML:      report.html (248 KB - 3D interactive dashboard)

✨ Next Steps:
   1. Review warnings: cat report.md | grep "⚠️"
   2. Validate build:  dotnet build ./src
   3. Run tests:       dotnet test --database postgresql
   4. Deploy:          See DEPLOYMENT.md for instructions

⏱️  Completed at: 2026-09-08 14:32:25
🎉 Success! All files ready for migration.

💡 Tip: Open report.html in browser for interactive 3D visualization
```

**Recursos do Console Output**:
- ✅ Barra de progresso ASCII animada (atualizada a cada segundo)
- ✅ Mostra velocidade atual (SQLs/s) e ETA em tempo real
- ✅ Cores ANSI: Verde (✓), Amarelo (⚠️), Vermelho (❌), Azul (info)
- ✅ Separadores visuais (━) para fácil leitura
- ✅ Exemplo de arquivo/método sendo processado
- ✅ Resumo final com estatísticas e próximos passos
- ✅ Suporta --verbose para mais detalhes
- ✅ Suporta redirecionamento para arquivo (> output.log)

---

## 🧪 Casos de Uso

### Caso 1: Migração SQL Server → PostgreSQL

**Contexto**: Empresa migrando de SQL Server para PostgreSQL

**Fluxo**:
```
1. User executa: 
   dialect translate-files --path ./AppData --source sqlserver --target postgresql \
     --output ./AppData_pg --include-backups --report migrate.json

2. CLI:
   ✓ Scan: Encontra 45 arquivos .cs com SQL
   ✓ Detect: Extrai 387 SQL strings
   ✓ Parse: 383 parseia com sucesso, 4 falham (procedures)
   ✓ Transform: 380 transforma, 3 com warnings (OUTPUT → N/A MySQL)
   ✓ Rewrite: 44 arquivos reescritos com backups
   ✓ Report: JSON com detalhes

3. Output:
   AppData_pg/         (converted .cs files)
   AppData/            (originals com .backup-TIMESTAMP)
   migrate.json        (2.5 KB report)

4. Next steps:
   - Review warnings em migrate.json
   - Testar compilação: dotnet build
   - Executar testes contra PostgreSQL
   - Deploy
```

**Resultado**: Migração automatizada, ~95% das SQLs convertidas, manual review apenas de edge cases.

---

### Caso 2: Descoberta de SQLs em Codebase

**Contexto**: Auditoria: "Qual SQL usamos neste projeto?"

**Fluxo**:
```
dialect translate-files --path . --only-parse --report sqls.json

Output: sqls.json com:
- 12 arquivos com SQL
- 47 SQL strings encontradas
- Confidence scores (para filtrar false positives)
- Arquivo + linha + snippet de cada SQL
```

**Resultado**: Inventário completo de SQLs, base para refatoração futura.

---

### Caso 3: Validação pré-migração

**Contexto**: "Conseguimos converter tudo?"

**Fluxo**:
```
dialect translate-files --path ./src --target postgresql --check-only \
  --report issues.json --verbose

Output: 
✗ 5 queries não suportadas (procedures, MERGE complexo, etc.)
⚠️ 8 warnings (OUTPUT não mapeado, etc.)
✓ 334 queries prontas para conversão

Next: Revisar os 5 bloqueadores manualmente
```

---

### Caso 4: Bidirecional (PostgreSQL → SQL Server)

**Contexto**: Produto com suporte multi-DB

**Fluxo**:
```
# Converter codebase PostgreSQL para SQL Server
dialect translate-files --path ./src --source postgresql --target sqlserver \
  --output ./src_sqlserver --report pg_to_tsql.json

# Depois converter MySQL para PostgreSQL
dialect translate-files --path ./api --source mysql --target postgresql \
  --report mysql_to_pg.json

# Todas 6 combinações (3 dialects × 2 direções) funcionam
```

---

## 🧪 Test Scenarios - Exemplos de Conversão (07_CLI)

**Localização**: `samples/Dialect.Samples/07_CLI/`

**Objetivo**: Fornecer cenários de teste exhaustivos para validar conversão bidirecional de SQL strings em código C#. **TODOS OS EXEMPLOS SÃO EXECUTÁVEIS CONTRA O BANCO DE DADOS REAL (Docker)**.

**Estrutura**:
```
samples/Dialect.Samples/07_CLI/
├── SqlServerExamples.cs          (Exemplos em T-SQL)
├── PostgreSQLExamples.cs         (Exemplos em PostgreSQL)
└── MySQLExamples.cs              (Exemplos em MySQL)
```

### 🗄️ Modelo Relacional (Docker Database)

**Todos os exemplos de SQL devem ser executáveis contra o modelo relacional definido em `docker/`**

**Estrutura Docker**:
```
docker/
├── docker-compose.yml          # Configuração dos 3 serviços (PostgreSQL, MySQL, SQL Server)
├── sql-server/
│   └── init.sql               # Schema e dados iniciais (T-SQL)
├── postgresql/
│   └── init.sql               # Schema e dados iniciais (PostgreSQL)
└── mysql/
    └── init.sql               # Schema e dados iniciais (MySQL)
```

**Tabelas Definidas** (idênticas em estrutura, com variações de naming):

#### **Users / user_id / user_id**
- **SQL Server**: `UserId INT PRIMARY KEY IDENTITY(1,1), Username NVARCHAR(100), Email NVARCHAR(100) UNIQUE, CreatedAt DATETIME`
- **PostgreSQL**: `user_id SERIAL PRIMARY KEY, username VARCHAR(100), email VARCHAR(100) UNIQUE, created_at TIMESTAMP`
- **MySQL**: `user_id INT AUTO_INCREMENT PRIMARY KEY, username VARCHAR(100), email VARCHAR(100) UNIQUE, created_at TIMESTAMP`
- **Dados**: 4 registros (john_doe, jane_smith, bob_wilson, alice_johnson)

#### **Products / product_id / product_id**
- **SQL Server**: `ProductId INT PRIMARY KEY IDENTITY(1,1), Name NVARCHAR(100), Price DECIMAL(10,2), StockQuantity INT`
- **PostgreSQL**: `product_id SERIAL PRIMARY KEY, name VARCHAR(100), price DECIMAL(10,2), stock_quantity INT`
- **MySQL**: `product_id INT AUTO_INCREMENT PRIMARY KEY, name VARCHAR(100), price DECIMAL(10,2), stock_quantity INT`
- **Dados**: 5 registros (Laptop, Mouse, Keyboard, Monitor, Headphones)

#### **Orders / order_id / order_id**
- **SQL Server**: `OrderId INT PRIMARY KEY IDENTITY(1,1), UserId INT FK, OrderDate DATETIME, Total DECIMAL(10,2)`
- **PostgreSQL**: `order_id SERIAL PRIMARY KEY, user_id INT FK, order_date TIMESTAMP, total DECIMAL(10,2)`
- **MySQL**: `order_id INT AUTO_INCREMENT PRIMARY KEY, user_id INT FK, order_date TIMESTAMP, total DECIMAL(10,2)`
- **Dados**: 5 registros
- **Índices**: `idx_orders_user_id` (ou equivalente em cada dialeto)

#### **OrderItems / order_item_id / order_item_id**
- **SQL Server**: `OrderItemId INT PK, OrderId INT FK, ProductId INT FK, Quantity INT, UnitPrice DECIMAL(10,2)`
- **PostgreSQL**: `order_item_id SERIAL PK, order_id INT FK, product_id INT FK, quantity INT, unit_price DECIMAL(10,2)`
- **MySQL**: `order_item_id INT AUTO_INCREMENT PK, order_id INT FK, product_id INT FK, quantity INT, unit_price DECIMAL(10,2)`
- **Dados**: 8 registros
- **Índices**: `idx_order_items_order_id`, `idx_order_items_product_id`

**Mapeamento de Naming Conventions** (para demonstrar transformação):
| Propriedade | SQL Server | PostgreSQL | MySQL |
|-------------|-----------|-----------|-------|
| PK Auto-inc | `IDENTITY(1,1)` | `SERIAL` | `AUTO_INCREMENT` |
| String | `NVARCHAR(n)` | `VARCHAR(n)` | `VARCHAR(n)` |
| Decimal | `DECIMAL(10,2)` | `DECIMAL(10,2)` | `DECIMAL(10,2)` |
| DateTime | `DATETIME` | `TIMESTAMP` | `TIMESTAMP` |
| Naming | CamelCase | snake_case | snake_case |
| Índice | `IX_TableColumn` | `idx_table_column` | `idx_table_column` |
| PK Constraint | `PRIMARY KEY` | `PRIMARY KEY` | `PRIMARY KEY` |
| FK Constraint | `FOREIGN KEY` | `FOREIGN KEY` | `FOREIGN KEY` |

**Exemplo de Transformação (Users table)**:

SQL Server:
```sql
CREATE TABLE Users (
    UserId INT PRIMARY KEY IDENTITY(1,1),
    Username NVARCHAR(100) NOT NULL,
    Email NVARCHAR(100) NOT NULL,
    CreatedAt DATETIME DEFAULT GETDATE()
);
CREATE UNIQUE INDEX IX_Users_Email ON Users(Email);
```

PostgreSQL (equivalente):
```sql
CREATE TABLE IF NOT EXISTS "Users" (
    user_id SERIAL PRIMARY KEY,
    username VARCHAR(100) NOT NULL,
    email VARCHAR(100) NOT NULL UNIQUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

MySQL (equivalente):
```sql
CREATE TABLE IF NOT EXISTS Users (
    user_id INT AUTO_INCREMENT PRIMARY KEY,
    username VARCHAR(100) NOT NULL,
    email VARCHAR(100) NOT NULL UNIQUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

**Instruções para Executar Exemplos**:
1. Iniciar Docker: `cd docker && docker-compose up -d`
2. Aguardar inicialização (20-30 segundos)
3. Executar testes: `dotnet test samples/Dialect.Samples/07_CLI/`
4. Verificar saída: Reports em `samples/Dialect.Samples/07_CLI/results/`

**⚠️ Ao Adicionar Novos Exemplos**:
- Se precisar de novas tabelas/colunas, atualizar todos os 3 arquivos init.sql
- Manter estrutura consistente entre SQL Server, PostgreSQL e MySQL
- Usar nomes CamelCase em SQL Server e snake_case em PostgreSQL/MySQL
- Adicionar dados de teste correspondentes em cada banco
- Testar queries contra Docker antes de adicionar ao sample
- Atualizar tabela de "Mapeamento de Naming Conventions" se houver novos padrões

**Referências de Arquivos**:
- [docker/sql-server/init.sql](docker/sql-server/init.sql) - Schema e dados SQL Server
- [docker/postgresql/init.sql](docker/postgresql/init.sql) - Schema e dados PostgreSQL  
- [docker/mysql/init.sql](docker/mysql/init.sql) - Schema e dados MySQL

---

### Arquivo 1: SqlServerExamples.cs (T-SQL)

**Contém**: 66 SQL strings inline em padrões realistas

**Breakdown**: 15 SELECT + 12 INSERT + 12 UPDATE + 10 DELETE + 11 Advanced + 6 Procedures/Functions

#### Procedure/Function Scenarios (6 cenários)

**SQL Server T-SQL Procedures/Functions**:

```csharp
// 1. Simple Procedure (SELECT from Users)
var proc1 = @"CREATE PROCEDURE GetAllUsers
AS
BEGIN
    SELECT UserId, Username, Email, CreatedAt
    FROM Users
    ORDER BY CreatedAt DESC
END";

// 2. Procedure with Parameters
var proc2 = @"CREATE PROCEDURE GetUserById @UserId INT
AS
BEGIN
    SELECT UserId, Username, Email, CreatedAt
    FROM Users
    WHERE UserId = @UserId
END";

// 3. Procedure with OUTPUT Parameter
var proc3 = @"CREATE PROCEDURE CountUserOrders
    @UserId INT,
    @OrderCount INT OUTPUT
AS
BEGIN
    SELECT @OrderCount = COUNT(*)
    FROM Orders
    WHERE UserId = @UserId
END";

// 4. Function returning scalar
var proc4 = @"CREATE FUNCTION GetUserEmail (@UserId INT)
RETURNS NVARCHAR(100)
AS
BEGIN
    DECLARE @Email NVARCHAR(100)
    SELECT @Email = Email FROM Users WHERE UserId = @UserId
    RETURN @Email
END";

// 5. Function returning table
var proc5 = @"CREATE FUNCTION GetUserOrdersByYear (@UserId INT, @Year INT)
RETURNS TABLE
AS
RETURN
    SELECT OrderId, OrderDate, Total
    FROM Orders
    WHERE UserId = @UserId AND YEAR(OrderDate) = @Year";

// 6. Procedure with INSERT/UPDATE logic
var proc6 = @"CREATE PROCEDURE UpsertProduct
    @ProductId INT,
    @Name NVARCHAR(100),
    @Price DECIMAL(10,2)
AS
BEGIN
    IF EXISTS (SELECT 1 FROM Products WHERE ProductId = @ProductId)
        UPDATE Products SET Name = @Name, Price = @Price WHERE ProductId = @ProductId
    ELSE
        INSERT INTO Products (Name, Price) VALUES (@Name, @Price)
END";
```

---

### SELECT Scenarios (15 cenários)
```csharp
// 1. Simple SELECT
var sql1 = "SELECT * FROM Users";

// 2. SELECT with WHERE
var sql2 = "SELECT * FROM Users WHERE Id = @id";

// 3. SELECT with ORDER BY
var sql3 = "SELECT * FROM Users ORDER BY Name DESC";

// 4. SELECT with TOP
var sql4 = "SELECT TOP 10 * FROM Users ORDER BY CreatedDate DESC";

// 5. SELECT with OFFSET FETCH
var sql5 = "SELECT * FROM Users ORDER BY Id OFFSET 20 ROWS FETCH NEXT 10 ROWS ONLY";

// 6. SELECT with JOIN
var sql6 = @"SELECT u.Id, u.Name, o.OrderId
            FROM Users u
            INNER JOIN Orders o ON u.Id = o.UserId
            WHERE u.Status = 'Active'";

// 7. SELECT with LEFT JOIN
var sql7 = @"SELECT u.Id, u.Name, COUNT(o.OrderId) as OrderCount
            FROM Users u
            LEFT JOIN Orders o ON u.Id = o.UserId
            GROUP BY u.Id, u.Name
            HAVING COUNT(o.OrderId) > 0";

// 8. SELECT with multiple JOINs
var sql8 = @"SELECT u.Id, u.Name, o.OrderId, p.ProductName
            FROM Users u
            INNER JOIN Orders o ON u.Id = o.UserId
            INNER JOIN Products p ON o.ProductId = p.Id
            WHERE o.OrderDate >= @startDate";

// 9. SELECT with DISTINCT
var sql9 = "SELECT DISTINCT City FROM Users WHERE Country = @country";

// 10. SELECT with aggregate functions
var sql10 = @"SELECT City, COUNT(*) as UserCount, AVG(Age) as AvgAge
             FROM Users
             GROUP BY City
             ORDER BY UserCount DESC";

// 11. SELECT with CASE statement
var sql11 = @"SELECT Id, Name, 
             CASE 
               WHEN Age < 18 THEN 'Minor'
               WHEN Age >= 18 AND Age < 65 THEN 'Adult'
               ELSE 'Senior'
             END as AgeGroup
             FROM Users";

// 12. SELECT with subquery in FROM
var sql12 = @"SELECT * FROM (
               SELECT Id, Name, YEAR(CreatedDate) as Year
               FROM Users
             ) AS UsersByYear
             WHERE Year = 2024";

// 13. SELECT with subquery in WHERE (IN)
var sql13 = @"SELECT * FROM Users
             WHERE Id IN (SELECT UserId FROM Orders WHERE Status = 'Completed')";

// 14. SELECT with CTE (WITH clause)
var sql14 = @"WITH RecentOrders AS (
               SELECT UserId, COUNT(*) as OrderCount
               FROM Orders
               WHERE OrderDate >= DATEADD(MONTH, -1, GETDATE())
               GROUP BY UserId
             )
             SELECT u.Id, u.Name, ro.OrderCount
             FROM Users u
             LEFT JOIN RecentOrders ro ON u.Id = ro.UserId";

// 15. SELECT with Window Functions
var sql15 = @"SELECT Id, Name, Salary,
             ROW_NUMBER() OVER (ORDER BY Salary DESC) as SalaryRank,
             LAG(Salary) OVER (ORDER BY Salary DESC) as PrevSalary
             FROM Employees";
```

#### INSERT Scenarios (12 cenários)
```csharp
// 16. Simple INSERT
var sql16 = "INSERT INTO Users (Name, Email) VALUES (@name, @email)";

// 17. INSERT with multiple columns
var sql17 = @"INSERT INTO Users (Id, Name, Email, CreatedDate, Status)
             VALUES (@id, @name, @email, @createdDate, @status)";

// 18. INSERT multiple rows
var sql18 = @"INSERT INTO Products (Name, Price, Category)
             VALUES (@name1, @price1, @category1),
                    (@name2, @price2, @category2),
                    (@name3, @price3, @category3)";

// 19. INSERT...SELECT
var sql19 = @"INSERT INTO UserArchive (Id, Name, Email, ArchivedDate)
             SELECT Id, Name, Email, GETDATE()
             FROM Users
             WHERE Status = 'Inactive'";

// 20. INSERT with default values
var sql20 = @"INSERT INTO Orders (UserId, OrderDate, Status, TotalAmount)
             VALUES (@userId, GETDATE(), 'Pending', 0.00)";

// 21. INSERT with identity handling
var sql21 = @"INSERT INTO Employees (Name, Department, Salary)
             VALUES (@name, @department, @salary);
             SELECT SCOPE_IDENTITY() as NewEmployeeId";

// 22. INSERT with CTE source
var sql22 = @"WITH NewUsers AS (
               SELECT @name as Name, @email as Email, GETDATE() as CreatedDate
             )
             INSERT INTO Users (Name, Email, CreatedDate)
             SELECT Name, Email, CreatedDate FROM NewUsers";

// 23. INSERT with complex expressions
var sql23 = @"INSERT INTO Orders (UserId, OrderDate, ShippedDate, Status)
             VALUES (@userId,
                     GETDATE(),
                     DATEADD(DAY, 7, GETDATE()),
                     'Processing')";

// 24. Bulk INSERT pattern
var sql24 = @"INSERT INTO ImportedData (SourceId, ImportedValue, ImportDate)
             SELECT Id, Value, @importDate
             FROM SourceTable st
             WHERE st.Type = 'Import'";

// 25. INSERT with OUTPUT clause
var sql25 = @"INSERT INTO Users (Name, Email, CreatedDate)
             OUTPUT inserted.Id, inserted.Name, inserted.CreatedDate
             VALUES (@name, @email, GETDATE())";

// 26. INSERT from external source
var sql26 = @"INSERT INTO Products (ExternalId, Name, Price)
             SELECT ProductId, ProductName, UnitPrice
             FROM ExternalSystem.Products
             WHERE IsActive = 1";

// 27. INSERT with validation
var sql27 = @"INSERT INTO Orders (UserId, OrderDate, Total)
             SELECT UserId, GETDATE(), SUM(LineTotal)
             FROM OrderLines
             GROUP BY UserId
             HAVING SUM(LineTotal) > 100";
```

#### UPDATE Scenarios (12 cenários)
```csharp
// 28. Simple UPDATE
var sql28 = "UPDATE Users SET Status = @status WHERE Id = @id";

// 29. UPDATE multiple columns
var sql29 = @"UPDATE Users
             SET Status = @status, LastModifiedDate = GETDATE(), ModifiedBy = @user
             WHERE Id = @id";

// 30. UPDATE with WHERE condition
var sql30 = @"UPDATE Orders
             SET Status = 'Shipped', ShippedDate = GETDATE()
             WHERE Status = 'Processing' AND CreatedDate < DATEADD(DAY, -3, GETDATE())";

// 31. UPDATE based on JOIN
var sql31 = @"UPDATE Orders
             SET Status = 'Cancelled'
             FROM Orders o
             INNER JOIN Users u ON o.UserId = u.Id
             WHERE u.Status = 'Suspended'";

// 32. UPDATE with calculation
var sql32 = @"UPDATE Products
             SET Price = Price * 1.1,
                 LastUpdated = GETDATE()
             WHERE CategoryId = @categoryId";

// 33. Bulk UPDATE
var sql33 = @"UPDATE Users
             SET LastLoginDate = GETDATE(),
                 LoginCount = LoginCount + 1
             WHERE Id IN (SELECT UserId FROM Sessions WHERE Date = @today)";

// 34. UPDATE with CASE
var sql34 = @"UPDATE Employees
             SET Salary = CASE
                           WHEN Department = 'Sales' THEN Salary * 1.15
                           WHEN Department = 'IT' THEN Salary * 1.12
                           ELSE Salary * 1.05
                         END
             WHERE HireDate < DATEADD(YEAR, -2, GETDATE())";

// 35. UPDATE with subquery
var sql35 = @"UPDATE Users
             SET Status = 'VIP'
             WHERE Id IN (
               SELECT UserId FROM Orders
               GROUP BY UserId
               HAVING SUM(TotalAmount) > 50000
             )";

// 36. UPDATE from external data
var sql36 = @"UPDATE Users
             SET Email = et.NewEmail,
                 ModifiedDate = GETDATE()
             FROM Users u
             INNER JOIN EmailTransfer et ON u.Id = et.UserId
             WHERE et.Status = 'Pending'";

// 37. UPDATE with OUTPUT
var sql37 = @"UPDATE Orders
             SET Status = 'Cancelled', CancelledDate = GETDATE()
             OUTPUT deleted.Id, deleted.Status, inserted.CancelledDate
             WHERE OrderDate < DATEADD(YEAR, -1, GETDATE())";

// 38. Conditional UPDATE
var sql38 = @"UPDATE Products
             SET StockLevel = CASE
                               WHEN StockLevel < 10 THEN 50
                               ELSE StockLevel
                             END
             WHERE CategoryId = @categoryId AND Discontinued = 0";

// 39. UPDATE with aggregate
var sql39 = @"UPDATE Users
             SET TotalOrderValue = (
               SELECT SUM(Total) FROM Orders WHERE UserId = Users.Id
             ),
             LastOrderDate = (
               SELECT MAX(OrderDate) FROM Orders WHERE UserId = Users.Id
             )
             WHERE Status = 'Active'";
```

#### DELETE Scenarios (10 cenários)
```csharp
// 40. Simple DELETE
var sql40 = "DELETE FROM Users WHERE Id = @id";

// 41. DELETE with condition
var sql41 = "DELETE FROM Orders WHERE Status = 'Cancelled' AND CreatedDate < DATEADD(MONTH, -6, GETDATE())";

// 42. DELETE with JOIN
var sql42 = @"DELETE FROM Orders
             FROM Orders o
             INNER JOIN Users u ON o.UserId = u.Id
             WHERE u.Status = 'Deleted'";

// 43. DELETE with IN subquery
var sql43 = @"DELETE FROM OrderItems
             WHERE OrderId IN (
               SELECT Id FROM Orders WHERE Status = 'Cancelled'
             )";

// 44. Bulk DELETE with audit
var sql44 = @"DELETE FROM AuditLog
             WHERE CreatedDate < DATEADD(YEAR, -2, GETDATE())
             AND LogLevel = 'Debug'";

// 45. DELETE with OUTPUT (archive pattern)
var sql45 = @"DELETE FROM Orders
             OUTPUT deleted.* INTO OrdersArchive
             WHERE OrderDate < DATEADD(YEAR, -5, GETDATE())
             AND Status IN ('Cancelled', 'Refunded')";

// 46. Soft DELETE pattern
var sql46 = @"UPDATE Users
             SET DeletedDate = GETDATE(), IsDeleted = 1
             WHERE Id = @id
             AND DeletedDate IS NULL";

// 47. DELETE cascade pattern
var sql47 = @"DELETE FROM UserPreferences WHERE UserId IN (
               SELECT Id FROM Users WHERE Status = 'Inactive' AND LastLoginDate < DATEADD(YEAR, -1, GETDATE())
             )";

// 48. DELETE with complex WHERE
var sql48 = @"DELETE FROM Logs
             WHERE CreatedDate < GETDATE()
             AND (Severity = 'Info' OR (Severity = 'Warning' AND CreatedDate < DATEADD(MONTH, -3, GETDATE())))
             AND Source NOT IN ('System', 'Security')";

// 49. DELETE with NOT EXISTS
var sql49 = @"DELETE FROM Products
             WHERE NOT EXISTS (
               SELECT 1 FROM OrderItems WHERE ProductId = Products.Id
             )
             AND CreatedDate < DATEADD(MONTH, -12, GETDATE())";
```

#### Advanced Scenarios (11 cenários)
```csharp
// 50. MERGE statement (UPSERT)
var sql50 = @"MERGE INTO Users AS target
             USING (VALUES (@id, @name, @email)) AS source (Id, Name, Email)
             ON target.Id = source.Id
             WHEN MATCHED THEN
               UPDATE SET Name = source.Name, Email = source.Email, ModifiedDate = GETDATE()
             WHEN NOT MATCHED THEN
               INSERT (Id, Name, Email, CreatedDate) 
               VALUES (source.Id, source.Name, source.Email, GETDATE())";

// 51. Complex CTE with multiple levels
var sql51 = @"WITH RECURSIVE EmployeeHierarchy AS (
               SELECT Id, Name, ManagerId, 1 as Level FROM Employees WHERE ManagerId IS NULL
               UNION ALL
               SELECT e.Id, e.Name, e.ManagerId, eh.Level + 1
               FROM Employees e
               INNER JOIN EmployeeHierarchy eh ON e.ManagerId = eh.Id
             )
             SELECT * FROM EmployeeHierarchy WHERE Level <= 3";

// 52. Window function with PARTITION
var sql52 = @"SELECT 
               Id, Name, Salary, Department,
               RANK() OVER (PARTITION BY Department ORDER BY Salary DESC) as DeptSalaryRank,
               ROW_NUMBER() OVER (ORDER BY Salary DESC) as OverallRank
             FROM Employees";

// 53. String aggregation
var sql53 = @"SELECT 
               UserId,
               STRING_AGG(ProductName, ', ') as ProductList,
               COUNT(*) as TotalOrders
             FROM OrderItems
             GROUP BY UserId";

// 54. Date calculations
var sql54 = @"SELECT 
               Id, Name, CreatedDate,
               DATEDIFF(DAY, CreatedDate, GETDATE()) as DaysActive,
               YEAR(CreatedDate) as YearCreated,
               MONTH(CreatedDate) as MonthCreated
             FROM Users
             WHERE DATEDIFF(YEAR, CreatedDate, GETDATE()) >= 1";

// 55. JSON operations
var sql55 = @"SELECT 
               Id, Name,
               JSON_VALUE(Metadata, '$.Department') as Department,
               JSON_VALUE(Metadata, '$.Title') as Title
             FROM Users
             WHERE JSON_VALUE(Metadata, '$.IsActive') = 'true'";

// 56. Full outer join pattern
var sql56 = @"SELECT COALESCE(u.Id, h.UserId) as UserId,
               u.Name,
               h.HistoryCount
             FROM Users u
             FULL OUTER JOIN (
               SELECT UserId, COUNT(*) as HistoryCount
               FROM UserHistory
               GROUP BY UserId
             ) h ON u.Id = h.UserId";

// 57. Paging pattern
var sql57 = @"SELECT *
             FROM Users
             WHERE Status = 'Active'
             ORDER BY CreatedDate DESC
             OFFSET (@pageNumber - 1) * @pageSize ROWS
             FETCH NEXT @pageSize ROWS ONLY";

// 58. Search with ranking
var sql58 = @"SELECT TOP 100
               Id, Name, Email,
               CASE 
                 WHEN Name LIKE @searchTerm + '%' THEN 100
                 WHEN Name LIKE '%' + @searchTerm + '%' THEN 50
                 WHEN Email LIKE '%' + @searchTerm + '%' THEN 25
                 ELSE 10
               END as Relevance
             FROM Users
             WHERE Status = 'Active'
             ORDER BY Relevance DESC, Name";

// 59. Recursive hierarchy with parent
var sql59 = @"SELECT 
               Id, Name, ParentId, CategoryName,
               CONVERT(VARCHAR(MAX), CAST(Id AS VARCHAR) + '/') as Path
             FROM Categories
             WHERE ParentId IS NULL
             UNION ALL
             SELECT 
               c.Id, c.Name, c.ParentId, c.CategoryName,
               ch.Path + CAST(c.Id AS VARCHAR) + '/'
             FROM Categories c
             INNER JOIN CategoryHierarchy ch ON c.ParentId = ch.Id";

// 60. Complex business logic query
var sql60 = @"SELECT 
               u.Id, u.Name, 
               COUNT(DISTINCT o.Id) as TotalOrders,
               SUM(o.TotalAmount) as TotalSpent,
               AVG(o.TotalAmount) as AvgOrderValue,
               MAX(o.OrderDate) as LastOrderDate,
               CASE 
                 WHEN SUM(o.TotalAmount) > 100000 THEN 'Platinum'
                 WHEN SUM(o.TotalAmount) > 50000 THEN 'Gold'
                 WHEN SUM(o.TotalAmount) > 10000 THEN 'Silver'
                 ELSE 'Bronze'
               END as Tier
             FROM Users u
             LEFT JOIN Orders o ON u.Id = o.UserId AND o.Status != 'Cancelled'
             WHERE u.Status = 'Active'
             GROUP BY u.Id, u.Name
             HAVING COUNT(DISTINCT o.Id) > 0
             ORDER BY TotalSpent DESC";
```

### Arquivo 2: PostgreSQLExamples.cs

**Idêntico ao SqlServerExamples.cs, mas com sintaxe PostgreSQL**:
- `@param` → `$1, $2` ou `:param`
- `GETDATE()` → `NOW()` ou `CURRENT_TIMESTAMP`
- `DATEADD(MONTH, -1, GETDATE())` → `NOW() - INTERVAL '1 month'`
- `TOP 10` → `LIMIT 10`
- `OFFSET...ROWS FETCH` → `OFFSET n LIMIT m`
- `MERGE` → `ON CONFLICT...DO UPDATE`
- `OUTPUT` → `RETURNING`
- `STRING_AGG()` (nativo em PostgreSQL)
- JSON: `JSON_VALUE()` → `->>` ou `->`
- **Procedures/Functions**:
  - `CREATE PROCEDURE` → `CREATE OR REPLACE FUNCTION ... RETURNS void`
  - `CREATE FUNCTION` → `CREATE OR REPLACE FUNCTION`
  - `RETURNS NVARCHAR(100)` → `RETURNS VARCHAR(100)`
  - `IF...ELSE` → `IF...THEN...ELSE...END IF`
  - `BEGIN...END` → `BEGIN...END` ou `$$...$$`
  - Parameters: `@param INT` → `param INT` (positional)
  - `OUTPUT` parameter → não suportado, usar INOUT ou RETURNING

**Total**: 66 cenários em sintaxe PostgreSQL (incluindo 6 procedures/functions)

### Arquivo 3: MySQLExamples.cs

**Idêntico ao SqlServerExamples.cs, mas com sintaxe MySQL**:
- `@param` → `?` (placeholders)
- `GETDATE()` → `NOW()` ou `CURRENT_TIMESTAMP`
- `DATEADD()` → `DATE_ADD()` ou `DATE_SUB()`
- `TOP 10` → `LIMIT 10`
- `OFFSET` → `OFFSET n LIMIT m`
- `MERGE` → `ON DUPLICATE KEY UPDATE`
- `OUTPUT` → ❌ Não suportado, usar SELECT após INSERT
- `STRING_AGG()` → `GROUP_CONCAT()`
- JSON: JSON functions nativas em MySQL 5.7+
- Backticks: `` `column` `` para identifiers
- **Procedures/Functions**:
  - `CREATE PROCEDURE` → `CREATE PROCEDURE` (sintaxe similar)
  - `CREATE FUNCTION` → `CREATE FUNCTION`
  - Parameters: `@param INT` → `param INT` (positional)
  - `RETURNS TABLE` → ❌ Não suportado, usar SELECT direto
  - `OUTPUT` parameter → ❌ Não suportado, usar INOUT
  - `BEGIN...END` → `BEGIN...END`
  - Delimiters: Usar `DELIMITER //` para procedures

**Total**: 66 cenários em sintaxe MySQL (incluindo 6 procedures/functions)

**Nota**: MySQL 5.7 não tem CTEs e Window Functions. MySQLExamples.cs deve ter flag `[Fact(Skip = "MySQL 5.7 limitation")]` para esses cenários ou usar `[Theory] [InlineData(Skip = "MySQL 8.0+ required")]`. Procedures/Functions são suportados mas com algumas limitações (RETURNS TABLE não existe)

---

### Model Enhancements (Opcional - para Cenários Adicionais)

**Status Atual**: Os 4 schemas base (Users, Products, Orders, OrderItems) já estão implementados e populados em todos os 3 bancos.

**Quando Estender**:
- Se os 66 exemplos base não forem suficientes para cobrir novos padrões SQL
- Quando quiser adicionar testes para JOINs mais complexos (M:M, self-join)
- Para demonstrar CASE/WHEN avançado, recursive CTEs, ou window functions mais sofisticadas

**Se Precisar Adicionar Novas Tabelas** (opcional):

#### Categories (opcional, para hierarquias e self-join)
```sql
-- SQL Server
CREATE TABLE Categories (
    CategoryId INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(100) NOT NULL,
    ParentCategoryId INT NULL REFERENCES Categories(CategoryId),
    Description NVARCHAR(MAX)
);

-- PostgreSQL
CREATE TABLE Categories (
    category_id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    parent_category_id INT REFERENCES Categories(category_id),
    description TEXT
);

-- MySQL
CREATE TABLE Categories (
    category_id INT AUTO_INCREMENT PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    parent_category_id INT REFERENCES Categories(category_id),
    description TEXT
);
```

#### ProductCategories (opcional, para M:M JOINs)
```sql
-- SQL Server
CREATE TABLE ProductCategories (
    ProductId INT NOT NULL REFERENCES Products(ProductId),
    CategoryId INT NOT NULL REFERENCES Categories(CategoryId),
    PRIMARY KEY (ProductId, CategoryId)
);

-- PostgreSQL
CREATE TABLE ProductCategories (
    product_id INT NOT NULL REFERENCES Products(product_id),
    category_id INT NOT NULL REFERENCES Categories(category_id),
    PRIMARY KEY (product_id, category_id)
);

-- MySQL
CREATE TABLE ProductCategories (
    product_id INT NOT NULL REFERENCES Products(product_id),
    category_id INT NOT NULL REFERENCES Categories(category_id),
    PRIMARY KEY (product_id, category_id)
);
```

#### UserPermissions (opcional, para CASE/WHEN avançado)
```sql
-- SQL Server
CREATE TABLE UserPermissions (
    PermissionId INT PRIMARY KEY IDENTITY(1,1),
    UserId INT NOT NULL REFERENCES Users(UserId),
    PermissionName NVARCHAR(50) NOT NULL,
    GrantedAt DATETIME DEFAULT GETDATE()
);

-- PostgreSQL
CREATE TABLE UserPermissions (
    permission_id SERIAL PRIMARY KEY,
    user_id INT NOT NULL REFERENCES Users(user_id),
    permission_name VARCHAR(50) NOT NULL,
    granted_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- MySQL
CREATE TABLE UserPermissions (
    permission_id INT AUTO_INCREMENT PRIMARY KEY,
    user_id INT NOT NULL REFERENCES Users(user_id),
    permission_name VARCHAR(50) NOT NULL,
    granted_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

**Procedimento para Adicionar**:
1. Adicionar SQL em cada arquivo `docker/{dialect}/init.sql`
2. Recriar containers: `docker-compose down -v && docker-compose up -d`
3. Validar com query manual contra cada banco
4. Criar exemplos correspondentes em SqlServerExamples.cs, PostgreSQLExamples.cs, MySQLExamples.cs
5. Atualizar esta seção com referências

**Referência de Arquivos Reais**:
- [docker/sql-server/init.sql](docker/sql-server/init.sql) - Implementação SQL Server
- [docker/postgresql/init.sql](docker/postgresql/init.sql) - Implementação PostgreSQL
- [docker/mysql/init.sql](docker/mysql/init.sql) - Implementação MySQL

---

---

### Fase 1: Foundation - SQL Detection & Extraction (Semana 1-2)
- ✅ SqlStringDetector com regex keyword-based
- ✅ SqlStringMatch records
- ✅ Unit tests para detecção (10+ patterns)
- ✅ Performance benchmark

**Deliverable**: Detector robusto que encontra 95%+ das SQLs em código real

---

### Fase 2: Parsing (Semana 3-4)
- ✅ Expandir SqlParserAdapter.ParseToAst() interface
- ✅ SqlServerParserAdapter.ParseToAst() com T-SQL normalization
- ✅ PostgreSqlParserAdapter.ParseToAst()
- ✅ MySqlParserAdapter.ParseToAst()
- ✅ Unit tests parsing (20+ queries por dialecto)
- ✅ Detecção de construtos não-translatáveis

**Deliverable**: Parsing agnóstico funcional para queries comuns

---

### Fase 3: Transformation & Rewriting (Semana 5-6)
- ✅ SqlTransformationEngine (reutilizar ISqlTranslator + ISqlCompiler)
- ✅ CSharpFileRewriter com preservação de formatting
- ✅ Backup mechanism
- ✅ Encoding/line-ending detection
- ✅ Unit tests rewriting (5+ scenarios)

**Deliverable**: End-to-end file transformation (detectar → transformar → reescrever)

---

### Fase 4: Reporting & CLI (Semana 7-8)
- ✅ TransformationReporter (JSON + Markdown)
- ✅ HtmlReportGenerator (HTML + Three.js + D3.js)
- ✅ TranslateFilesCommand com option parsing
- ✅ Dry-run mode
- ✅ Progress display
- ✅ Integration tests (Docker databases)
- ✅ User documentation

**Deliverable**: Production-ready CLI command com reporting em 3 formatos (JSON, Markdown, HTML interativo)

---

### Fase 5: Polish & Optimization (Semana 9)
- ✅ Performance optimization (parallel processing)
- ✅ Error handling robustness
- ✅ Edge case coverage
- ✅ Security review
- ✅ End-to-end testing com projetos reais

**Deliverable**: v3.0 stable release

---

## 🧪 Teste Strategy

### 🐳 Setup Docker para Testes com Banco de Dados Real

**Pré-requisitos**:
- Docker e Docker Compose instalados
- Mínimo 4 GB RAM disponível
- Portas 5432 (PostgreSQL), 3306 (MySQL), 1433 (SQL Server) livres

**Estrutura de Inicialização**:
- Cada dialeto tem seu arquivo `init.sql` que cria schema + insere dados automáticamente
- SQL Server: `docker/sql-server/init.sql` → cria banco `DialectSamples` + 4 tabelas + 22 registros
- PostgreSQL: `docker/postgresql/init.sql` → cria banco `dialect_samples` + 4 tabelas + 22 registros
- MySQL: `docker/mysql/init.sql` → cria banco `dialect_samples` + 4 tabelas + 22 registros

**Iniciar Ambiente de Testes**:
```bash
# 1. Navegar para pasta docker
cd Dialect/docker

# 2. Iniciar todos os bancos de dados (pulls imagens + cria containers + executa init.sql)
docker-compose up -d

# 3. Aguardar inicialização (~30-60 segundos)
docker-compose logs -f

# 4. Quando ver "DialectSamples" e "dialect_samples ready", pressionar Ctrl+C

# 5. Validar que containers estão rodando
docker-compose ps
# Deve mostrar: postgresql, mysql, sqlserver (STATUS: Up)

# 6. Opcional - testar via CLI quando estiver implementado
dialect translate-files --path ./samples/Dialect.Samples/07_CLI \
  --source sqlserver --target postgresql --check-only --verbose
```

**Verificar Dados no Banco de Dados**:

PostgreSQL:
```bash
docker exec -it dialect-postgres psql -U postgres -d dialect_samples -c "\d"  # listar tabelas
docker exec -it dialect-postgres psql -U postgres -d dialect_samples -c "SELECT * FROM \"Users\";"
```

MySQL:
```bash
docker exec -it dialect-mysql mysql -u root dialect_samples -e "SHOW TABLES;"
docker exec -it dialect-mysql mysql -u root dialect_samples -e "SELECT * FROM Users;"
```

SQL Server:
```bash
docker exec -it dialect-sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P YourPassword123! -d DialectSamples -Q "SELECT * FROM Users;"
```

**Parar/Limpar Ambiente**:
```bash
# Parar containers (manter dados)
docker-compose stop

# Parar e remover containers (manter volumes)
docker-compose down

# Parar, remover containers E volumes (resetar dados)
docker-compose down -v
```

**Troubleshooting**:
- **SQL Server não inicia**: Aumentar RAM Docker para 4GB+ (Docker Desktop → Settings → Resources → Memory)
- **Porta já em uso**: Editar `docker-compose.yml`, mudar `5432:5432` para `5433:5432` (mantém target, muda host)
- **Dados não aparecem**: Verificar logs com `docker-compose logs sqlserver` (ou postgresql/mysql)
- **Permissão negada no Linux**: Usar `sudo docker-compose up -d`
- **Container crash loop**: Verificar `docker-compose logs <service>` para mensagens de erro

**Conexão Programática** (para testes de integração):

SQL Server:
```csharp
var connString = "Server=localhost,1433;Database=DialectSamples;User Id=sa;Password=YourPassword123!;";
using var conn = new SqlConnection(connString);
conn.Open();
```

PostgreSQL:
```csharp
var connString = "Host=localhost;Port=5432;Database=dialect_samples;Username=postgres;Password=postgres;";
using var conn = new NpgsqlConnection(connString);
conn.Open();
```

MySQL:
```csharp
var connString = "Server=localhost;Port=3306;Database=dialect_samples;Uid=root;Pwd=password;";
using var conn = new MySqlConnection(connString);
conn.Open();
```

---

### Testes Unitários

**SqlStringDetector Tests**:
```csharp
✅ Verbatim string detection
✅ Normal string detection
✅ Method call detection
✅ Variable assignment detection
✅ Multi-line collapsing
✅ Confidence scoring
✅ False positive filtering
✅ Edge cases (empty, very short, no keywords)
```

**SqlTransformationEngine Tests**:
```csharp
✅ SQL Server → PostgreSQL (SELECT, INSERT, UPDATE, DELETE)
✅ PostgreSQL → MySQL (6 statements)
✅ MySQL → SQL Server (6 statements)
✅ Bidirectional (A→B→A produces same result)
✅ Untranslatable detection
✅ Error handling (parse failures)
```

**CSharpFileRewriter Tests**:
```csharp
✅ Preserve @"" strings
✅ Preserve "normal" strings
✅ Preserve indentation
✅ Preserve encoding
✅ Preserve line endings (CRLF/LF)
✅ Multi-match rewriting
✅ Backup creation
```

### Testes de Integração

**Cross-Dialect Tests**:
```csharp
✅ SQL Server (T-SQL) → PostgreSQL → MySQL → SQL Server (round-trip)
✅ Verify output matches expected render
✅ Compare with Dialect.Samples compiled output
```

**Real-world Project Tests**:
```csharp
✅ Run against Dialect.Samples project
✅ Run against sample e-commerce codebase
✅ Measure performance (100+ files, 500+ SQLs)
```

---

## 📚 Dependências

**Existentes** (já em projeto):
- ISqlTranslator, ISqlCompiler (reutilizar)
- SqlParserAdapter (expandir)
- Query Renderers (SQL Server, PostgreSQL, MySQL)
- AST types (SelectStatement, etc.)

**Novas** (adicionar):
- System.Text.RegularExpressions (built-in)
- Newtonsoft.Json ou System.Text.Json (built-in)
- System.IO, System.Linq (built-in)

**Frontend Dependencies** (embutidas no HTML report via CDN):
- Three.js (3D visualization)
- D3.js ou Chart.js (statistical charts)
- Bootstrap 5 (responsiveness)
- highlight.js (SQL syntax highlighting)

**Nota**: Sem dependências externas .NET — apenas built-ins. JavaScript libraries via CDN (sem npm)

---

## � Configuração Docker Compose Detalhada

**Arquivo**: [docker/docker-compose.yml](docker/docker-compose.yml)

**Serviços Definidos**:

### SQL Server 2022
```yaml
container_name: dialect-sqlserver
image: mcr.microsoft.com/mssql/server:2022-latest
port: 1433:1433
credentials: SA_PASSWORD="P@ssw0rd!"
database: DialectSamples
volumes: sqlserver-data-local (dados persistentes)
healthcheck: sqlcmd select 1
init_script: sql-server/init.sql (cria schema + 4 tabelas + 22 registros)
```

### PostgreSQL 15
```yaml
container_name: dialect-postgresql
image: postgres:15-alpine
port: 5432:5432
credentials: POSTGRES_USER=postgres, POSTGRES_PASSWORD=postgres
database: dialect_samples
volumes: postgresql-data (dados persistentes) + postgresql/init.sql
healthcheck: pg_isready -U postgres
init_script: postgresql/init.sql (cria schema + 4 tabelas + 22 registros)
```

### MySQL 8.0
```yaml
container_name: dialect-mysql
image: mysql:8.0
port: 3306:3306
credentials: MYSQL_ROOT_PASSWORD=root
database: dialect_samples
volumes: mysql-data (dados persistentes) + mysql/init.sql
healthcheck: mysqladmin ping -h localhost -u root -proot
init_script: mysql/init.sql (cria schema + 4 tabelas + 22 registros)
```

**Network**:
- `dialect-network` (bridge) — conecta os 3 serviços

**Volumes**:
- `sqlserver-data-local/` — SQL Server data files
- `postgresql-data/` — PostgreSQL volume
- `mysql-data/` — MySQL volume

**Scripts de Inicialização** (executados automaticamente na primeira vez):
- [docker/sql-server/init.sql](docker/sql-server/init.sql) → Cria banco DialectSamples + Users, Products, Orders, OrderItems
- [docker/postgresql/init.sql](docker/postgresql/init.sql) → Cria banco dialect_samples + tabelas com snake_case
- [docker/mysql/init.sql](docker/mysql/init.sql) → Cria banco dialect_samples + tabelas com snake_case

**Comandos Úteis**:

```bash
# Iniciar todos os serviços
docker-compose up -d

# Ver logs (exit com Ctrl+C)
docker-compose logs -f

# Ver status dos containers
docker-compose ps

# Entrar em container PostgreSQL
docker-compose exec postgresql psql -U postgres -d dialect_samples

# Entrar em container MySQL
docker-compose exec mysql mysql -u root -proot dialect_samples

# Entrar em container SQL Server
docker-compose exec sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P P@ssw0rd!

# Parar e remover volumes (reset completo)
docker-compose down -v
```

**Health Checks**:
- Cada serviço tem healthcheck automático
- Verifica a cada 10s se o banco está respondendo
- Máximo 5 tentativas, timeout 5s
- Útil para CI/CD (esperar até que todos estejam ready)

---

## �🚫 Fora de Escopo (v3.0)

- ❌ CLR-based Stored Procedures (SQL Server específico, não SQL puro)
- ❌ Triggers (lógica muito dialecto-específica, temporal)
- ❌ Advanced cursors com loops complexos (performance concerns)
- ❌ GUI/Web interface (CLI apenas)
- ❌ Real-time execution validation (análise estática apenas)
- ❌ Database-agnostic abstractions (FluentBuilder exists, não é objetivo)
- ❌ ORM integration (Dapper, EF Core — out of scope)
- ❌ Version detection (estrutura existe, não está pronta)

---

## ✅ Critérios de Aceitação

### Functional AC

- [ ] CLI comando `translate-files` compila e executa sem erros
- [ ] Detecta 95%+ de SQL strings em código real (.cs files)
- [ ] Parseia SELECT/INSERT/UPDATE/DELETE com sucesso
- [ ] Parseia e transforma CREATE PROCEDURE/CREATE FUNCTION com sucesso
- [ ] Transforma bidirecionalmente (todas 9 combinações)
- [ ] Reescreve arquivos com SQL transformado
- [ ] Preserva formatting, encoding, line endings
- [ ] Gera relatório JSON com detalhes
- [ ] Gera relatório Markdown formatado
- [ ] Gera relatório HTML interativo com Three.js visualization
- [ ] HTML report mostra arquivos + método convertidos (antes/depois)
- [ ] HTML report mostra arquivos + método com erros e motivos
- [ ] HTML report permite filtrar por status/tipo de SQL (SELECT, INSERT, UPDATE, DELETE, PROCEDURE, FUNCTION)
- [ ] HTML report permite busca em tempo real
- [ ] Visualização 3D interativa com zoom/pan/rotação
- [ ] Gráficos estatísticos (Chart.js ou D3.js)
- [ ] Dry-run mode funciona (preview sem escrever)
- [ ] Backups criados com --include-backups

### Non-Functional AC

- [ ] Performance: < 30s para 100 arquivos, 500 SQLs
- [ ] HTML report gerado em < 5s
- [ ] Cobertura de testes: > 80%
- [ ] Sem warnings de build
- [ ] Documentação completa (README, exemplos)
- [ ] HTML report responsivo (desktop, tablet, mobile)
- [ ] Tema claro/escuro com persistência em localStorage
- [ ] Paleta de cores consistente com docs/documentation.html

---

## 📖 Documentação Necessária

1. **README.md** (adicionar seção ao README existente)
   - Overview, use cases, installation, quick start

2. **CLI Help** (in-code)
   - `dialect translate-files --help`

3. **User Guide** (markdown)
   - Step-by-step de migração
   - Troubleshooting comum
   - Limitações conhecidas

4. **API Docs** (code comments)
   - Javadoc/XML comments em todas classes públicas

5. **Architecture Doc** (markdown)
   - Diagramas de componentes
   - Flow chart de parsing/transformation
   - Design decisions

---

## 🎓 Apêndice: Estratégia de Detecção com Palavras-Chave

### Fase 1: Captura de Candidatos (Regex Genérico)

**Objetivo**: Encontrar todas as strings que *poderiam* ser SQL

#### Pattern 1: Verbatim String
```regex
@"([^"]+(?:""[^"]*)*?)"
```
Matches: `@"SELECT * FROM Users"`, `@"DELETE FROM Orders"`

#### Pattern 2: Normal String (single-line)
```regex
"([^"\\]*(?:\\.[^"\\]*)*)"
```
Matches: `"SELECT * FROM Users"`, `"INSERT INTO Products ..."`

#### Pattern 3: Method Call com SQL
```regex
\.(Execute|Query|ExecuteScalar|ExecuteReader|ExecuteSql|QueryAsync)\s*\(\s*@?"([^"]*?)"
```
Matches: `.Execute("SELECT ...")`, `.Query(@"INSERT ...")`, `.ExecuteAsync("DELETE ...")`

#### Pattern 4: Variable Assignment
```regex
(?:var|string)\s+\w+\s*=\s*@?"([^"]*?)"
```
Matches: `var sql = "SELECT ..."`, `string query = @"INSERT ..."`

---

### Fase 2: Validação com Palavras-Chave SQL (Confidence Scoring)

**Objetivo**: Filtrar candidatos, validar que são realmente SQL

**Palavras-chave Primárias** (exatamente 1 obrigatória):
- `SELECT`
- `INSERT`
- `UPDATE`
- `DELETE`
- `WITH` (CTEs)
- `MERGE`
- `EXEC` / `EXECUTE` / `CALL`

**Palavras-chave Secundárias** (pelo menos 1 recomendada):
- `FROM` (SELECT/DELETE)
- `WHERE` (SELECT/UPDATE/DELETE)
- `INTO` (INSERT)
- `VALUES` (INSERT)
- `SET` (UPDATE)
- `JOIN` (SELECT/UPDATE/DELETE)
- `GROUP BY` / `ORDER BY` / `HAVING` (SELECT)
- `AND` / `OR` / `IN` / `LIKE` (WHERE conditions)
- `UNION` / `EXCEPT` / `INTERSECT` (SELECT combinations)

**Cálculo de Confidence Score**:
```
score = 0

// Primária: +60 pontos (obrigatória)
if (has_primary_keyword) score += 60
else return score = 0  // Rejeitar

// Secundária: +20 pontos por keyword
for each secondary_keyword_found:
    score += 20 (max 40)

// Tamanho: +0 se < 20 chars, +10 se >= 20
if (length >= 20) score += 10

// Filtro: Rejeitar se contém padrões não-SQL
if (contains("class ", "namespace ", "using ", "static ")) 
    score = 0

// Resultado final
final_score = min(score, 100)
confidence = "High" if score >= 80
           = "Medium" if score >= 50
           = "Low" if score < 50
           = "Reject" if score == 0
```

---

### Exemplos de Scoring

| Texto | Primária | Secundárias | Score | Decisão |
|-------|----------|-------------|-------|---------|
| `"SELECT * FROM Users"` | SELECT (+60) | FROM (+20) | 90 | ✅ High |
| `"SELECT TOP 10 * FROM Users WHERE Id > 5"` | SELECT (+60) | FROM, WHERE (+40) | 100 | ✅ High |
| `"INSERT INTO Orders VALUES (@id, @date)"` | INSERT (+60) | INTO, VALUES (+40) | 100 | ✅ High |
| `"UPDATE Users SET Status = 'Active' WHERE Id = 1"` | UPDATE (+60) | SET, WHERE (+40) | 100 | ✅ High |
| `"DELETE FROM Logs WHERE Date < NOW()"` | DELETE (+60) | FROM, WHERE (+40) | 100 | ✅ High |
| `"var x = "Hello World"` | — | — | 0 | ❌ Reject |
| `"SELECT COUNT(*)"` | SELECT (+60) | — | 70 | ⚠️ Medium |
| `"User SELECT * FROM Users"` | SELECT (+60) | FROM (+20) | 80 | ✅ High (falso positivo detectado, mas seguro) |
| `"public class SelectUser"` | SELECT (em class name) | — | 0 | ❌ Reject (contém "class") |
| `"throw new Exception("SELECT ERROR")"` | — (em string erro) | — | 0 | ❌ Reject |

---

### Recomendação: Usar Score ≥ 75 como Threshold

- **≥ 75**: Processar automaticamente
- **50-74**: Marcar como "Manual Review" no relatório
- **< 50**: Ignorar (baixa confiança)

**Benefício**: Reduz false positives, melhora recall em codebases reais

**FIM DA ESPECIFICAÇÃO**
