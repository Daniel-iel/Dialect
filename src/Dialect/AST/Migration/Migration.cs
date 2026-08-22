namespace Dialect.Core.AST.Migration;

/// <summary>
/// Represents a timestamped database migration with a sequence of steps.
/// </summary>
public sealed record Migration(
    string Name,
    string Version,  // Semantic version: e.g., "001", "001.001", "20240115123456"
    DateTime Timestamp,
    IReadOnlyList<MigrationStep> Steps
)
{
    /// <summary>
    /// Validates the migration for correctness.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new ArgumentException("Migration name cannot be empty");

        if (string.IsNullOrWhiteSpace(Version))
            throw new ArgumentException("Migration version cannot be empty");

        if (Steps == null || Steps.Count == 0)
            throw new ArgumentException("Migration must contain at least one step");

        // Validate each step
        foreach (var step in Steps)
        {
            switch (step)
            {
                case CreateTableStep ct:
                    ct.Validate();
                    break;
                case DropTableStep dt:
                    dt.Validate();
                    break;
                case AddColumnStep ac:
                    ac.Validate();
                    break;
                case DropColumnStep dc:
                    dc.Validate();
                    break;
                case AlterColumnStep alt:
                    alt.Validate();
                    break;
                case AddIndexStep ai:
                    ai.Validate();
                    break;
                case DropIndexStep di:
                    di.Validate();
                    break;
                case AddForeignKeyStep afk:
                    afk.Validate();
                    break;
                case DropForeignKeyStep dfk:
                    dfk.Validate();
                    break;
            }
        }
    }
}
