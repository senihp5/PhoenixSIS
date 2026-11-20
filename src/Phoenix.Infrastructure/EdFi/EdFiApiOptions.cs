namespace Phoenix.Infrastructure.EdFi;

/// <summary>
/// Configuration for the Ed-Fi API client, bound from the "EdFiApi"
/// section. Kept distinct from the "Features:EdFi" flag. BaseUrl comes from
/// appsettings; Key and Secret come from dotnet user-secrets locally (and
/// App Service settings / Key Vault when deployed) — never the repo.
/// </summary>
public sealed class EdFiApiOptions
{
    public const string SectionName = "EdFiApi";

    public string BaseUrl { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;

    /// <summary>
    /// Extra attempts for a write that fails transiently: a cold-start 404
    /// (nh-edfi-cus spins down when idle) or a 5xx. Never applied to
    /// 400/403/409.
    /// </summary>
    public int WriteRetryCount { get; set; } = 3;

    /// <summary>Delay between write retries, in milliseconds.</summary>
    public int WriteRetryDelayMs { get; set; } = 2000;

    // PHX-7.2 student sync defaults (non-secret). Sample-data values that
    // match what _tools\Seed-EdfiReference.ps1 creates in the ODS.

    public long DefaultLeaId { get; set; } = 220822;
    public long DefaultSchoolId { get; set; } = 220822001;
    public int SchoolYear { get; set; } = 2027;
    public string? CalendarCode { get; set; } = "220822001-2027";

    // PHX-8.4 boundary mapping: Phoenix codes (enum names in
    // Phoenix.Core.Students) -> Ed-Fi descriptor URIs. Every URI below was
    // read from the live nh-edfi-cus descriptors on 2026-11-20. Override a
    // single key in configuration, e.g.
    // EdFiApi:ExitWithdrawTypeDescriptors:NonAttendance.

    /// <summary>Phoenix GradeLevel -> Ed-Fi GradeLevelDescriptor.</summary>
    public Dictionary<string, string> GradeLevelDescriptors { get; set; } = new()
    {
        ["AdultEducation"] = "uri://ed-fi.org/GradeLevelDescriptor#Adult Education",
        ["Grade09"] = "uri://ed-fi.org/GradeLevelDescriptor#Ninth grade",
        ["Grade10"] = "uri://ed-fi.org/GradeLevelDescriptor#Tenth grade",
        ["Grade11"] = "uri://ed-fi.org/GradeLevelDescriptor#Eleventh grade",
        ["Grade12"] = "uri://ed-fi.org/GradeLevelDescriptor#Twelfth grade",
        ["Ungraded"] = "uri://ed-fi.org/GradeLevelDescriptor#Ungraded"
    };

    /// <summary>Phoenix EnrollmentKind -> Ed-Fi EnrollmentTypeDescriptor.</summary>
    public Dictionary<string, string> EnrollmentTypeDescriptors { get; set; } = new()
    {
        ["Regular"] = "uri://ed-fi.org/EnrollmentTypeDescriptor#Current",
        ["Summer"] = "uri://ed-fi.org/EnrollmentTypeDescriptor#Summer",
        ["Preregistered"] = "uri://ed-fi.org/EnrollmentTypeDescriptor#Preregistered"
    };

    /// <summary>
    /// Phoenix ExitReason -> Ed-Fi ExitWithdrawTypeDescriptor. NonAttendance
    /// has no exact core Ed-Fi value ("No show" means never attended), so it
    /// maps to Withdrawn until the Texas leaver codes arrive with the TX
    /// extension.
    /// </summary>
    public Dictionary<string, string> ExitWithdrawTypeDescriptors { get; set; } = new()
    {
        ["Graduated"] = "uri://ed-fi.org/ExitWithdrawTypeDescriptor#Graduated",
        ["TransferredOut"] = "uri://ed-fi.org/ExitWithdrawTypeDescriptor#Transferred",
        ["Withdrew"] = "uri://ed-fi.org/ExitWithdrawTypeDescriptor#Withdrawn",
        ["NonAttendance"] = "uri://ed-fi.org/ExitWithdrawTypeDescriptor#Withdrawn",
        ["Other"] = "uri://ed-fi.org/ExitWithdrawTypeDescriptor#Other"
    };

    /// <summary>
    /// Looks a Phoenix code up in one of the maps above. Case-insensitive,
    /// because configuration keys are. Null when the code is unmapped or
    /// mapped to an empty value.
    /// </summary>
    public static string? FindDescriptor(Dictionary<string, string> map, string phoenixCode)
    {
        ArgumentNullException.ThrowIfNull(map);

        foreach (var pair in map)
        {
            if (string.Equals(pair.Key, phoenixCode, StringComparison.OrdinalIgnoreCase))
                return string.IsNullOrWhiteSpace(pair.Value) ? null : pair.Value;
        }

        return null;
    }

    public string LocalIdentificationSystemDescriptor { get; set; } =
        "uri://ed-fi.org/StudentIdentificationSystemDescriptor#Local";
}
