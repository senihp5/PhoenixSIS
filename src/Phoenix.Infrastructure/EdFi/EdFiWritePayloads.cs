using System.Text.Json.Serialization;
using Phoenix.Core.Modules.EdFi;

namespace Phoenix.Infrastructure.EdFi;

/// <summary>
/// Maps the Phoenix.Core write contracts to the Ed-Fi DS 6.1 resource JSON
/// (property names and nested references verified against the live
/// nh-edfi-cus OpenAPI, 2026-11-20). Null optionals are omitted by the
/// serializer, so an unset field is never sent as null.
/// </summary>
internal static class EdFiWritePayloads
{
    private const string SsnSystemSuffix = "#SSN";

    public static object Student(EdFiStudentWrite student)
    {
        ArgumentNullException.ThrowIfNull(student);
        ArgumentException.ThrowIfNullOrWhiteSpace(student.StudentUniqueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(student.FirstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(student.LastSurname);

        return new StudentPayload(
            student.StudentUniqueId,
            student.FirstName,
            student.MiddleName,
            student.LastSurname,
            student.GenerationCodeSuffix,
            student.BirthDate);
    }

    public static object Enrollment(EdFiEnrollmentWrite enrollment)
    {
        ArgumentNullException.ThrowIfNull(enrollment);
        ArgumentException.ThrowIfNullOrWhiteSpace(enrollment.StudentUniqueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(enrollment.EntryGradeLevelDescriptor);

        if (enrollment.CalendarCode is not null && enrollment.SchoolYear is null)
        {
            throw new ArgumentException(
                "A calendar reference needs SchoolYear as well as CalendarCode.",
                nameof(enrollment));
        }

        return new StudentSchoolAssociationPayload(
            new StudentReference(enrollment.StudentUniqueId),
            new SchoolReference(enrollment.SchoolId),
            enrollment.EntryDate,
            enrollment.EntryGradeLevelDescriptor,
            enrollment.SchoolYear is int year ? new SchoolYearTypeReference(year) : null,
            enrollment.CalendarCode is string code
                ? new CalendarReference(code, enrollment.SchoolId, enrollment.SchoolYear!.Value)
                : null,
            enrollment.PrimarySchool,
            enrollment.EnrollmentTypeDescriptor,
            enrollment.FullTimeEquivalency,
            enrollment.ExitWithdrawDate,
            enrollment.ExitWithdrawTypeDescriptor);
    }

    public static object StudentEdOrg(EdFiStudentEdOrgWrite association)
    {
        ArgumentNullException.ThrowIfNull(association);
        ArgumentException.ThrowIfNullOrWhiteSpace(association.StudentUniqueId);

        return new StudentEducationOrganizationAssociationPayload(
            new StudentReference(association.StudentUniqueId),
            new EducationOrganizationReference(association.EducationOrganizationId));
    }

    public static object IdentificationCode(EdFiStudentIdentificationCodeWrite code)
    {
        ArgumentNullException.ThrowIfNull(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(code.StudentUniqueId);
        ArgumentException.ThrowIfNullOrWhiteSpace(code.StudentIdentificationSystemDescriptor);
        ArgumentException.ThrowIfNullOrWhiteSpace(code.IdentificationCode);

        if (IsSsnSystem(code.StudentIdentificationSystemDescriptor))
        {
            throw new InvalidOperationException(
                "Phoenix never sends Social Security numbers to Ed-Fi.");
        }

        return new StudentIdentificationCodePayload(
            new StudentReference(code.StudentUniqueId),
            new EducationOrganizationReference(code.EducationOrganizationId),
            code.StudentIdentificationSystemDescriptor,
            code.IdentificationCode,
            code.AssigningOrganizationIdentificationCode);
    }

    public static bool IsSsnSystem(string descriptor) =>
        descriptor.Trim().EndsWith(SsnSystemSuffix, StringComparison.OrdinalIgnoreCase);

    private sealed record StudentReference(
        [property: JsonPropertyName("studentUniqueId")] string StudentUniqueId);

    private sealed record SchoolReference(
        [property: JsonPropertyName("schoolId")] long SchoolId);

    private sealed record EducationOrganizationReference(
        [property: JsonPropertyName("educationOrganizationId")] long EducationOrganizationId);

    private sealed record SchoolYearTypeReference(
        [property: JsonPropertyName("schoolYear")] int SchoolYear);

    private sealed record CalendarReference(
        [property: JsonPropertyName("calendarCode")] string CalendarCode,
        [property: JsonPropertyName("schoolId")] long SchoolId,
        [property: JsonPropertyName("schoolYear")] int SchoolYear);

    private sealed record StudentPayload(
        [property: JsonPropertyName("studentUniqueId")] string StudentUniqueId,
        [property: JsonPropertyName("firstName")] string FirstName,
        [property: JsonPropertyName("middleName")] string? MiddleName,
        [property: JsonPropertyName("lastSurname")] string LastSurname,
        [property: JsonPropertyName("generationCodeSuffix")] string? GenerationCodeSuffix,
        [property: JsonPropertyName("birthDate")] DateOnly BirthDate);

    private sealed record StudentSchoolAssociationPayload(
        [property: JsonPropertyName("studentReference")] StudentReference StudentReference,
        [property: JsonPropertyName("schoolReference")] SchoolReference SchoolReference,
        [property: JsonPropertyName("entryDate")] DateOnly EntryDate,
        [property: JsonPropertyName("entryGradeLevelDescriptor")] string EntryGradeLevelDescriptor,
        [property: JsonPropertyName("schoolYearTypeReference")] SchoolYearTypeReference? SchoolYearTypeReference,
        [property: JsonPropertyName("calendarReference")] CalendarReference? CalendarReference,
        [property: JsonPropertyName("primarySchool")] bool? PrimarySchool,
        [property: JsonPropertyName("enrollmentTypeDescriptor")] string? EnrollmentTypeDescriptor,
        [property: JsonPropertyName("fullTimeEquivalency")] double? FullTimeEquivalency,
        [property: JsonPropertyName("exitWithdrawDate")] DateOnly? ExitWithdrawDate,
        [property: JsonPropertyName("exitWithdrawTypeDescriptor")] string? ExitWithdrawTypeDescriptor);

    private sealed record StudentEducationOrganizationAssociationPayload(
        [property: JsonPropertyName("studentReference")] StudentReference StudentReference,
        [property: JsonPropertyName("educationOrganizationReference")] EducationOrganizationReference EducationOrganizationReference);

    private sealed record StudentIdentificationCodePayload(
        [property: JsonPropertyName("studentReference")] StudentReference StudentReference,
        [property: JsonPropertyName("educationOrganizationReference")] EducationOrganizationReference EducationOrganizationReference,
        [property: JsonPropertyName("studentIdentificationSystemDescriptor")] string StudentIdentificationSystemDescriptor,
        [property: JsonPropertyName("identificationCode")] string IdentificationCode,
        [property: JsonPropertyName("assigningOrganizationIdentificationCode")] string? AssigningOrganizationIdentificationCode);
}
