namespace Dialect.Tests.Schema;

using Dialect.Core.Schema;
using Dialect.Core.AST.Migration;
using Dialect.SqlServer;
using Xunit;

/// <summary>
/// Tests for schema validation across all dialects.
/// Phase 5 comprehensive schema validation testing.
/// </summary>
public class SchemaValidatorTests
{
    [Fact]
    public void ValidateColumnName_ValidPascalCase_ReturnsSuccess()
    {
        // Arrange
        var validator = new SqlServerDialect().CreateSchemaValidator();

        // Act
        var result = ValidationRules.ValidateColumnName("UserId", NamingConvention.PascalCase);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateColumnName_EmptyName_ReturnsError()
    {
        // Act
        var result = ValidationRules.ValidateColumnName("", NamingConvention.Any);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
        Assert.Contains("empty", result.Errors[0].Message.ToLower());
    }

    [Fact]
    public void ValidateIdentifierLength_ExceedsLimit_ReturnsError()
    {
        // Arrange
        var longName = new string('a', 65);

        // Act
        var result = ValidationRules.ValidateIdentifierLength(longName, 64);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }

    [Fact]
    public void ValidateStringTypeLength_VarcharWithoutLength_ReturnsError()
    {
        // Act
        var result = ValidationRules.ValidateStringTypeLength(DataType.Varchar, null);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateAutoIncrementNotNullable_NullableAutoIncrement_ReturnsError()
    {
        // Arrange
        var column = new ColumnDef("id", DataType.Int, Nullable: true, DefaultValue: null, IsAutoIncrement: true, IsPrimaryKey: false);

        // Act
        var result = ValidationRules.ValidateAutoIncrementNotNullable(column);

        // Assert
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateMigration_ValidCreateTable_ReturnsSuccess()
    {
        // Arrange
        var columns = new[]
        {
            new ColumnDef("id", DataType.BigInt, Nullable: false, DefaultValue: null, IsAutoIncrement: true, IsPrimaryKey: true),
            new ColumnDef("name", DataType.Varchar, Nullable: false, DefaultValue: null, IsAutoIncrement: false, IsPrimaryKey: false, Length: 255)
        };

        var step = new CreateTableStep("Users", columns, new[] { "id" });
        var migration = new Migration("001_CreateUsers", "1.0.0", DateTime.UtcNow, new[] { step });

        var validator = new SqlServerDialect().CreateSchemaValidator();

        // Act
        var result = validator.Validate(migration, new SqlServerDialect());

        // Assert
        Assert.True(result.IsValid);
    }
}
