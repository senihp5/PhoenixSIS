namespace Phoenix.Core.Modules.EdFi;

/// <summary>
/// The Ed-Fi Student fields Phoenix writes (DS 6.1 required:
/// studentUniqueId, firstName, lastSurname, birthDate). Demographics,
/// contact details and identification codes are separate resources in
/// DS 6.1 and are not part of this record.
/// </summary>
public sealed record EdFiStudentWrite(
    string StudentUniqueId,
    string FirstName,
    string LastSurname,
    DateOnly BirthDate,
    string? MiddleName = null,
    string? GenerationCodeSuffix = null);
