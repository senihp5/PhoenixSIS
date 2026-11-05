using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Phoenix.Core.Features;
using Phoenix.Infrastructure.EdFi;

namespace Phoenix.Infrastructure.Tests;

/// <summary>
/// Opt-in live integration test against a real Ed-Fi sandbox. Runs only when
/// EDFI_SMOKE_KEY and EDFI_SMOKE_SECRET are present in the environment; it is
/// a no-op pass in CI (and for anyone without sandbox creds). Exercises the
/// real EdFiApiClient HTTP path end to end.
/// </summary>
public class EdFiApiClientLiveSmoke
{
    [Fact]
    public async Task Live_discovery_and_token()
    {
        var key = Environment.GetEnvironmentVariable("EDFI_SMOKE_KEY");
        var secret = Environment.GetEnvironmentVariable("EDFI_SMOKE_SECRET");
        var baseUrl = Environment.GetEnvironmentVariable("EDFI_SMOKE_BASEURL")
            ?? "https://nh-edfi-cus.azurewebsites.net";

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(secret))
            return; // creds not provided — skip the live call

        var client = new EdFiApiClient(
            new RealFactory(),
            Options.Create(new EdFiApiOptions { BaseUrl = baseUrl, Key = key, Secret = secret }),
            new AlwaysEnabled(),
            NullLogger<EdFiApiClient>.Instance);

        var info = await client.GetApiInfoAsync();
        Assert.StartsWith("7.3", info.Version);

        var token = await client.GetAccessTokenAsync();
        Assert.False(string.IsNullOrWhiteSpace(token));

        var students = await client.GetStudentsAsync(3);
        Assert.NotNull(students);
    }

    private sealed class RealFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }

    private sealed class AlwaysEnabled : IFeatureFlagService
    {
        public bool IsEnabled(string featureName) => true;
        public IReadOnlyDictionary<string, FeatureFlagState> GetAll() =>
            new Dictionary<string, FeatureFlagState>();
        public void RequireEnabled(string featureName) { }
    }
}
