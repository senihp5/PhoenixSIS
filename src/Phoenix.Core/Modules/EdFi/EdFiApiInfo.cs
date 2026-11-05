namespace Phoenix.Core.Modules.EdFi;

/// <summary>
/// Summary of the connected Ed-Fi ODS/API, read from the discovery
/// endpoint. A Phoenix.Core contract so the UI can consume it without
/// referencing Infrastructure.
/// </summary>
public sealed record EdFiApiInfo(string Version, string Build, string DataModels);
