using Dialect.PostgreSql.DI;
using Dialect.Samples._01_Basic;
using Dialect.Samples._02_Intermediate;
using Dialect.Samples._03_Advanced;
using Dialect.Samples.Executors;
using Dialect.Samples.Utilities;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Dialect Samples - Interactive console application demonstrating all framework features.
/// </summary>
class Program
{
    static void SafeClear()
    {
        try
        {
            if (!Console.IsOutputRedirected && !Console.IsInputRedirected)
            {
                Console.Clear();
            }
        }
        catch
        {
            // Console.Clear() may fail in some terminal environments
        }
    }

    static void SafeReadKey()
    {
        try
        {
            if (!Console.IsInputRedirected)
            {
                Console.ReadKey();
            }
        }
        catch
        {
            // ReadKey() may fail in some terminal environments
        }
    }

    static void Main(string[] args)
    {
        // Initialize SQL Framework with default dialect (PostgreSQL)
        // This registers the default dialect in SqlDialectRegistry
        // which will be used by all .Compile() calls throughout the application
        var services = new ServiceCollection();
        services.AddPostgreSqlFramework();
        var serviceProvider = services.BuildServiceProvider();

        // If input is redirected or an explicit batch command is provided, run batch mode
        if (Console.IsInputRedirected || args.Contains("--batch") || args.Contains("98"))
        {
            MainAsync(args).GetAwaiter().GetResult();
            return;
        }

        DisplayWelcome();

        while (true)
        {
            DisplayMenu();
            var choice = Console.ReadLine()?.Trim() ?? "";

            if (ExecuteExample(choice))
            {
                Console.WriteLine("\nPress any key to continue...");
                SafeReadKey();
                SafeClear();
            }
            else if (choice.Equals("exit", StringComparison.OrdinalIgnoreCase) || choice == "0")
            {
                break;
            }
            else
            {
                OutputFormatter.PrintError("Invalid choice. Please try again.");
                SafeReadKey();
                SafeClear();
            }
        }

        Console.WriteLine("\nThank you for using Dialect Samples! 👋");
    }

    static async Task MainAsync(string[] args)
    {
        Console.WriteLine("Starting Dialect Samples Batch Execution...");
        var executor = new BatchExampleExecutor();

        try
        {
            var resultPath = await executor.ExecuteAll();
            Console.WriteLine($"\nBatch execution completed successfully!");
            Console.WriteLine($"Results saved to: {resultPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError during batch execution: {ex.Message}");
            if (ex.StackTrace != null)
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }

    static void DisplayWelcome()
    {
        SafeClear();
        OutputFormatter.PrintSectionHeader("DIALECT FRAMEWORK - COMPREHENSIVE SAMPLES 🚀");

        const string? welcomeText = @"
Welcome to the Dialect FluentBuilder SQL Framework samples!
This application demonstrates all major framework features with live examples.

Supported Databases:
  • SQL Server 2022
  • PostgreSQL 15
  • MySQL 8.0

(Make sure Docker containers are running: docker-compose up -d)

Press any key to continue...";

        Console.WriteLine(welcomeText);
        SafeReadKey();
        SafeClear();
    }

    static void DisplayMenu()
    {
        OutputFormatter.PrintSectionHeader("SELECT AN EXAMPLE CATEGORY");

        const string? menuText = @"
BASIC EXAMPLES (DML Fundamentals)
  1. SELECT - Basic Queries
  2. INSERT - Data Insertion
  3. UPDATE - Data Modification
  4. DELETE - Data Removal
  5. UPSERT - Insert or Update (Dialect Comparison)

INTERMEDIATE EXAMPLES (Query Composition)
  6. JOINs - Inner, Left, Right, Full, Cross
  7. CTEs - Common Table Expressions
  8. Window Functions - ROW_NUMBER, RANK, LAG, LEAD
  9. Grouping & Aggregation - GROUP BY, HAVING

ADVANCED EXAMPLES (Performance & Analysis)
  10. Query Optimization - Analysis & Parsing
  11. Index Advisor - Performance Tuning
  12. Schema Validation - Data Integrity

UTILITIES
  98. Batch Execution - All Examples
  99. Docker Setup Guide
  0. Exit";

        Console.WriteLine(menuText);
        Console.Write("\nEnter choice (1-12, 98, 99, or 0): ");
    }

    static bool ExecuteExample(string choice)
    {
        try
        {
            return choice switch
            {
                // Basic Examples
                "1" => RunExample(new SelectExamples()),
                "2" => RunExample(new InsertExamples()),
                "3" => RunExample(new UpdateExamples()),
                "4" => RunExample(new DeleteExamples()),
                "5" => RunExample(new UpsertExamples()),

                // Intermediate Examples
                "6" => RunExample(new JoinExamples()),
                "7" => RunExample(new CteExamples()),
                "8" => RunExample(new WindowFunctionExamples()),
                "9" => RunExample(new GroupingExamples()),

                // Advanced Examples
                "10" => RunExample(new OptimizationExamples()),
                "11" => RunExample(new IndexAdvisorExamples()),
                "12" => RunExample(new SchemaValidationExamples()),

                // Utilities
                "98" => ExecuteBatchMode(),
                "99" => DisplayDockerSetupGuide(),

                _ => false
            };
        }
        catch (Exception ex)
        {
            OutputFormatter.PrintError($"Error executing example: {ex.Message}");
            return true;
        }
    }

    static bool RunExample(ExampleBase example)
    {
        Console.Clear();
        OutputFormatter.PrintSectionHeader(example.Name);
        Console.WriteLine($"{example.Description}\n");

        try
        {
            example.Run();
            OutputFormatter.PrintSuccess("Example completed successfully!");
            return true;
        }
        catch (Exception ex)
        {
            OutputFormatter.PrintError($"Example failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                OutputFormatter.PrintError($"Inner exception: {ex.InnerException.Message}");
            }
            return true;
        }
    }

    static bool ExecuteBatchMode()
    {
        Console.WriteLine("\nExecuting all examples in batch mode...\n");
        MainAsync(new string[] { }).GetAwaiter().GetResult();

        Console.WriteLine("\nPress any key to continue...");
        SafeReadKey();
        SafeClear();
        return true;
    }

    static bool DisplayDockerSetupGuide()
    {
        Console.Clear();
        OutputFormatter.PrintSectionHeader("DOCKER SETUP GUIDE");

        const string? quickStartText = @"
QUICK START
1. Install Docker Desktop (https://www.docker.com/products/docker-desktop)
2. Navigate to project root directory
3. Run the following command:

   docker-compose up -d";

        const string? verifyContainersText = @"
VERIFY CONTAINERS ARE RUNNING
Run: docker-compose ps

You should see 3 containers with STATUS 'Up':
  • dialect-sqlserver   (Port 1433)
  • dialect-postgresql  (Port 5432)
  • dialect-mysql       (Port 3306)";

        const string? healthChecksText = @"
HEALTH CHECKS
Containers include automatic health checks. Run:
  docker-compose ps  (check STATUS column)";

        const string? connectionStringsText = @"
CONNECTION STRINGS
SQL Server:   Server=localhost,1433; User=sa; Password=P@ssw0rd!
PostgreSQL:   Host=localhost:5432; User=postgres; Password=postgres
MySQL:        Host=localhost:3306; User=root; Password=root";

        const string? stopResetText = @"
STOP CONTAINERS
  docker-compose down

REMOVE DATA & RESET
  docker-compose down -v";

        const string? databaseSchemaText = @"
DATABASE SCHEMA
All databases contain identical schemas:
  • Users (UserId, Username, Email, CreatedAt)
  • Products (ProductId, Name, Price, StockQuantity)
  • Orders (OrderId, UserId, OrderDate, Total)
  • OrderItems (OrderItemId, OrderId, ProductId, Quantity, UnitPrice)";

        const string? sampleDataText = @"
SAMPLE DATA
  • 4 Users (john_doe, jane_smith, bob_wilson, alice_johnson)
  • 5 Products (Laptop, Mouse, Keyboard, Monitor, Headphones)
  • 5 Orders with multiple order items";

        const string? troubleshootingText = @"
TROUBLESHOOTING
Q: Container won't start?
A: Check Docker is running, ports not in use, disk space available

Q: Connection refused?
A: Wait 30 seconds for health checks to complete, check docker-compose ps

Q: Data not loading?
A: Check /docker folder has .sql files, rerun: docker-compose down -v && docker-compose up -d

Press any key to return to menu...";

        string[] texts = { quickStartText, verifyContainersText, healthChecksText, connectionStringsText, stopResetText, databaseSchemaText, sampleDataText, troubleshootingText };

        foreach (var text in texts)
        {
            Console.WriteLine(text);
        }

        SafeReadKey();
        Console.Clear();

        return true;
    }
}
