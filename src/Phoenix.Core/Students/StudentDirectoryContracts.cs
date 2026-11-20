namespace Phoenix.Core.Students;

/// <summary>One row of the student list. <see cref="YearEnrollment"/> is the student's enrollment for the selected school year, if any.</summary>
public sealed record StudentListItem(
    Guid Id,
    string FirstName,
    string? MiddleName,
    string LastName,
    DateOnly? BirthDate,
    string? EdFiId,
    EnrollmentRecord? YearEnrollment);

public sealed record StudentPage(
    IReadOnlyList<StudentListItem> Items,
    int Total,
    int Page,
    int PageSize)
{
    public int PageCount => Total == 0 ? 1 : (Total + PageSize - 1) / PageSize;
}

public sealed record StudentIdentifierInfo(string SourceSystem, string Value);

public sealed record StudentDetail(
    StudentRecord Student,
    IReadOnlyList<StudentIdentifierInfo> Identifiers,
    IReadOnlyList<EnrollmentRecord> Enrollments);

/// <summary>
/// Read side of the student pages (PHX-8.3, Students flag). Every student
/// returned is recorded as viewed before the data is returned (ADR-004 fail
/// closed): one event per listed student, one per opened record.
/// </summary>
public interface IStudentDirectory
{
    Task<StudentPage> SearchAsync(
        string? search,
        int schoolYear,
        int page,
        CancellationToken cancellationToken = default);

    Task<StudentDetail?> GetAsync(
        Guid studentId,
        CancellationToken cancellationToken = default);
}
