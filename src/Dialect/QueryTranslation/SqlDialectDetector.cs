namespace Dialect.Core.QueryTranslation;

using Dialect.Core.AST;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

/// <summary>
/// Detects the SQL dialect/provider of a given SQL query through syntax analysis.
/// Uses heuristic pattern matching to identify TSQL (SQL Server), PostgreSQL, and MySQL syntax patterns.
/// </summary>
public static class SqlDialectDetector
{
    // SQL Server / TSQL patterns
    private static readonly Regex[] TsqlPatterns = new[]
    {
        new Regex(@"@\w+", RegexOptions.IgnoreCase | RegexOptions.Compiled), // @parameter style
        new Regex(@"\[[\w_]+\]", RegexOptions.IgnoreCase | RegexOptions.Compiled), // [bracketed identifiers]
        new Regex(@"\bTOP\s+\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled), // TOP n
        new Regex(@"\bMERGE\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), // MERGE statement
        new Regex(@"\bCONVERT\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled), // CONVERT() function
        new Regex(@"\bOUTPUT\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), // OUTPUT clause
        new Regex(@"\bOFFSET\s+\d+\s+ROWS", RegexOptions.IgnoreCase | RegexOptions.Compiled), // OFFSET n ROWS
        new Regex(@"\bFETCH\s+NEXT", RegexOptions.IgnoreCase | RegexOptions.Compiled), // FETCH NEXT (ANSI paging)
    };

    // PostgreSQL patterns
    private static readonly Regex[] PostgreSqlPatterns = new[]
    {
        new Regex(@":\w+(?=\s|,|;|\)|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled), // :parameter style
        new Regex(@"""[\w_]+""", RegexOptions.IgnoreCase | RegexOptions.Compiled), // "quoted identifiers"
        new Regex(@"\bON\s+CONFLICT\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), // ON CONFLICT
        new Regex(@"::\w+", RegexOptions.IgnoreCase | RegexOptions.Compiled), // :: cast operator
        new Regex(@"\bRETURNING\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), // RETURNING clause
        new Regex(@"\bDISTINCT\s+ON\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), // DISTINCT ON
        new Regex(@"\$\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled), // $1, $2 positional parameters
        new Regex(@"\bGEN_RANDOM_UUID\b|\buuid_generate", RegexOptions.IgnoreCase | RegexOptions.Compiled), // PostgreSQL UUID functions
    };

    // MySQL patterns
    private static readonly Regex[] MySqlPatterns = new[]
    {
        new Regex(@"\?", RegexOptions.IgnoreCase | RegexOptions.Compiled), // ? placeholder
        new Regex(@"`[\w_]+`", RegexOptions.IgnoreCase | RegexOptions.Compiled), // `backtick identifiers`
        new Regex(@"\bON\s+DUPLICATE\s+KEY\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), // ON DUPLICATE KEY
        new Regex(@"\bLIMIT\s+\d+\s+OFFSET", RegexOptions.IgnoreCase | RegexOptions.Compiled), // LIMIT n OFFSET (MySQL order)
        new Regex(@"\bVALUES\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled), // INSERT with VALUES
        new Regex(@"IFNULL\s*\(|IF\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled), // MySQL-specific functions
        new Regex(@"\bJSON_EXTRACT\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), // MySQL JSON functions
        new Regex(@"\bGROUP_CONCAT\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), // MySQL GROUP_CONCAT function
    };

    /// <summary>
    /// Detects the SQL dialect provider of a given SQL query through pattern matching.
    /// </summary>
    /// <param name="sql">The SQL query to analyze</param>
    /// <returns>
    /// The detected SqlProvider (SqlServer, PostgreSql, or MySql), or null if no clear match
    /// or confidence is low.
    /// </returns>
    public static SqlProvider? DetectDialectProvider(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return null;

        // Score each dialect based on pattern matches
        int tsqlScore = CountPatternMatches(sql, TsqlPatterns);
        int postgreSqlScore = CountPatternMatches(sql, PostgreSqlPatterns);
        int mySqlScore = CountPatternMatches(sql, MySqlPatterns);

        // Determine the winner
        if (tsqlScore == 0 && postgreSqlScore == 0 && mySqlScore == 0)
        {
            // No distinctive patterns found — query is likely ANSI SQL compatible
            return null;
        }

        // Find the maximum score
        int maxScore = Math.Max(tsqlScore, Math.Max(postgreSqlScore, mySqlScore));

        // If there's a tie or no clear winner, return null to force explicit dialect specification
        int winnerCount = 0;
        if (tsqlScore == maxScore) winnerCount++;
        if (postgreSqlScore == maxScore) winnerCount++;
        if (mySqlScore == maxScore) winnerCount++;

        if (winnerCount > 1)
        {
            // Tie or ambiguous — return null
            return null;
        }

        // Return the clear winner
        if (tsqlScore == maxScore)
            return SqlProvider.SqlServer;
        if (postgreSqlScore == maxScore)
            return SqlProvider.PostgreSql;
        if (mySqlScore == maxScore)
            return SqlProvider.MySql;

        return null;
    }

    /// <summary>
    /// Detects the SQL dialect provider with a minimum confidence threshold.
    /// Only returns a dialect if the score exceeds the threshold.
    /// </summary>
    /// <param name="sql">The SQL query to analyze</param>
    /// <param name="minConfidenceScore">Minimum number of pattern matches to return a result (default: 1)</param>
    /// <returns>The detected SqlProvider or null if confidence is below threshold</returns>
    public static SqlProvider? DetectDialectProvider(string sql, int minConfidenceScore = 1)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return null;

        int tsqlScore = CountPatternMatches(sql, TsqlPatterns);
        int postgreSqlScore = CountPatternMatches(sql, PostgreSqlPatterns);
        int mySqlScore = CountPatternMatches(sql, MySqlPatterns);

        // Require minimum confidence
        if (tsqlScore >= minConfidenceScore && tsqlScore > Math.Max(postgreSqlScore, mySqlScore))
            return SqlProvider.SqlServer;
        if (postgreSqlScore >= minConfidenceScore && postgreSqlScore > Math.Max(tsqlScore, mySqlScore))
            return SqlProvider.PostgreSql;
        if (mySqlScore >= minConfidenceScore && mySqlScore > Math.Max(tsqlScore, postgreSqlScore))
            return SqlProvider.MySql;

        return null;
    }

    /// <summary>
    /// Counts how many patterns match in the given SQL query.
    /// </summary>
    private static int CountPatternMatches(string sql, Regex[] patterns)
    {
        int count = 0;
        foreach (var pattern in patterns)
        {
            if (pattern.IsMatch(sql))
                count++;
        }
        return count;
    }

    /// <summary>
    /// Gets detailed scoring information for debugging.
    /// Returns the score for each dialect based on pattern matches.
    /// </summary>
    public static (int TsqlScore, int PostgreSqlScore, int MySqlScore) GetDetailedScores(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return (0, 0, 0);

        return (
            CountPatternMatches(sql, TsqlPatterns),
            CountPatternMatches(sql, PostgreSqlPatterns),
            CountPatternMatches(sql, MySqlPatterns)
        );
    }
}
