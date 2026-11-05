using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Phoenix.Core.Features;
using Phoenix.Core.Modules.EdFi;

namespace Phoenix.Infrastructure.EdFi;

/// <summary>
/// Ed-Fi ODS/API client. Registered as a singleton so the bearer-token
/// cache survives across calls; it pulls a fresh <see cref="HttpClient"/>
/// from <see cref="IHttpClientFactory"/> per request to avoid socket
/// exhaustion. Every public method is guarded by the EdFi feature flag.
/// </summary>
public sealed class EdFiApiClient : IEdFiApiClient
{
    public const string HttpClientName = "EdFiApi";
    private const string FeatureFlag = "EdFi";
    private static readonly TimeSpan TokenRefreshSkew = TimeSpan.FromSeconds(60);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly EdFiApiOptions _options;
    private readonly IFeatureFlagService _features;
    private readonly ILogger<EdFiApiClient> _logger;

    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _cachedToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    public EdFiApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<EdFiApiOptions> options,
        IFeatureFlagService features,
        ILogger<EdFiApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _features = features;
        _logger = logger;
    }

    public async Task<EdFiApiInfo> GetApiInfoAsync(CancellationToken cancellationToken = default)
    {
        _features.RequireEnabled(FeatureFlag);

        var client = CreateClient();
        using var response = await client.GetAsync(string.Empty, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content
            .ReadFromJsonAsync<DiscoveryResponse>(cancellationToken);
        if (payload is null)
            throw new InvalidOperationException("Ed-Fi discovery endpoint returned no content.");

        var models = payload.DataModels is { Count: > 0 }
            ? string.Join(", ", payload.DataModels.Select(m => $"{m.Name} {m.Version}"))
            : "(none reported)";

        return new EdFiApiInfo(
            payload.Version ?? "unknown",
            payload.Build ?? "unknown",
            models);
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        _features.RequireEnabled(FeatureFlag);

        if (TryGetCachedToken(out var cached))
            return cached;

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            // Re-check inside the lock: another caller may have refreshed.
            if (TryGetCachedToken(out cached))
                return cached;

            var client = CreateClient();
            var basic = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{_options.Key}:{_options.Secret}"));

            using var request = new HttpRequestMessage(HttpMethod.Post, "oauth/token");
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials"
            });

            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var token = await response.Content
                .ReadFromJsonAsync<TokenResponse>(cancellationToken);
            if (token is null || string.IsNullOrEmpty(token.AccessToken))
                throw new InvalidOperationException("Ed-Fi token endpoint returned no access token.");

            _cachedToken = token.AccessToken;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn);
            _logger.LogInformation(
                "Ed-Fi access token acquired (expires in {Seconds}s).",
                token.ExpiresIn);

            return _cachedToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    public async Task<IReadOnlyList<EdFiStudentSummary>> GetStudentsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        _features.RequireEnabled(FeatureFlag);

        var safeLimit = Math.Clamp(limit, 1, 500);
        var token = await GetAccessTokenAsync(cancellationToken);
        var client = CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"data/v3/ed-fi/students?limit={safeLimit}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var students = await response.Content
            .ReadFromJsonAsync<List<StudentResource>>(cancellationToken) ?? [];

        return students
            .Select(s => new EdFiStudentSummary(
                s.StudentUniqueId ?? string.Empty,
                s.FirstName ?? string.Empty,
                s.LastSurname ?? string.Empty,
                s.BirthDate))
            .ToList();
    }

    private bool TryGetCachedToken(out string token)
    {
        if (_cachedToken is not null
            && DateTimeOffset.UtcNow < _tokenExpiresAt - TokenRefreshSkew)
        {
            token = _cachedToken;
            return true;
        }

        token = string.Empty;
        return false;
    }

    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        if (client.BaseAddress is null)
        {
            if (string.IsNullOrWhiteSpace(_options.BaseUrl))
                throw new InvalidOperationException("EdFiApi:BaseUrl is not configured.");

            var baseUrl = _options.BaseUrl.EndsWith('/')
                ? _options.BaseUrl
                : _options.BaseUrl + "/";
            client.BaseAddress = new Uri(baseUrl);
        }

        return client;
    }

    private sealed record DiscoveryResponse(
        [property: JsonPropertyName("version")] string? Version,
        [property: JsonPropertyName("build")] string? Build,
        [property: JsonPropertyName("dataModels")] IReadOnlyList<DataModel>? DataModels);

    private sealed record DataModel(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("version")] string? Version);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("token_type")] string? TokenType);

    private sealed record StudentResource(
        [property: JsonPropertyName("studentUniqueId")] string? StudentUniqueId,
        [property: JsonPropertyName("firstName")] string? FirstName,
        [property: JsonPropertyName("lastSurname")] string? LastSurname,
        [property: JsonPropertyName("birthDate")] string? BirthDate);
}
