# AST DML Coverage Plan — 100% sem Fallback

## Objetivo
Implementar parser AST que cobre **todos os padrões DML** entre SQL Server, PostgreSQL, MySQL **sem recair em fallback regex**.

Abordagem: **SQL → [Parser AST] → SIM (sempre consegue) → Compila para SQL alvo**

---

## 1. SELECT (Casos de Uso)

### 1.1 SELECT Simples ✅ (já cobre)
- `SELECT * FROM Users`
- `SELECT Id, Name FROM Users`
- `SELECT TOP 10 * FROM Users` (SQL Server)
- `SELECT * FROM Users LIMIT 10` (PostgreSQL, MySQL)
- `SELECT DISTINCT Name FROM Users`

### 1.2 SELECT com WHERE
- `SELECT * FROM Users WHERE Id = 1`
- `SELECT * FROM Users WHERE Status = 'Active' AND Id > 5`
- `SELECT * FROM Users WHERE Name LIKE 'A%' OR Email IS NULL`
- `SELECT * FROM Users WHERE Id IN (1, 2, 3)`
- `SELECT * FROM Users WHERE Age BETWEEN 18 AND 65`

### 1.3 SELECT com JOIN
- `SELECT u.Id, o.OrderId FROM Users u INNER JOIN Orders o ON u.Id = o.UserId`
- `SELECT * FROM Users LEFT JOIN Orders ON Users.Id = Orders.UserId`
- `SELECT * FROM Users RIGHT JOIN Orders ON Users.Id = Orders.UserId`
- `SELECT * FROM Orders WHERE OrderId IN (SELECT OrderId FROM OrderDetails WHERE Qty > 10)` (Subquery)

### 1.4 SELECT com GROUP BY / HAVING
- `SELECT Dept, COUNT(*) as Total FROM Users GROUP BY Dept`
- `SELECT Dept, COUNT(*) as Total FROM Users GROUP BY Dept HAVING COUNT(*) > 5`

### 1.5 SELECT com ORDER BY / LIMIT+OFFSET
- `SELECT * FROM Users ORDER BY Name ASC`
- `SELECT * FROM Users ORDER BY Name DESC, Id ASC`
- `SELECT * FROM Users ORDER BY Name LIMIT 10`
- `SELECT * FROM Users ORDER BY Name LIMIT 10 OFFSET 5`

### 1.6 SELECT com Window Functions
- `SELECT Id, Name, ROW_NUMBER() OVER (PARTITION BY Dept ORDER BY Salary DESC) as rank FROM Users`
- `SELECT Id, SUM(Amount) OVER (PARTITION BY UserId) as Total FROM Orders`

### 1.7 SELECT com CTE (WITH clause)
- `WITH UserStats AS (SELECT UserId, COUNT(*) as Orders FROM Orders GROUP BY UserId) SELECT * FROM UserStats WHERE Orders > 5`

### 1.8 SELECT Compound (UNION, INTERSECT, EXCEPT)
- `SELECT Id FROM Users UNION SELECT EmployeeId FROM Employees`
- `SELECT Id FROM Table1 INTERSECT SELECT Id FROM Table2`
- `SELECT Id FROM Table1 EXCEPT SELECT Id FROM Table2`

---

## 2. INSERT

### 2.1 INSERT básico
- `INSERT INTO Users (Id, Name) VALUES (1, 'Ana')`

### 2.2 INSERT múltiplas linhas
- `INSERT INTO Users (Id, Name) VALUES (1, 'Ana'), (2, 'Bob'), (3, 'Carol')`

### 2.3 INSERT ... SELECT
- `INSERT INTO Users_Backup SELECT * FROM Users WHERE Status = 'Inactive'`
- `INSERT INTO Users (Id, Name) SELECT EmployeeId, EmployeeName FROM Employees WHERE Dept = 'Sales'`

### 2.4 INSERT com RETURNING (PostgreSQL)
- `INSERT INTO Users (Id, Name) VALUES (1, 'Ana') RETURNING Id`

### 2.5 INSERT com OUTPUT (SQL Server)
- `INSERT INTO Users (Id, Name) VALUES (1, 'Ana') OUTPUT INSERTED.*`

### 2.6 INSERT com ON DUPLICATE KEY UPDATE (MySQL)
- `INSERT INTO Users (Id, Name) VALUES (1, 'Ana') ON DUPLICATE KEY UPDATE Name = 'Ana Updated'`

### 2.7 INSERT com ON CONFLICT (PostgreSQL)
- `INSERT INTO Users (Id, Name) VALUES (1, 'Ana') ON CONFLICT (Id) DO UPDATE SET Name = 'Ana Updated'`

---

## 3. UPDATE

### 3.1 UPDATE básico
- `UPDATE Users SET Name = 'Bob' WHERE Id = 1`

### 3.2 UPDATE múltiplas colunas
- `UPDATE Users SET Name = 'Bob', Status = 'Active' WHERE Id = 1`

### 3.3 UPDATE com JOIN (SQL Server / PostgreSQL)
- `UPDATE u SET u.Status = 'Active' FROM Users u INNER JOIN Orders o ON u.Id = o.UserId WHERE o.Amount > 1000`

### 3.4 UPDATE com subquery
- `UPDATE Users SET Status = 'VIP' WHERE Id IN (SELECT UserId FROM Orders WHERE Amount > 5000)`

### 3.5 UPDATE com RETURNING (PostgreSQL)
- `UPDATE Users SET Name = 'Bob' WHERE Id = 1 RETURNING *`

### 3.6 UPDATE com OUTPUT (SQL Server)
- `UPDATE Users SET Name = 'Bob' WHERE Id = 1 OUTPUT INSERTED.*, DELETED.*`

---

## 4. DELETE

### 4.1 DELETE básico
- `DELETE FROM Users WHERE Id = 1`

### 4.2 DELETE com JOIN (SQL Server / PostgreSQL)
- `DELETE u FROM Users u INNER JOIN Orders o ON u.Id = o.UserId WHERE o.Status = 'Cancelled'`

### 4.3 DELETE com subquery
- `DELETE FROM Users WHERE Id IN (SELECT UserId FROM Orders WHERE Status = 'Cancelled')`

### 4.4 DELETE com RETURNING (PostgreSQL)
- `DELETE FROM Users WHERE Id = 1 RETURNING *`

### 4.5 DELETE com OUTPUT (SQL Server)
- `DELETE FROM Users WHERE Id = 1 OUTPUT DELETED.*`

### 4.6 DELETE com WHERE complexo
- `DELETE FROM Users WHERE (Status = 'Inactive' OR LastLogin < '2020-01-01') AND Dept != 'Admin'`

---

## 5. UPSERT (Merge)

### 5.1 PostgreSQL: INSERT ... ON CONFLICT
- `INSERT INTO Users (Id, Name) VALUES (1, 'Ana') ON CONFLICT (Id) DO UPDATE SET Name = 'Ana Updated'`

### 5.2 MySQL: INSERT ... ON DUPLICATE KEY UPDATE
- `INSERT INTO Users (Id, Name) VALUES (1, 'Ana') ON DUPLICATE KEY UPDATE Name = 'Ana Updated'`

### 5.3 SQL Server: MERGE
- `MERGE INTO Users t USING (VALUES (1, 'Ana')) s(Id, Name) ON t.Id = s.Id WHEN MATCHED THEN UPDATE SET Name = s.Name WHEN NOT MATCHED THEN INSERT (Id, Name) VALUES (s.Id, s.Name)`

---

## Estratégia de Implementação

### Fase 1: Expandir SimpleDmlAstParser (Semanas 1-2)
- ✅ SELECT simples (já feito)
- [ ] SELECT com JOIN simples
- [ ] SELECT com WHERE complexo (AND/OR/BETWEEN/IN combinado)
- [ ] INSERT (simples e múltiplas linhas)
- [ ] UPDATE (básico e com WHERE complexo)
- [ ] DELETE (básico e com WHERE complexo)

### Fase 2: Suporte Avançado SELECT (Semana 3)
- [ ] GROUP BY / HAVING
- [ ] Window Functions (ROW_NUMBER, RANK, SUM OVER, etc.)
- [ ] CTE / WITH clause
- [ ] Subqueries (SELECT dentro de WHERE/FROM/SELECT)
- [ ] UNION / INTERSECT / EXCEPT

### Fase 3: Suporte Avançado DML (Semana 4)
- [ ] INSERT ... SELECT
- [ ] INSERT ... ON CONFLICT / ON DUPLICATE KEY / OUTPUT
- [ ] UPDATE ... JOIN (SQL Server / PostgreSQL)
- [ ] UPDATE ... RETURNING / OUTPUT
- [ ] DELETE ... JOIN
- [ ] DELETE ... RETURNING / OUTPUT
- [ ] MERGE / UPSERT

### Fase 4: Validação e Refinamento (Semana 5)
- [ ] Teste de matriz: todos os pares (SqlServer → PostgreSQL, etc.)
- [ ] Teste de cenários "edge": strings com aspas, identifiers especiais, etc.
- [ ] Remover fallback regex gradualmente (apenas para DDL)
- [ ] Documentação de suporte

---

## Estrutura de Código

### SimpleDmlAstParser.cs (ExpandirSerá o nucleus)
Métodos por tipo de statement:
- `ParseSelect()` — SELECT com todas variações
- `ParseInsert()` — INSERT
- `ParseUpdate()` — UPDATE
- `ParseDelete()` — DELETE
- `ParseMerge()` — MERGE (SQL Server)

Cada um que recebe `SqlProvider` e retorna AST tipado (`SelectStatement`, `InsertStatement`, etc.).

### DefaultSqlTranslator.cs
- `CompileAst()` já escolhe o tipo de statement e chama `Compile(targetDialect)`
- Sem fallback — se AST falhar a parsear, é um erro, não é silenciosamente redirecionado para regex

### Testes
- Matriz DML: todos os pares de dialetos × casos de uso
- Golden files: exemplos reais de cada padrão

---

## Critério de Sucesso
1. **Nenhum fallback regex para DML**: TODO comando DML é parseado com AST
2. **100% de cobertura de matriz**: Todos os pares (3 dialetos × 3 dialetos = 9 pares) × todos os casos DML
3. **Sem regressão**: Testes existentes continuam passando
4. **Suporte multi-dialeto**: INSERT/UPDATE/DELETE funciona de SqlServer → PostgreSQL → MySQL e vice-versa

---

## Próximos Passos Imediatos
1. Revisar `SimpleDmlAstParser` para ampliar `ParseUpdate()` e `ParseDelete()` com WHERE complexo
2. Adicionar suporte a `ParseInsert()` com múltiplas linhas
3. Executar testes de matriz para identificar gaps
4. Iterar parser até todo padrão passar
