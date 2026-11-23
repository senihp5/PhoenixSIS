using Phoenix.Core.DataMigration;

namespace Phoenix.Infrastructure.DataMigration;

/// <summary>
/// Flightdeck (the New Heights Case Management System) as a Phoenix migration
/// source. ADR-003: Flightdeck is integrated across a boundary, never merged -
/// this connector maps Flightdeck's student shape into Phoenix's source-neutral
/// record at the anti-corruption layer (ADR-002); no Flightdeck code is copied.
///
/// Flightdeck's canonical student key is Student.StudentDCID (long), surfaced
/// here as <see cref="SourceStudentRecord.ExternalId"/>. Flightdeck also carries
/// StudentNumber, PowerSchoolStudentID and StateStudentNumber (the 10-digit TSDS
/// Unique ID); since PHX-9.1 they travel as extra identifiers and are stored as
/// mapped rows under "Flightdeck:&lt;Kind&gt;". Every value below is made up.
///
/// This returns a SAMPLE dataset shaped like Flightdeck. Reading live New Heights
/// records is gated on the three conditions in ADR-003 (hardening complete, NH
/// data authorization, and - for TSDS submission - TEA vendor approval), so the
/// scaffold proves the boundary on sample data only.
/// </summary>
public sealed class FlightdeckSourceConnector : ISisSourceConnector
{
    public string SourceSystem => "Flightdeck";
    public string DisplayName => "Flightdeck (New Heights CMS) - sample";

    public Task<IReadOnlyList<SourceStudentRecord>> ReadStudentsAsync(
        CancellationToken cancellationToken = default)
    {
        // ExternalId values stand in for Flightdeck Student.StudentDCID.
        IReadOnlyList<SourceStudentRecord> data =
        [
            new("100457", "Marcus", null, "Ellison", new DateOnly(1990, 3, 18), Ids("260457", "70457", "9900100457")),
            new("100892", "Latoya", "R", "Simmons", new DateOnly(1986, 7, 2), Ids("260892", "70892", "9900100892")),
            new("101203", "Diego", null, "Vargas", new DateOnly(2002, 10, 9), Ids("261203", "71203", "9900101203")),
            new("101574", "Amara", null, "Okonkwo", new DateOnly(1997, 5, 24), Ids("261574", "71574", "9900101574")),
            new("101988", "Brandon", "K", "Pham", new DateOnly(2000, 12, 11), Ids("261988", "71988", "9900101988")),
            new("102341", "Shanice", null, "Williams", new DateOnly(1983, 9, 6), Ids("262341", "72341", "9900102341")),
        ];
        return Task.FromResult(data);
    }

    public const string StudentNumberKind = "StudentNumber";
    public const string PowerSchoolStudentIdKind = "PowerSchoolStudentID";
    public const string StateStudentNumberKind = "StateStudentNumber";

    private static IReadOnlyList<SourceIdentifier> Ids(
        string studentNumber, string powerSchoolStudentId, string stateStudentNumber) =>
    [
        new(StudentNumberKind, studentNumber),
        new(PowerSchoolStudentIdKind, powerSchoolStudentId),
        new(StateStudentNumberKind, stateStudentNumber)
    ];
}
