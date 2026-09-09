using System;
using System.IO;
using Xunit;

namespace Dialect.Tests.QueryTranslation
{
    public class DmlMatrixTests
    {
        // This test skeleton iterates over origin->destination pairs and runs golden files.
        // TODO: implement helper to load golden input/output and invoke the translator.

        [Fact]
        public void Fallback_Should_Translate_SelectTop_Into_PostgreSql_Limit()
        {
            var translator = CreateTranslator();
            var result = translator.Translate("SELECT TOP 1 * FROM [Order]", Dialect.Core.AST.SqlProvider.SqlServer, new Dialect.PostgreSql.PostgreSqlDialect());

            Assert.True(result.HasCompiledResult);
            Assert.Equal("SELECT * FROM \"Order\" LIMIT 1", NormalizeSql(result.Compiled!.Sql));
        }

        [Fact]
        public void Fallback_Should_Translate_SelectTop_Into_MySql_Limit()
        {
            var translator = CreateTranslator();
            var result = translator.Translate("SELECT TOP 1 * FROM [Order]", Dialect.Core.AST.SqlProvider.SqlServer, new Dialect.MySql.MySqlDialect());

            Assert.True(result.HasCompiledResult);
            Assert.Equal("SELECT * FROM `Order` LIMIT 1", NormalizeSql(result.Compiled!.Sql));
        }

        [Fact]
        public void Ast_Should_Preserve_SqlServer_Top_When_Target_Is_SqlServer()
        {
            var translator = CreateTranslator();
            var result = translator.Translate("SELECT TOP 3 [Id] FROM [Order]", Dialect.Core.AST.SqlProvider.SqlServer, new Dialect.SqlServer.SqlServerDialect());

            Assert.True(result.HasCompiledResult);
            Assert.Equal("SELECT TOP 3 [Id] FROM [Order]", NormalizeSql(result.Compiled!.Sql));
        }

        [Fact]
        public void Ast_Should_Convert_PostgreSql_Limit_To_SqlServer_Top()
        {
            var translator = CreateTranslator();
            var result = translator.Translate("SELECT \"Id\" FROM \"Order\" LIMIT 2", Dialect.Core.AST.SqlProvider.PostgreSql, new Dialect.SqlServer.SqlServerDialect());

            Assert.True(result.HasCompiledResult);
            Assert.Equal("SELECT TOP 2 [Id] FROM [Order]", NormalizeSql(result.Compiled!.Sql));
        }

        [Fact]
        public void Ast_Should_Translate_Insert_Literals_SqlServer_To_PostgreSql()
        {
            var translator = CreateTranslator();
            var result = translator.Translate(
                "INSERT INTO [Users] ([Id], [Name]) VALUES (1, 'Ana')",
                Dialect.Core.AST.SqlProvider.SqlServer,
                new Dialect.PostgreSql.PostgreSqlDialect());

            Assert.True(result.HasCompiledResult);
            Assert.Equal("INSERT INTO \"Users\" (\"Id\", \"Name\") VALUES (1, 'Ana')", NormalizeSql(result.Compiled!.Sql));
        }

        [Fact]
        public void Ast_Should_Translate_Update_Literals_SqlServer_To_MySql()
        {
            var translator = CreateTranslator();
            var result = translator.Translate(
                "UPDATE [Users] SET [Name] = 'Ana Maria' WHERE [Id] = 10",
                Dialect.Core.AST.SqlProvider.SqlServer,
                new Dialect.MySql.MySqlDialect());

            Assert.True(result.HasCompiledResult);
            Assert.Equal("UPDATE `Users` SET `Name` = 'Ana Maria' WHERE `Id` = 10", NormalizeSql(result.Compiled!.Sql));
        }

        [Fact]
        public void Ast_Should_Translate_Delete_Literals_MySql_To_SqlServer()
        {
            var translator = CreateTranslator();
            var result = translator.Translate(
                "DELETE FROM `Users` WHERE `Id` = 7",
                Dialect.Core.AST.SqlProvider.MySql,
                new Dialect.SqlServer.SqlServerDialect());

            Assert.True(result.HasCompiledResult);
            Assert.Equal("DELETE FROM [Users] WHERE [Id] = 7", NormalizeSql(result.Compiled!.Sql));
        }

        [Theory]
        [InlineData("sqlserver", "postgresql")]
        [InlineData("sqlserver", "mysql")]
        [InlineData("postgresql", "sqlserver")]
        [InlineData("postgresql", "mysql")]
        [InlineData("mysql", "postgresql")]
        [InlineData("mysql", "sqlserver")]
        public void GoldenCases_OriginToDestination_ShouldMatchExpected(string origin, string destination)
        {
            // Arrange
            var testName = $"{origin}-to-{destination}";
            var searchDir = Path.Combine("tests", "Dialect.Tests", "QueryTranslation");

            if (!Directory.Exists(searchDir))
            {
                // Skip if search directory missing
                return;
            }

            // Find input files matching this origin->destination pair in the search directory
            foreach (var inputPath in Directory.GetFiles(searchDir, $"{testName}.*.input.sql"))
            {
                var fileName = Path.GetFileName(inputPath);
                var expectedPath = Path.Combine(searchDir, fileName.Replace(".input.sql", ".expected.sql"));
                if (!File.Exists(expectedPath))
                    throw new FileNotFoundException("Missing expected file for " + inputPath);

                var inputSql = File.ReadAllText(inputPath);
                var expectedSql = File.ReadAllText(expectedPath);

                // Setup translator with default detector and parser adapters
                var detector = new Dialect.Core.QueryTranslation.DefaultSqlProviderDetector();
                var adapters = new System.Collections.Generic.Dictionary<Dialect.Core.AST.SqlProvider, Dialect.Core.QueryTranslation.SqlParserAdapter>
                {
                    [Dialect.Core.AST.SqlProvider.SqlServer] = new Dialect.Core.QueryTranslation.SqlServerParserAdapter(),
                    [Dialect.Core.AST.SqlProvider.PostgreSql] = new Dialect.Core.QueryTranslation.PostgreSqlParserAdapter(),
                    [Dialect.Core.AST.SqlProvider.MySql] = new Dialect.Core.QueryTranslation.MySqlParserAdapter()
                };

                var translator = new Dialect.Core.QueryTranslation.DefaultSqlTranslator(detector, adapters);

                // Map origin/destination strings to enums/instances
                Dialect.Core.AST.SqlProvider sourceProvider = origin.ToLowerInvariant() switch
                {
                    "sqlserver" => Dialect.Core.AST.SqlProvider.SqlServer,
                    "postgresql" => Dialect.Core.AST.SqlProvider.PostgreSql,
                    "mysql" => Dialect.Core.AST.SqlProvider.MySql,
                    _ => throw new ArgumentException("Unknown origin: " + origin)
                };

                Dialect.Core.Dialects.ISqlDialect targetDialect = destination.ToLowerInvariant() switch
                {
                    "sqlserver" => new Dialect.SqlServer.SqlServerDialect(),
                    "postgresql" => new Dialect.PostgreSql.PostgreSqlDialect(),
                    "mysql" => new Dialect.MySql.MySqlDialect(),
                    _ => throw new ArgumentException("Unknown destination: " + destination)
                };

                var result = translator.Translate(inputSql, sourceProvider, targetDialect);

                if (!result.HasCompiledResult)
                {
                    // Fail test with diagnostic information so implementers can see what's missing
                    var issues = result.UntranslatableConstructs ?? new System.Collections.Generic.List<string>();
                    var msg = result.ErrorMessage ?? string.Join("; ", issues);
                    Assert.Fail($"Translation did not produce a compiled result for {inputPath}: {msg}");
                }

                var actualSql = result.Compiled!.Sql;

                // Assert compiled SQL matches expected (normalized)
                Assert.Equal(NormalizeSql(expectedSql), NormalizeSql(actualSql));
            }
        }

        private static Dialect.Core.QueryTranslation.DefaultSqlTranslator CreateTranslator()
        {
            var detector = new Dialect.Core.QueryTranslation.DefaultSqlProviderDetector();
            var adapters = new System.Collections.Generic.Dictionary<Dialect.Core.AST.SqlProvider, Dialect.Core.QueryTranslation.SqlParserAdapter>
            {
                [Dialect.Core.AST.SqlProvider.SqlServer] = new Dialect.Core.QueryTranslation.SqlServerParserAdapter(),
                [Dialect.Core.AST.SqlProvider.PostgreSql] = new Dialect.Core.QueryTranslation.PostgreSqlParserAdapter(),
                [Dialect.Core.AST.SqlProvider.MySql] = new Dialect.Core.QueryTranslation.MySqlParserAdapter()
            };

            return new Dialect.Core.QueryTranslation.DefaultSqlTranslator(detector, adapters);
        }

        private static string NormalizeSql(string sql)
        {
            return sql?.Trim().TrimEnd(';').Replace("\r\n", "\n") ?? string.Empty;
        }
    }
}
