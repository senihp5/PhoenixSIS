namespace Phoenix.Core.Students;

/// <summary>Phoenix-native grade levels. Ed-Fi descriptors are mapped at the EdFi boundary.</summary>
public enum GradeLevel
{
    AdultEducation,
    Grade09,
    Grade10,
    Grade11,
    Grade12,
    Ungraded
}

/// <summary>Phoenix-native kind of enrollment (Ed-Fi EnrollmentType is mapped at the boundary).</summary>
public enum EnrollmentKind
{
    Regular,
    Summer,
    Preregistered
}

/// <summary>Phoenix-native exit reasons (Ed-Fi ExitWithdrawType is mapped at the boundary).</summary>
public enum ExitReason
{
    Graduated,
    TransferredOut,
    Withdrew,
    NonAttendance,
    Other
}

/// <summary>A campus Phoenix can enroll into (from configuration, Phoenix:Campuses).</summary>
public sealed record CampusInfo(string Number, string Name, string LeaNumber);

public interface ICampusDirectory
{
    IReadOnlyList<CampusInfo> GetCampuses();

    CampusInfo? Find(string campusNumber);
}

/// <summary>
/// A request to enroll a student. <see cref="SchoolYear"/> is the ending year
/// (2027 = 2026-27). The entry date cannot be changed once created; the only
/// later change is an exit (Sprint 8 decision - Ed-Fi keys the enrollment on
/// the entry date).
/// </summary>
public sealed record NewEnrollment(
    Guid StudentId,
    string CampusNumber,
    int SchoolYear,
    DateOnly EntryDate,
    GradeLevel EntryGradeLevel,
    EnrollmentKind Kind = EnrollmentKind.Regular,
    decimal FullTimeEquivalency = 1.0m,
    bool IsPrimary = true);

public sealed record EnrollmentRecord(
    Guid Id,
    Guid StudentId,
    string CampusNumber,
    int SchoolYear,
    DateOnly EntryDate,
    GradeLevel EntryGradeLevel,
    EnrollmentKind Kind,
    decimal FullTimeEquivalency,
    bool IsPrimary,
    DateOnly? ExitDate,
    ExitReason? ExitReason)
{
    public bool IsOpen => ExitDate is null;
}

/// <summary>Enroll every student who has no enrollment at the campus for the school year.</summary>
public sealed record BulkEnrollRequest(
    string CampusNumber,
    int SchoolYear,
    DateOnly EntryDate,
    GradeLevel EntryGradeLevel,
    EnrollmentKind Kind = EnrollmentKind.Regular,
    decimal FullTimeEquivalency = 1.0m);

/// <summary>
/// Result of a bulk enroll. When <see cref="Applied"/> is false (preview),
/// <see cref="StudentIds"/> are the students that would be enrolled.
/// </summary>
public sealed record BulkEnrollResult(
    bool Applied,
    IReadOnlyList<Guid> StudentIds,
    int AlreadyEnrolled);

/// <summary>A request that breaks an enrollment rule. The message is safe to show to the admin.</summary>
public sealed class EnrollmentRuleException(string message) : Exception(message);

/// <summary>
/// Phoenix enrollments (core domain, Sprint 8). Every change is audited in the
/// same save (ADR-004). Reads are not audited here: callers that show records
/// to a person record the view.
/// </summary>
public interface IEnrollmentService
{
    Task<IReadOnlyList<EnrollmentRecord>> GetForStudentAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EnrollmentRecord>> GetForCampusYearAsync(
        string campusNumber,
        int schoolYear,
        CancellationToken cancellationToken = default);

    Task<EnrollmentRecord> EnrollAsync(
        NewEnrollment enrollment,
        CancellationToken cancellationToken = default);

    Task<EnrollmentRecord> ExitAsync(
        Guid enrollmentId,
        DateOnly exitDate,
        ExitReason reason,
        CancellationToken cancellationToken = default);

    Task<BulkEnrollResult> EnrollManyAsync(
        BulkEnrollRequest request,
        bool apply,
        CancellationToken cancellationToken = default);
}
