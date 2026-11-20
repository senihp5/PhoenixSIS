namespace Phoenix.Core.Modules.EdFi;

/// <summary>
/// Typed client for the Ed-Fi ODS/API. Contract lives in Phoenix.Core so
/// modules and UI depend on the abstraction, not the Infrastructure
/// implementation. All members require the EdFi feature flag to be enabled.
/// </summary>
public interface IEdFiApiClient
{
    /// <summary>
    /// Reads the Ed-Fi discovery endpoint (anonymous) and returns version,
    /// build, and data-model summary. Proves reachability.
    /// </summary>
    Task<EdFiApiInfo> GetApiInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires (and caches) an OAuth client-credentials bearer token.
    /// Proves authentication. The token is reused until shortly before it
    /// expires. Returns the access token string.
    /// </summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads up to <paramref name="limit"/> students from the ODS. Only
    /// students the caller's claim set is authorized to see are returned
    /// (relationship-based authorization).
    /// </summary>
    Task<IReadOnlyList<EdFiStudentSummary>> GetStudentsAsync(
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /ed-fi/students (upsert by studentUniqueId). Returns the outcome,
    /// including the parsed Problem Details on failure; does not throw for
    /// API errors.
    /// </summary>
    Task<EdFiWriteResult> UpsertStudentAsync(
        EdFiStudentWrite student,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /ed-fi/studentSchoolAssociations (upsert by student, school,
    /// entry date). The student must exist first.
    /// </summary>
    Task<EdFiWriteResult> UpsertStudentSchoolAssociationAsync(
        EdFiEnrollmentWrite enrollment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /ed-fi/studentEducationOrganizationAssociations (upsert by
    /// student, education organization).
    /// </summary>
    Task<EdFiWriteResult> UpsertStudentEducationOrganizationAssociationAsync(
        EdFiStudentEdOrgWrite association,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /ed-fi/studentIdentificationCodes (upsert by student, education
    /// organization, identification system). Throws
    /// <see cref="InvalidOperationException"/> for the SSN system - Phoenix
    /// never sends Social Security numbers.
    /// </summary>
    Task<EdFiWriteResult> UpsertStudentIdentificationCodeAsync(
        EdFiStudentIdentificationCodeWrite code,
        CancellationToken cancellationToken = default);
}
