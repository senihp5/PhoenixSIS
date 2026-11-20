using Microsoft.EntityFrameworkCore;
using Phoenix.Core.Auditing;
using Phoenix.Core.Features;
using Phoenix.Core.Students;
using Phoenix.Infrastructure.Data;

namespace Phoenix.Infrastructure.Students;

/// <summary>
/// PHX-8.3 audited read side of the student pages. Guarded by the Students
/// flag. Views are recorded before the data is returned; if the audit write
/// fails, the caller gets the exception and no data (ADR-004).
/// </summary>
public sealed class StudentDirectory : IStudentDirectory
{
    public const int PageSize = 25;
    private const string FeatureFlag = "Students";

    // The source system name the Ed-Fi sync records (ADR-002 identifier row).
    private const string EdFiSourceSystem = "Ed-Fi";

    private readonly PhoenixDbContext _db;
    private readonly IAuditLog _audit;
    private readonly IEnrollmentService _enrollments;
    private readonly IFeatureFlagService _features;

    public StudentDirectory(
        PhoenixDbContext db,
        IAuditLog audit,
        IEnrollmentService enrollments,
        IFeatureFlagService features)
    {
        _db = db;
        _audit = audit;
        _enrollments = enrollments;
        _features = features;
    }

    public async Task<StudentPage> SearchAsync(
        string? search,
        int schoolYear,
        int page,
        CancellationToken cancellationToken = default)
    {
        _features.RequireEnabled(FeatureFlag);

        var query = _db.Students.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            query = query.Where(s =>
                s.FirstName.Contains(q)
                || s.LastName.Contains(q)
                || s.Identifiers.Any(i => i.Value.Contains(q)));
        }

        var total = await query.CountAsync(cancellationToken);
        var pageCount = total == 0 ? 1 : (total + PageSize - 1) / PageSize;
        var safePage = Math.Clamp(page, 1, pageCount);

        var students = await query
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ThenBy(s => s.Id)
            .Skip((safePage - 1) * PageSize)
            .Take(PageSize)
            .Select(s => new
            {
                s.Id,
                s.FirstName,
                s.MiddleName,
                s.LastName,
                s.BirthDate,
                EdFiId = s.Identifiers
                    .Where(i => i.SourceSystem == EdFiSourceSystem)
                    .Select(i => i.Value)
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        var ids = students.Select(s => s.Id).ToList();
        var yearEnrollments = await _db.Enrollments
            .AsNoTracking()
            .Where(e => ids.Contains(e.StudentId) && e.SchoolYear == schoolYear)
            .ToListAsync(cancellationToken);

        // Show the open primary enrollment if there is one, else the latest.
        var byStudent = yearEnrollments
            .GroupBy(e => e.StudentId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(e => e.ExitDate is null && e.IsPrimary)
                    .ThenByDescending(e => e.EntryDate)
                    .First());

        await _audit.RecordAsync(
            ids.Select(id => new AuditEntry(AuditAction.View, "Student", id, id, "Listed in student search.")),
            cancellationToken);

        var items = students
            .Select(s => new StudentListItem(
                s.Id,
                s.FirstName,
                s.MiddleName,
                s.LastName,
                s.BirthDate,
                s.EdFiId,
                byStudent.TryGetValue(s.Id, out var e) ? EnrollmentService.Map(e) : null))
            .ToList();

        return new StudentPage(items, total, safePage, PageSize);
    }

    public async Task<StudentDetail?> GetAsync(Guid studentId, CancellationToken cancellationToken = default)
    {
        _features.RequireEnabled(FeatureFlag);

        var student = await _db.Students
            .AsNoTracking()
            .Include(s => s.Identifiers)
            .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken);

        if (student is null)
            return null;

        var enrollments = await _enrollments.GetForStudentAsync(studentId, cancellationToken);

        await _audit.RecordAsync(
            new AuditEntry(AuditAction.View, "Student", studentId, studentId, "Opened student record."),
            cancellationToken);

        return new StudentDetail(
            new StudentRecord(student.Id, student.FirstName, student.MiddleName, student.LastName, student.BirthDate),
            student.Identifiers
                .OrderBy(i => i.SourceSystem)
                .Select(i => new StudentIdentifierInfo(i.SourceSystem, i.Value))
                .ToList(),
            enrollments);
    }
}
