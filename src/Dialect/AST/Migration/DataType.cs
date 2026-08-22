namespace Dialect.Core.AST.Migration;

/// <summary>
/// Represents SQL data types for column definitions in migrations.
/// </summary>
public enum DataType
{
    // Numeric types
    SmallInt,      // SMALLINT
    Int,           // INT / INTEGER
    BigInt,        // BIGINT
    Decimal,       // DECIMAL / NUMERIC (requires precision, scale)
    Money,         // MONEY (SQL Server specific)

    // Floating point
    Float,         // FLOAT
    Double,        // DOUBLE PRECISION (PostgreSQL) / DOUBLE (MySQL)

    // String types
    Char,          // CHAR(n)
    Varchar,       // VARCHAR(n)
    Text,          // TEXT (unlimited)
    NChar,         // NCHAR(n) - Unicode (SQL Server)
    NVarchar,      // NVARCHAR(n) - Unicode (SQL Server)

    // Date/Time types
    Date,          // DATE
    Time,          // TIME
    DateTime,      // DATETIME / TIMESTAMP
    DateTime2,     // DATETIME2 (SQL Server)
    SmallDateTime, // SMALLDATETIME (SQL Server)
    Timestamp,     // TIMESTAMP (MySQL) / TIMESTAMPTZ (PostgreSQL)

    // Boolean
    Boolean,       // BOOLEAN / BIT (SQL Server) / BOOL (MySQL)

    // Binary
    Varbinary,     // VARBINARY(n)
    Binary,        // BINARY(n)

    // Special types
    Uuid,          // UUID (PostgreSQL) / UNIQUEIDENTIFIER (SQL Server) / CHAR(36) (MySQL)
    Json,          // JSON (MySQL/PostgreSQL) / NVARCHAR(MAX) (SQL Server)
    Xml,           // XML (SQL Server)
}
