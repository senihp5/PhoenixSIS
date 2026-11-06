using Phoenix.Core.DataMigration;

namespace Phoenix.Infrastructure.DataMigration;

/// <summary>
/// A built-in Texas adult-high-school sample source: older students with
/// realistic names/DOBs, shaped for the New Heights scenario. Proves the
/// pipeline with Texas-flavored data and gives Sprint 7's Phoenix -> Ed-Fi
/// sync realistic source records. A real SIS connector plugs into the same
/// <see cref="ISisSourceConnector"/> boundary later.
/// </summary>
public sealed class TexasSampleSourceConnector : ISisSourceConnector
{
    public string SourceSystem => "TexasSample";
    public string DisplayName => "Texas adult-HS sample (built-in dataset)";

    public Task<IReadOnlyList<SourceStudentRecord>> ReadStudentsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SourceStudentRecord> data =
        [
            new("NH-24001", "Maria", null, "Hernandez", new DateOnly(1999, 4, 12)),
            new("NH-24002", "Jamal", "T", "Washington", new DateOnly(2004, 8, 3)),
            new("NH-24003", "Ana", "Lucia", "Ramirez", new DateOnly(1988, 11, 27)),
            new("NH-24004", "David", null, "Nguyen", new DateOnly(2001, 2, 15)),
            new("NH-24005", "Keisha", null, "Johnson", new DateOnly(1995, 6, 9)),
            new("NH-24006", "Carlos", null, "Mendoza", new DateOnly(1979, 1, 22)),
            new("NH-24007", "Fatima", null, "Al-Rashid", new DateOnly(2003, 9, 30)),
            new("NH-24008", "Tyler", "J", "Boudreaux", new DateOnly(1992, 12, 5)),
        ];
        return Task.FromResult(data);
    }
}
