namespace Dialect.Tests.Cli;

using Dialect.Cli.Models;
using Dialect.Cli.Services;
using Dialect.Cli.SqlDiscovery;
using Dialect.Core.AST;
using Dialect.Core.QueryTranslation;
using Dialect.MySql;
using Dialect.PostgreSql;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class SqlConversionServiceTests
{
    [Fact]
    public async Task ConvertAsync_DryRun_ShouldNotModifySourceFile()
    {
        var filePath = CreateTempSourceFile("""
            var sql = "SELECT TOP 1 * FROM [Order]";
            """);
        try
        {
            var service = CreateService();
            var options = new ConversionOptions
            {
                Scope = new ConversionScope.SingleFile(filePath),
                SourceProvider = SqlProvider.SqlServer,
                TargetDialect = new PostgreSqlDialect(),
                DryRun = true,
                CreateBackups = false
            };

            var result = await service.ConvertAsync(options);

            result.IsError.Should().BeFalse();
            var content = await File.ReadAllTextAsync(filePath);
            content.Should().Contain("SELECT TOP 1");
            content.Should().NotContain("LIMIT 1");
        }
        finally
        {
            CleanupTempArtifacts(filePath);
        }
    }

    [Fact]
    public async Task ConvertAsync_Apply_ShouldRewriteLiteralToTargetDialectAndCreateBackup()
    {
        var filePath = CreateTempSourceFile("""
            var sql = "SELECT TOP 1 * FROM [Order]";
            """);
        try
        {
            var service = CreateService();
            var options = new ConversionOptions
            {
                Scope = new ConversionScope.SingleFile(filePath),
                SourceProvider = SqlProvider.SqlServer,
                TargetDialect = new MySqlDialect(),
                DryRun = false,
                CreateBackups = true
            };

            var result = await service.ConvertAsync(options);

            result.IsError.Should().BeFalse();
            var content = await File.ReadAllTextAsync(filePath);
            content.Should().Contain("SELECT * FROM `Order` LIMIT 1");
            content.Should().NotContain("SELECT TOP 1");
            File.Exists(filePath + ".bak").Should().BeTrue();
        }
        finally
        {
            CleanupTempArtifacts(filePath);
        }
    }

    private static SqlConversionService CreateService()
    {
        var discoveryLogger = new Mock<ILogger<RoslynSqlDiscoveryService>>();
        var conversionLogger = new Mock<ILogger<SqlConversionService>>();
        var discovery = new RoslynSqlDiscoveryService(discoveryLogger.Object);

        var parserAdapters = new Dictionary<SqlProvider, SqlParserAdapter>
        {
            [SqlProvider.SqlServer] = new SqlServerParserAdapter(),
            [SqlProvider.PostgreSql] = new PostgreSqlParserAdapter(),
            [SqlProvider.MySql] = new MySqlParserAdapter()
        };

        var translator = new DefaultSqlTranslator(new DefaultSqlProviderDetector(), parserAdapters);
        return new SqlConversionService(discovery, translator, conversionLogger.Object);
    }

    private static string CreateTempSourceFile(string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), "Dialect.SqlConversionServiceTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, "Input.cs");
        File.WriteAllText(filePath, content);
        return filePath;
    }

    private static void CleanupTempArtifacts(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(dir))
            return;

        if (Directory.Exists(dir))
            Directory.Delete(dir, recursive: true);
    }
}
