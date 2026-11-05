using Phoenix.Core.DataMigration;

namespace Phoenix.Infrastructure.DataMigration;

/// <summary>
/// A built-in sample source for proving the migration pipeline end to end
/// without an external SIS. A real file/CSV or PowerSchool connector plugs into
/// the same <see cref="ISisSourceConnector"/> boundary in a later sprint.
/// </summary>
public sealed class SampleSourceConnector : ISisSourceConnector
{
    public string SourceSystem => "Sample";
    public string DisplayName => "Sample (built-in dataset)";

    public Task<IReadOnlyList<SourceStudentRecord>> ReadStudentsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SourceStudentRecord> data =
        [
            new("S-1001", "Lisa", null, "Woods", new DateOnly(2005, 3, 1)),
            new("S-1002", "Omar", null, "Vega", new DateOnly(2004, 11, 20)),
            new("S-1003", "Mia", "K", "Nguyen", new DateOnly(2006, 1, 9)),
            new("S-1004", "Jalen", null, "Brooks", new DateOnly(2003, 7, 15)),
            new("S-1005", "Sofia", null, "Reyes", new DateOnly(2005, 9, 30)),
        ];
        return Task.FromResult(data);
    }
}
