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
}
