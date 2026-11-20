using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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

    private static readonly JsonSerializerOptions WriteJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

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

    public async Task<EdFiWriteResult> UpsertStudentAsync(
        EdFiStudentWrite student,
        CancellationToken cancellationToken = default)
    {
        _features.RequireEnabled(FeatureFlag);
        var payload = EdFiWritePayloads.Student(student);
        return await PostResourceAsync("students", payload, cancellationToken);
    }

    public async Task<EdFiWriteResult> UpsertStudentSchoolAssociationAsync(
        EdFiEnrollmentWrite enrollment,
        CancellationToken cancellationToken = default)
    {
        _features.RequireEnabled(FeatureFlag);
        var payload = EdFiWritePayloads.Enrollment(enrollment);
        return await PostResourceAsync("studentSchoolAssociations", payload, cancellationToken);
    }

    public async Task<EdFiWriteResult> UpsertStudentEducationOrganizationAssociationAsync(
        EdFiStudentEdOrgWrite association,
        CancellationToken cancellationToken = default)
    {
        _features.RequireEnabled(FeatureFlag);
        var payload = EdFiWritePayloads.StudentEdOrg(association);
        return await PostResourceAsync("studentEducationOrganizationAssociations", payload, cancellationToken);
    }

    public async Task<EdFiWriteResult> UpsertStudentIdentificationCodeAsync(
        EdFiStudentIdentificationCodeWrite code,
        CancellationToken cancellationToken = default)
    {
        _features.RequireEnabled(FeatureFlag);
        var payload = EdFiWritePayloads.IdentificationCode(code);
        return await PostResourceAsync("studentIdentificationCodes", payload, cancellationToken);
    }

    /// <summary>
    /// POSTs one resource. 200/201 = success (Ed-Fi POST is upsert by natural
    /// key). A 401 refreshes the bearer token once. A cold-start 404, a 5xx,
    /// a timeout or a transport failure is retried up to WriteRetryCount
    /// times; 400/403/409 and every other status return at once with the
    /// parsed Problem Details. Logs carry the resource, status, problem type
    /// and correlation id - never the request body.
    /// </summary>
    private async Task<EdFiWriteResult> PostResourceAsync(
        string resource,
        object payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, payload.GetType(), WriteJsonOptions);
        var maxAttempts = 1 + Math.Max(0, _options.WriteRetryCount);
        var tokenRefreshed = false;
        var attempt = 0;

        while (true)
        {
            attempt++;
            var token = await GetAccessTokenAsync(cancellationToken);
            var client = CreateClient();

            using var request = new HttpRequestMessage(HttpMethod.Post, $"data/v3/ed-fi/{resource}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            try
            {
                response = await client.SendAsync(request, cancellationToken);
            }
            catch (Exception ex) when (IsTransientTransportFailure(ex, cancellationToken) && attempt < maxAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "Ed-Fi POST {Resource} transport failure (attempt {Attempt}/{MaxAttempts}); retrying.",
                    resource, attempt, maxAttempts);
                await DelayBeforeRetryAsync(cancellationToken);
                continue;
            }

            using (response)
            {
                var status = (int)response.StatusCode;
                if (status is 200 or 201)
                {
                    _logger.LogDebug(
                        "Ed-Fi POST {Resource} -> {Status} (attempt {Attempt}).",
                        resource, status, attempt);
                    return new EdFiWriteResult(resource, true, status, attempt, null);
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (status == 401 && !tokenRefreshed)
                {
                    _logger.LogWarning(
                        "Ed-Fi POST {Resource} -> 401; refreshing the access token once.",
                        resource);
                    InvalidateCachedToken();
                    tokenRefreshed = true;
                    continue;
                }

                if (IsTransientStatus(status) && attempt < maxAttempts)
                {
                    _logger.LogWarning(
                        "Ed-Fi POST {Resource} -> {Status} (attempt {Attempt}/{MaxAttempts}); retrying.",
                        resource, status, attempt, maxAttempts);
                    await DelayBeforeRetryAsync(cancellationToken);
                    continue;
                }

                var problem = EdFiProblemParser.Parse(status, body);
                _logger.LogWarning(
                    "Ed-Fi POST {Resource} failed: {Status} {ProblemType} (correlationId {CorrelationId}, attempts {Attempts}).",
                    resource, status, problem.Type ?? "(none)", problem.CorrelationId ?? "(none)", attempt);

                return new EdFiWriteResult(resource, false, status, attempt, problem);
            }
        }
    }

    private static bool IsTransientStatus(int status) => status == 404 || status >= 500;

    private static bool IsTransientTransportFailure(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException
        || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private Task DelayBeforeRetryAsync(CancellationToken cancellationToken) =>
        _options.WriteRetryDelayMs > 0
            ? Task.Delay(_options.WriteRetryDelayMs, cancellationToken)
            : Task.CompletedTask;

    private void InvalidateCachedToken()
    {
        _cachedToken = null;
        _tokenExpiresAt = DateTimeOffset.MinValue;
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
