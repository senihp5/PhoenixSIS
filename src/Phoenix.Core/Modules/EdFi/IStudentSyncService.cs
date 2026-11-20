namespace Phoenix.Core.Modules.EdFi;

/// <summary>Where the sync writes: the LEA, campus, school year and calendar.</summary>
public sealed record EdFiSyncTarget(
    long LeaId,
    long SchoolId,
    int SchoolYear,
    string? CalendarCode,
    DateOnly EntryDate);

public enum StudentSyncOutcome
{
    /// <summary>Dry run: no Ed-Fi identifier yet; apply would create the student.</summary>
    WouldCreate,

    /// <summary>Dry run: already mapped to an Ed-Fi studentUniqueId; apply would re-upsert.</summary>
    WouldUpdate,

    /// <summary>Applied: Ed-Fi created the student (HTTP 201) and every association succeeded.</summary>
    Created,

    /// <summary>Applied: the student already existed in Ed-Fi (HTTP 200) and every write succeeded.</summary>
    Updated,

    /// <summary>Not sent: the Phoenix record lacks a field Ed-Fi requires (for example birth date).</summary>
    Skipped,

    /// <summary>Applied: one write failed. <see cref="StudentSyncRow.FailedResource"/> says which.</summary>
    Failed
}

/// <summary>
/// One student's result. <see cref="StudentUniqueId"/> is the Ed-Fi
/// identifier (PHX-NNNNNN); in a dry run for a new student it is the number
/// apply would assign. On failure <see cref="Problem"/> carries Ed-Fi's
/// Problem Details, or <see cref="Reason"/> a non-API cause.
/// </summary>
public sealed record StudentSyncRow(
    Guid StudentId,
    string DisplayName,
    string? StudentUniqueId,
    StudentSyncOutcome Outcome,
    string? FailedResource = null,
    EdFiProblem? Problem = null,
    string? Reason = null);

public sealed record StudentSyncResult(
    EdFiSyncTarget Target,
    bool Applied,
    IReadOnlyList<StudentSyncRow> Rows)
{
    public int Total => Rows.Count;
    public int ToCreate => Rows.Count(r => r.Outcome is StudentSyncOutcome.WouldCreate or StudentSyncOutcome.Created);
    public int ToUpdate => Rows.Count(r => r.Outcome is StudentSyncOutcome.WouldUpdate or StudentSyncOutcome.Updated);
    public int Skipped => Rows.Count(r => r.Outcome == StudentSyncOutcome.Skipped);
    public int Failed => Rows.Count(r => r.Outcome == StudentSyncOutcome.Failed);
}

/// <summary>
/// Pushes canonical Phoenix students to the Ed-Fi ODS (PHX-7.2). Per
/// student, in this order: Student, StudentSchoolAssociation
/// (campus/year/calendar), StudentEducationOrganizationAssociation (LEA),
/// StudentIdentificationCode (Local). The enrollment must precede the other
/// student records: Ed-Fi's relationship-based authorization rejects them
/// (403) until the student is enrolled in one of the client's schools.
/// The Ed-Fi studentUniqueId is a mapped identifier (ADR-002, source
/// system "Ed-Fi"). Dry run writes nothing anywhere; apply is
/// idempotent; one student's failure never stops the batch.
/// </summary>
public interface IStudentSyncService
{
    /// <summary>The target built from configuration (EdFiApi section).</summary>
    EdFiSyncTarget GetDefaultTarget();

    Task<StudentSyncResult> SyncAllAsync(
        EdFiSyncTarget target,
        bool apply,
        CancellationToken cancellationToken = default);
}
