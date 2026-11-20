namespace Phoenix.Core.Modules.EdFi;

/// <summary>
/// An Ed-Fi StudentEducationOrganizationAssociation - ties the student to
/// an education organization (Phoenix writes the LEA). Natural key: student,
/// education organization.
/// </summary>
public sealed record EdFiStudentEdOrgWrite(
    string StudentUniqueId,
    long EducationOrganizationId);
