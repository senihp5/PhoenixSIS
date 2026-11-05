using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Phoenix.Core.Features;
using Phoenix.Infrastructure.EdFi;

namespace Phoenix.Infrastructure.Tests;

public class EdFiApiClientTests
{
    [Fact]
    public async Task GetAccessTokenAsync_caches_token_across_calls()
    {
        var handler = new StubHandler();
        var client = CreateClient(handler, enabled: true);

        var first = await client.GetAccessTokenAsync();
        var second = await client.GetAccessTokenAsync();

        Assert.Equal("test-token-123", first);
        Assert.Equal(first, second);
        Assert.Equal(1, handler.TokenRequests); // second call served from cache
    }

    [Fact]
    public async Task GetApiInfoAsync_parses_discovery()
    {
        var handler = new StubHandler();
        var client = CreateClient(handler, enabled: true);

        var info = await client.GetApiInfoAsync();

        Assert.Equal("7.3", info.Version);
        Assert.Equal("7.3.20432.0", info.Build);
        Assert.Contains("Ed-Fi 6.1.0", info.DataModels);
        Assert.Equal(0, handler.TokenRequests); // discovery is anonymous
    }

    [Fact]
    public async Task GetStudentsAsync_maps_students_and_sends_bearer()
    {
        var handler = new StubHandler();
        var client = CreateClient(handler, enabled: true);

        var students = await client.GetStudentsAsync(25);

        Assert.Equal(2, students.Count);
        Assert.Equal("100001", students[0].StudentUniqueId);
        Assert.Equal("Lisa", students[0].FirstName);
        Assert.Equal("Woods", students[0].LastSurname);
        Assert.Equal("test-token-123", handler.LastBearer); // authenticated call
    }

    [Fact]
    public async Task Methods_throw_when_flag_disabled()
    {
        var handler = new StubHandler();
        var client = CreateClient(handler, enabled: false);

        await Assert.ThrowsAsync<FeatureNotEnabledException>(() => client.GetApiInfoAsync());
        await Assert.ThrowsAsync<FeatureNotEnabledException>(() => client.GetAccessTokenAsync());
        await Assert.ThrowsAsync<FeatureNotEnabledException>(() => client.GetStudentsAsync(25));
        Assert.Equal(0, handler.TokenRequests);
    }

    private static EdFiApiClient CreateClient(StubHandler handler, bool enabled)
    {
        var options = Options.Create(new EdFiApiOptions
        {
            BaseUrl = "https://edfi.test",
            Key = "key",
            Secret = "secret"
        });
        return new EdFiApiClient(
            new StubHttpClientFactory(handler),
            options,
            new StubFeatureFlags(enabled),
            NullLogger<EdFiApiClient>.Instance);
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubFeatureFlags(bool enabled) : IFeatureFlagService
    {
        public bool IsEnabled(string featureName) => enabled;
        public IReadOnlyDictionary<string, FeatureFlagState> GetAll() =>
            new Dictionary<string, FeatureFlagState>();
        public void RequireEnabled(string featureName)
        {
            if (!enabled) throw new FeatureNotEnabledException(featureName);
        }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public int TokenRequests { get; private set; }
        public string? LastBearer { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath.Trim('/');

            if (path.Equals("oauth/token", StringComparison.OrdinalIgnoreCase))
            {
                TokenRequests++;
                return Task.FromResult(Json(
                    "{\"access_token\":\"test-token-123\",\"expires_in\":1800,\"token_type\":\"bearer\"}"));
            }

            if (path.Equals("data/v3/ed-fi/students", StringComparison.OrdinalIgnoreCase))
            {
                if (request.Headers.Authorization is { Scheme: "Bearer" } auth)
                {
                    LastBearer = auth.Parameter;
                }

                return Task.FromResult(Json(
                    "[{\"studentUniqueId\":\"100001\",\"firstName\":\"Lisa\",\"lastSurname\":\"Woods\",\"birthDate\":\"2005-03-01\"}," +
                    "{\"studentUniqueId\":\"100002\",\"firstName\":\"Omar\",\"lastSurname\":\"Vega\",\"birthDate\":\"2004-11-20\"}]"));
            }

            // discovery root
            return Task.FromResult(Json(
                "{\"version\":\"7.3\",\"build\":\"7.3.20432.0\"," +
                "\"dataModels\":[{\"name\":\"Ed-Fi\",\"version\":\"6.1.0\"}]}"));
        }

        private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }
}
