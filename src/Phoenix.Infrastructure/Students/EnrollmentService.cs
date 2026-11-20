using Microsoft.EntityFrameworkCore;
using Phoenix.Core.Auditing;
using Phoenix.Core.Students;
using Phoenix.Infrastructure.Data;

namespace Phoenix.Infrastructure.Students;

/// <summary>
/// Sprint 8 enrollment service. Rules (Sprint 8 cards):
///  - the campus is in the campus directory;
///  - the entry date falls inside the school year (Jul 1 .. Jun 30);
///  - 0 &lt; FTE &lt;= 1;
///  - a new enrollment may not overlap another at the same campus (an
///    earlier one must have exited before the new entry date);
///  - one open primary enrollment per student per school year;
///  - an exit is on or after entry and inside the school year, once only.
/// Every change is audited in the same save (ADR-004 fail closed). Audit
/// summaries carry only the campus number and school year - never names or
/// record content (dates, grade, exit reason).
/// </summary>
public sealed class EnrollmentService : IEnrollmentService
{
    private const string EntityType = "Enrollment";

    private readonly PhoenixDbContext _db;
    private readonly IAuditLog _audit;
    private readonly ICampusDirectory _campuses;

    public EnrollmentService(PhoenixDbContext db, IAuditLog audit, ICampusDirectory campuses)
    {
        _db = db;
        _audit = audit;
        _campuses = campuses;
    }

    public async Task<IReadOnlyList<EnrollmentRecord>> GetForStudentAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.SchoolYear)
            .ThenByDescending(e => e.EntryDate)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<EnrollmentRecord>> GetForCampusYearAsync(
        string campusNumber,
        int schoolYear,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.Enrollments
            .AsNoTracking()
            .Where(e => e.CampusNumber == campusNumber && e.SchoolYear == schoolYear)
            .OrderBy(e => e.StudentId)
            .ThenBy(e => e.EntryDate)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public async Task<EnrollmentRecord> EnrollAsync(
        NewEnrollment enrollment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(enrollment);

        var campus = RequireCampus(enrollment.CampusNumber);
        ValidateYearAndDate(enrollment.SchoolYear, enrollment.EntryDate, "entry date");
        ValidateFte(enrollment.FullTimeEquivalency);

        if (!await _db.Students.AnyAsync(s => s.Id == enrollment.StudentId, cancellationToken))
            throw new EnrollmentRuleException("The student does not exist.");

        var existing = await _db.Enrollments
            .Where(e => e.StudentId == enrollment.StudentId)
            .ToListAsync(cancellationToken);

        CheckOverlap(existing, campus.Number, enrollment.EntryDate);

        if (enrollment.IsPrimary
            && existing.Any(e => e.SchoolYear == enrollment.SchoolYear && e.IsPrimary && e.ExitDate is null))
        {
            throw new EnrollmentRuleException(
                $"The student already has an open primary enrollment for school year {FormatYear(enrollment.SchoolYear)}.");
        }

        var row = NewRow(enrollment.StudentId, campus.Number, enrollment.SchoolYear, enrollment.EntryDate,
            enrollment.EntryGradeLevel, enrollment.Kind, enrollment.FullTimeEquivalency, enrollment.IsPrimary);

        _db.Enrollments.Add(row);
        await _audit.StageAsync(EnrollEntry(row), cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Map(row);
    }

    public async Task<EnrollmentRecord> ExitAsync(
        Guid enrollmentId,
        DateOnly exitDate,
        ExitReason reason,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.Enrollments.FirstOrDefaultAsync(e => e.Id == enrollmentId, cancellationToken)
            ?? throw new EnrollmentRuleException("The enrollment does not exist.");

        if (row.ExitDate is not null)
            throw new EnrollmentRuleException("The enrollment has already been exited.");

        if (exitDate < row.EntryDate)
            throw new EnrollmentRuleException("The exit date cannot be before the entry date.");

        ValidateYearAndDate(row.SchoolYear, exitDate, "exit date");

        row.ExitDate = exitDate;
        row.ExitReason = reason;
        row.UpdatedUtc = DateTimeOffset.UtcNow;

        await _audit.StageAsync(
            new AuditEntry(
                AuditAction.Exit,
                EntityType,
                row.Id,
                row.StudentId,
                $"Exited campus {row.CampusNumber}, school year {FormatYear(row.SchoolYear)}."),
            cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return Map(row);
    }

    public async Task<BulkEnrollResult> EnrollManyAsync(
        BulkEnrollRequest request,
        bool apply,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var campus = RequireCampus(request.CampusNumber);
        ValidateYearAndDate(request.SchoolYear, request.EntryDate, "entry date");
        ValidateFte(request.FullTimeEquivalency);

        var studentIds = await _db.Students
            .AsNoTracking()
            .OrderBy(s => s.LastName)
            .ThenBy(s => s.FirstName)
            .ThenBy(s => s.Id)
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);

        var enrollments = await _db.Enrollments
            .Where(e => studentIds.Contains(e.StudentId))
            .ToListAsync(cancellationToken);
        var byStudent = enrollments.ToLookup(e => e.StudentId);

        var toEnroll = new List<Guid>();
        var alreadyEnrolled = 0;

        foreach (var studentId in studentIds)
        {
            var mine = byStudent[studentId].ToList();

            if (mine.Any(e => e.CampusNumber == campus.Number && e.SchoolYear == request.SchoolYear))
            {
                alreadyEnrolled++;
                continue;
            }

            // Bulk enroll never breaks a rule: a student who would overlap at
            // this campus or already has an open primary for the year is left
            // for an individual decision on the student page.
            var overlaps = mine.Any(e => e.CampusNumber == campus.Number
                && (e.ExitDate is null || e.ExitDate >= request.EntryDate));
            var hasPrimary = mine.Any(e => e.SchoolYear == request.SchoolYear && e.IsPrimary && e.ExitDate is null);
            if (overlaps || hasPrimary)
            {
                alreadyEnrolled++;
                continue;
            }

            toEnroll.Add(studentId);
        }

        if (!apply || toEnroll.Count == 0)
            return new BulkEnrollResult(apply, toEnroll, alreadyEnrolled);

        var rows = toEnroll
            .Select(id => NewRow(id, campus.Number, request.SchoolYear, request.EntryDate,
                request.EntryGradeLevel, request.Kind, request.FullTimeEquivalency, isPrimary: true))
            .ToList();

        _db.Enrollments.AddRange(rows);
        await _audit.StageAsync(rows.Select(EnrollEntry), cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);

        return new BulkEnrollResult(true, toEnroll, alreadyEnrolled);
    }

    private CampusInfo RequireCampus(string campusNumber) =>
        _campuses.Find(campusNumber)
            ?? throw new EnrollmentRuleException($"Campus {campusNumber} is not a configured Phoenix campus.");

    private static void ValidateYearAndDate(int schoolYear, DateOnly date, string label)
    {
        if (schoolYear is < 2000 or > 2100)
            throw new EnrollmentRuleException($"School year {schoolYear} is out of range.");

        var start = new DateOnly(schoolYear - 1, 7, 1);
        var end = new DateOnly(schoolYear, 6, 30);
        if (date < start || date > end)
        {
            throw new EnrollmentRuleException(
                $"The {label} {date:yyyy-MM-dd} is outside school year {FormatYear(schoolYear)} ({start:yyyy-MM-dd} to {end:yyyy-MM-dd}).");
        }
    }

    private static void ValidateFte(decimal fte)
    {
        if (fte <= 0m || fte > 1m)
            throw new EnrollmentRuleException("Full-time equivalency must be greater than 0 and at most 1.");
    }

    private static void CheckOverlap(IEnumerable<Enrollment> existing, string campusNumber, DateOnly entryDate)
    {
        var clash = existing.FirstOrDefault(e => e.CampusNumber == campusNumber
            && (e.ExitDate is null || e.ExitDate >= entryDate));

        if (clash is not null)
        {
            throw new EnrollmentRuleException(clash.ExitDate is null
                ? $"The student already has an open enrollment at campus {campusNumber} (entered {clash.EntryDate:yyyy-MM-dd}). Exit it first."
                : $"The new entry date must be after the previous exit on {clash.ExitDate:yyyy-MM-dd} at campus {campusNumber}.");
        }
    }

    private static Enrollment NewRow(
        Guid studentId,
        string campusNumber,
        int schoolYear,
        DateOnly entryDate,
        GradeLevel grade,
        EnrollmentKind kind,
        decimal fte,
        bool isPrimary)
    {
        var now = DateTimeOffset.UtcNow;
        return new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            CampusNumber = campusNumber,
            SchoolYear = schoolYear,
            EntryDate = entryDate,
            EntryGradeLevel = grade,
            Kind = kind,
            FullTimeEquivalency = fte,
            IsPrimary = isPrimary,
            CreatedUtc = now,
            UpdatedUtc = now
        };
    }

    private static AuditEntry EnrollEntry(Enrollment row) =>
        new(
            AuditAction.Enroll,
            EntityType,
            row.Id,
            row.StudentId,
            $"Enrolled at campus {row.CampusNumber}, school year {FormatYear(row.SchoolYear)}.");

    private static string FormatYear(int schoolYear) => $"{schoolYear - 1}-{schoolYear % 100:D2}";

    internal static EnrollmentRecord Map(Enrollment e) =>
        new(e.Id, e.StudentId, e.CampusNumber, e.SchoolYear, e.EntryDate, e.EntryGradeLevel,
            e.Kind, e.FullTimeEquivalency, e.IsPrimary, e.ExitDate, e.ExitReason);
}
