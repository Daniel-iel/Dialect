using Dialect.Samples._01_Basic;
using Dialect.Samples._02_Intermediate;
using Dialect.Samples._03_Advanced;
using Dialect.Samples.Utilities;

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

    static void Main(string[] args)
    {
        DisplayWelcome();

        while (true)
        {
            DisplayMenu();
            var choice = Console.ReadLine()?.Trim() ?? "";

            if (ExecuteExample(choice))
            {
                Console.WriteLine("\nPress any key to continue...");
                Console.ReadKey();
                SafeClear();
            }
            else if (choice.Equals("exit", StringComparison.OrdinalIgnoreCase) || choice == "0")
            {
                break;
            }
            else
            {
                OutputFormatter.PrintError("Invalid choice. Please try again.");
                Console.ReadKey();
                SafeClear();
            }
        }

        Console.WriteLine("\nThank you for using Dialect Samples! 👋");
    }

    static void DisplayWelcome()
    {
        SafeClear();
        OutputFormatter.PrintSectionHeader("🚀 DIALECT FRAMEWORK - COMPREHENSIVE SAMPLES 🚀");

        Console.WriteLine("\nWelcome to the Dialect FluentBuilder SQL Framework samples!");
        Console.WriteLine("This application demonstrates all major framework features with live examples.");
        Console.WriteLine("\nSupported Databases:");
        Console.WriteLine("  • SQL Server 2022");
        Console.WriteLine("  • PostgreSQL 15");
        Console.WriteLine("  • MySQL 8.0");
        Console.WriteLine("\n(Make sure Docker containers are running: docker-compose up -d)");
        Console.WriteLine("\nPress any key to continue...");
        Console.ReadKey();
        SafeClear();
    }

    static void DisplayMenu()
    {
        OutputFormatter.PrintSectionHeader("SELECT AN EXAMPLE CATEGORY");

        Console.WriteLine("\n📚 BASIC EXAMPLES (DML Fundamentals)");
        Console.WriteLine("  1. SELECT - Basic Queries");
        Console.WriteLine("  2. INSERT - Data Insertion");
        Console.WriteLine("  3. UPDATE - Data Modification");
        Console.WriteLine("  4. DELETE - Data Removal");
        Console.WriteLine("  5. UPSERT - Insert or Update (Dialect Comparison)");

        Console.WriteLine("\n🔄 INTERMEDIATE EXAMPLES (Query Composition)");
        Console.WriteLine("  6. JOINs - Inner, Left, Right, Full, Cross");
        Console.WriteLine("  7. CTEs - Common Table Expressions");
        Console.WriteLine("  8. Window Functions - ROW_NUMBER, RANK, LAG, LEAD");
        Console.WriteLine("  9. Grouping & Aggregation - GROUP BY, HAVING");

        Console.WriteLine("\n⚙️  ADVANCED EXAMPLES (Performance & Analysis)");
        Console.WriteLine("  10. Query Optimization - Analysis & Parsing");
        Console.WriteLine("  11. Index Advisor - Performance Tuning");
        Console.WriteLine("  12. Schema Validation - Data Integrity");

        Console.WriteLine("\n💡 UTILITIES");
        Console.WriteLine("  99. Docker Setup Guide");
        Console.WriteLine("  0. Exit");

        Console.Write("\nEnter choice (1-12, 99, or 0): ");
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

    static bool DisplayDockerSetupGuide()
    {
        Console.Clear();
        OutputFormatter.PrintSectionHeader("🐳 DOCKER SETUP GUIDE");

        Console.WriteLine("\n🚀 QUICK START");
        Console.WriteLine("1. Install Docker Desktop (https://www.docker.com/products/docker-desktop)");
        Console.WriteLine("2. Navigate to project root directory");
        Console.WriteLine("3. Run the following command:");
        Console.WriteLine("\n   docker-compose up -d\n");

        Console.WriteLine("📊 VERIFY CONTAINERS ARE RUNNING");
        Console.WriteLine("Run: docker-compose ps\n");
        Console.WriteLine("You should see 3 containers with STATUS 'Up':");
        Console.WriteLine("  • dialect-sqlserver   (Port 1433)");
        Console.WriteLine("  • dialect-postgresql  (Port 5432)");
        Console.WriteLine("  • dialect-mysql       (Port 3306)\n");

        Console.WriteLine("✅ HEALTH CHECKS");
        Console.WriteLine("Containers include automatic health checks. Run:");
        Console.WriteLine("  docker-compose ps  (check STATUS column)\n");

        Console.WriteLine("🔧 CONNECTION STRINGS");
        Console.WriteLine("SQL Server:   Server=localhost,1433; User=sa; Password=P@ssw0rd!");
        Console.WriteLine("PostgreSQL:   Host=localhost:5432; User=postgres; Password=postgres");
        Console.WriteLine("MySQL:        Host=localhost:3306; User=root; Password=root\n");

        Console.WriteLine("🛑 STOP CONTAINERS");
        Console.WriteLine("  docker-compose down\n");

        Console.WriteLine("🗑️  REMOVE DATA & RESET");
        Console.WriteLine("  docker-compose down -v\n");

        Console.WriteLine("📝 DATABASE SCHEMA");
        Console.WriteLine("All databases contain identical schemas:");
        Console.WriteLine("  • Users (UserId, Username, Email, CreatedAt)");
        Console.WriteLine("  • Products (ProductId, Name, Price, StockQuantity)");
        Console.WriteLine("  • Orders (OrderId, UserId, OrderDate, Total)");
        Console.WriteLine("  • OrderItems (OrderItemId, OrderId, ProductId, Quantity, UnitPrice)\n");

        Console.WriteLine("💾 SAMPLE DATA");
        Console.WriteLine("  • 4 Users (john_doe, jane_smith, bob_wilson, alice_johnson)");
        Console.WriteLine("  • 5 Products (Laptop, Mouse, Keyboard, Monitor, Headphones)");
        Console.WriteLine("  • 5 Orders with multiple order items\n");

        Console.WriteLine("📖 TROUBLESHOOTING");
        Console.WriteLine("Q: Container won't start?");
        Console.WriteLine("A: Check Docker is running, ports not in use, disk space available\n");
        Console.WriteLine("Q: Connection refused?");
        Console.WriteLine("A: Wait 30 seconds for health checks to complete, check docker-compose ps\n");
        Console.WriteLine("Q: Data not loading?");
        Console.WriteLine("A: Check /docker folder has .sql files, rerun: docker-compose down -v && docker-compose up -d\n");

        Console.WriteLine("Press any key to return to menu...");
        Console.ReadKey();
        Console.Clear();

        return true;
    }
}
