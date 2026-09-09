namespace Dialect.Cli.Models;

/// <summary>
/// Discriminated union representing the scope of files/directories to convert.
/// Supports single file, directory, or project file specification.
/// </summary>
public abstract record ConversionScope
{
    /// <summary>
    /// Single C# file conversion.
    /// </summary>
    public sealed record SingleFile(string FilePath) : ConversionScope
    {
        public override string ToString() => $"File: {FilePath}";
    }

    /// <summary>
    /// All C# files in a directory (recursive search).
    /// </summary>
    public sealed record Directory(string DirectoryPath, string SearchPattern = "**/*.cs") : ConversionScope
    {
        public override string ToString() => $"Directory: {DirectoryPath} ({SearchPattern})";
    }

    /// <summary>
    /// C# project file (.csproj) - all source files in the project.
    /// </summary>
    public sealed record Project(string ProjectFilePath) : ConversionScope
    {
        public override string ToString() => $"Project: {ProjectFilePath}";
    }

    /// <summary>
    /// Visual Studio solution file (.sln) - all projects in the solution.
    /// </summary>
    public sealed record Solution(string SolutionFilePath) : ConversionScope
    {
        public override string ToString() => $"Solution: {SolutionFilePath}";
    }
}
