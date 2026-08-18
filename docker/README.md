# Dialect Framework - Docker Database Services

This directory contains Docker configurations for running Dialect Framework samples against real database instances.

## Quick Start

### 1. Install Docker

Download and install [Docker Desktop](https://www.docker.com/products/docker-desktop) for your platform.

### 2. Start All Services

From the project root directory:

```bash
docker-compose up -d
```

This will start three containerized databases:
- **SQL Server 2022** - Port 1433
- **PostgreSQL 15** - Port 5432
- **MySQL 8.0** - Port 3306

### 3. Verify Services Are Running

```bash
docker-compose ps
```

You should see all 3 services with status `Up` and healthy.

## Database Details

### Connection Strings

**SQL Server:**
```
Server=localhost,1433;Database=DialectSamples;User Id=sa;Password=P@ssw0rd!;TrustServerCertificate=true;
```

**PostgreSQL:**
```
Host=localhost;Port=5432;Database=dialect_samples;Username=postgres;Password=postgres;
```

**MySQL:**
```
Server=localhost;Port=3306;Database=dialect_samples;Uid=root;Pwd=root;
```

### Sample Schema

All three databases contain identical schemas:

**Users Table**
```sql
CREATE TABLE Users (
    UserId INT PRIMARY KEY,
    Username VARCHAR(100) NOT NULL,
    Email VARCHAR(100) NOT NULL UNIQUE,
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);
```

**Products Table**
```sql
CREATE TABLE Products (
    ProductId INT PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    Price DECIMAL(10, 2) NOT NULL,
    StockQuantity INT DEFAULT 0
);
```

**Orders Table**
```sql
CREATE TABLE Orders (
    OrderId INT PRIMARY KEY,
    UserId INT NOT NULL FOREIGN KEY,
    OrderDate TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    Total DECIMAL(10, 2) NOT NULL
);
```

**OrderItems Table**
```sql
CREATE TABLE OrderItems (
    OrderItemId INT PRIMARY KEY,
    OrderId INT NOT NULL FOREIGN KEY,
    ProductId INT NOT NULL FOREIGN KEY,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(10, 2) NOT NULL
);
```

### Sample Data

**Users (4 records)**
- john_doe (john@example.com)
- jane_smith (jane@example.com)
- bob_wilson (bob@example.com)
- alice_johnson (alice@example.com)

**Products (5 records)**
- Laptop ($999.99)
- Mouse ($29.99)
- Keyboard ($79.99)
- Monitor ($299.99)
- Headphones ($149.99)

**Orders (5 records with related OrderItems)**
- Populated with realistic order data

## Health Checks

Each service includes automatic health checks:

```bash
docker-compose ps
```

Services automatically wait for health checks to pass before becoming available.

## Initialization Scripts

Each database runs an initialization script to create the schema and populate sample data:

- `/docker/sql-server/init.sql` - SQL Server specific DDL
- `/docker/postgresql/init.sql` - PostgreSQL specific DDL
- `/docker/mysql/init.sql` - MySQL specific DDL

## Data Persistence

Data is persisted in Docker volumes:

```yaml
volumes:
  sqlserver-data:
  postgresql-data:
  mysql-data:
```

This means data persists even if containers are stopped and restarted.

## Common Commands

### Stop Services (Keep Data)
```bash
docker-compose stop
```

### Start Services (Existing Data)
```bash
docker-compose start
```

### Stop and Remove Data
```bash
docker-compose down -v
```

### View Logs
```bash
docker-compose logs [service-name]
```

### Connect to a Specific Database

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

## Troubleshooting

### Containers Won't Start

**Problem:** `docker-compose up -d` fails or containers exit immediately

**Solutions:**
1. Ensure Docker daemon is running
2. Check available disk space: `docker system df`
3. Check for port conflicts: `netstat -an | grep 1433` (for SQL Server), etc.
4. View error logs: `docker-compose logs`

### Can't Connect to Database

**Problem:** Connection refused after containers start

**Solutions:**
1. Wait for health checks to pass: `docker-compose ps` should show `healthy`
2. Verify container is still running: `docker ps`
3. Check firewall settings blocking ports
4. Verify connection string (copy from above)

### Initialization Scripts Didn't Run

**Problem:** Database is empty or schema wasn't created

**Solutions:**
1. Verify `.sql` files exist in `/docker` subdirectories
2. Check docker logs: `docker-compose logs [service-name]`
3. Stop and reset: `docker-compose down -v && docker-compose up -d`
4. Wait ~30 seconds for initialization to complete

### Database Corrupted or Data Lost

**Problem:** Data appears corrupted or tables missing

**Solutions:**
1. Reset database (WARNING: deletes all data):
   ```bash
   docker-compose down -v
   docker-compose up -d
   ```
2. This removes volumes and recreates clean schema with sample data

### Port Already in Use

**Problem:** `Address already in use` error

**Solutions:**
1. Find what's using the port: `netstat -ano | findstr :1433` (Windows)
2. Stop the conflicting service or change docker-compose port mapping
3. Edit `docker-compose.yml` to use different external port:
   ```yaml
   sqlserver:
     ports:
       - "1434:1433"  # Changed from 1433
   ```

## Performance Notes

- Containers are configured with reasonable defaults but can be tuned
- For large datasets, consider adding resource limits to `docker-compose.yml`
- Health checks wait up to 50 seconds (10 retries × 5s timeout) for services to be ready
- First startup takes longer due to database initialization

## Advanced Configuration

### Enable Persistent Logging

Edit `docker-compose.yml` to add environment variables:

```yaml
sqlserver:
  environment:
    SA_PASSWORD: "P@ssw0rd!"
    ACCEPT_EULA: "Y"
    MSSQL_PID: "Developer"  # Use Developer edition features
```

### Increase Resource Limits

Add to `docker-compose.yml` services:

```yaml
services:
  sqlserver:
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 4G
```

### Bind to Specific Network Interface

Edit `docker-compose.yml` port mappings:

```yaml
ports:
  - "localhost:1433:1433"  # Only accept local connections
```

## Clean Up

Remove everything (containers, volumes, networks):

```bash
docker-compose down -v
docker system prune -a
```

This is useful if you need to free up disk space or start fresh.
