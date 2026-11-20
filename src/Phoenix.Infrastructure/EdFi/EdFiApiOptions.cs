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

    /// <summary>Entry date sent on new enrollments (yyyy-MM-dd).</summary>
    public string DefaultEntryDate { get; set; } = "2026-08-17";

    public string DefaultEntryGradeLevelDescriptor { get; set; } =
        "uri://ed-fi.org/GradeLevelDescriptor#Adult Education";

    public string? DefaultEnrollmentTypeDescriptor { get; set; } =
        "uri://ed-fi.org/EnrollmentTypeDescriptor#Current";

    public double? DefaultFullTimeEquivalency { get; set; } = 1.0;

    public string LocalIdentificationSystemDescriptor { get; set; } =
        "uri://ed-fi.org/StudentIdentificationSystemDescriptor#Local";
}
