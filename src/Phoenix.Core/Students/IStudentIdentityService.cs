namespace Phoenix.Core.Students;

/// <summary>
/// Reconciles source identifiers to one canonical Phoenix student (ADR-002).
/// Used by inbound migration and outbound sync alike — core, not a module.
/// </summary>
public interface IStudentIdentityService
{
    /// <summary>Returns the student mapped to (sourceSystem, externalId), or null.</summary>
    Task<StudentRecord?> GetByIdentifierAsync(
        string sourceSystem,
        string externalId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the student already mapped to (sourceSystem, externalId); if none,
    /// creates a new canonical student with that identifier. Idempotent: the same
    /// (sourceSystem, externalId) always resolves to the same student.
    /// </summary>
    Task<StudentRecord> ResolveOrCreateAsync(
        string sourceSystem,
        string externalId,
        StudentDetails details,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Maps an additional source identifier to an existing student. Idempotent
    /// when the identifier already maps to that same student; throws when it maps
    /// to a different student, or when the student does not exist.
    /// </summary>
    Task AddIdentifierAsync(
        Guid studentId,
        string sourceSystem,
        string externalId,
        CancellationToken cancellationToken = default);
}
