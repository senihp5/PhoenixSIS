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
}
