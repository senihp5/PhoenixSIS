using Microsoft.EntityFrameworkCore;
using Phoenix.Core.Students;
using Phoenix.Infrastructure.Data;

namespace Phoenix.Infrastructure.Students;

public sealed class StudentIdentityService : IStudentIdentityService
{
    private readonly PhoenixDbContext _db;

    public StudentIdentityService(PhoenixDbContext db) => _db = db;

    public async Task<StudentRecord?> GetByIdentifierAsync(
        string sourceSystem, string externalId, CancellationToken cancellationToken = default)
    {
        var identifier = await _db.StudentIdentifiers
            .Include(i => i.Student)
            .FirstOrDefaultAsync(
                i => i.SourceSystem == sourceSystem && i.Value == externalId,
                cancellationToken);

        return identifier?.Student is null ? null : Map(identifier.Student);
    }

    public async Task<StudentRecord> ResolveOrCreateAsync(
        string sourceSystem, string externalId, StudentDetails details,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.StudentIdentifiers
            .Include(i => i.Student)
            .FirstOrDefaultAsync(
                i => i.SourceSystem == sourceSystem && i.Value == externalId,
                cancellationToken);

        if (existing?.Student is not null)
            return Map(existing.Student);

        var now = DateTimeOffset.UtcNow;
        var student = new Student
        {
            Id = Guid.NewGuid(),
            FirstName = details.FirstName,
            MiddleName = details.MiddleName,
            LastName = details.LastName,
            BirthDate = details.BirthDate,
            CreatedUtc = now,
            UpdatedUtc = now
        };
        student.Identifiers.Add(new StudentIdentifier
        {
            Id = Guid.NewGuid(),
            SourceSystem = sourceSystem,
            Value = externalId
        });

        _db.Students.Add(student);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(student);
    }

    public async Task AddIdentifierAsync(
        Guid studentId, string sourceSystem, string externalId,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.StudentIdentifiers.FirstOrDefaultAsync(
            i => i.SourceSystem == sourceSystem && i.Value == externalId,
            cancellationToken);

        if (existing is not null)
        {
            if (existing.StudentId != studentId)
                throw new InvalidOperationException(
                    $"Identifier {sourceSystem}:{externalId} is already mapped to a different student.");
            return; // already mapped to this student — idempotent
        }

        var studentExists = await _db.Students.AnyAsync(s => s.Id == studentId, cancellationToken);
        if (!studentExists)
            throw new InvalidOperationException($"Student {studentId} does not exist.");

        _db.StudentIdentifiers.Add(new StudentIdentifier
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            SourceSystem = sourceSystem,
            Value = externalId
        });
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static StudentRecord Map(Student s) =>
        new(s.Id, s.FirstName, s.MiddleName, s.LastName, s.BirthDate);
}
