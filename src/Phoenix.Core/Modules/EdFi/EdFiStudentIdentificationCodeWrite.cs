namespace Phoenix.Core.Modules.EdFi;

/// <summary>
/// An Ed-Fi StudentIdentificationCode (its own resource in DS 6.1).
/// Natural key: student, education organization, identification system.
/// Phoenix never sends Social Security numbers: the client rejects the SSN
/// identification system before any network call.
/// </summary>
public sealed record EdFiStudentIdentificationCodeWrite(
    string StudentUniqueId,
    long EducationOrganizationId,
    string StudentIdentificationSystemDescriptor,
    string IdentificationCode,
    string? AssigningOrganizationIdentificationCode = null);
