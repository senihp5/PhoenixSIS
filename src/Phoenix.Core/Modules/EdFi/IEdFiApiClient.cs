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
}
