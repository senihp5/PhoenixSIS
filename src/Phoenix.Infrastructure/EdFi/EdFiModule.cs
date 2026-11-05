using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phoenix.Core.Modules;
using Phoenix.Core.Modules.EdFi;

namespace Phoenix.Infrastructure.EdFi;

public class EdFiModule : IModule
{
    public string FeatureFlagName => "EdFi";

    public string DisplayName => "Ed-Fi Integration";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Registration runs for every module at startup regardless of flag
        // state, so it stays safe while disabled: options binding, a named
        // HttpClient, and a singleton client — no network calls, and no
        // secrets are read until a flag-guarded method is actually invoked.
        services.Configure<EdFiApiOptions>(
            configuration.GetSection(EdFiApiOptions.SectionName));
        services.AddHttpClient(EdFiApiClient.HttpClientName);
        services.AddSingleton<IEdFiApiClient, EdFiApiClient>();
    }

    public Task InitializeAsync(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("Ed-Fi module initialized");
        return Task.CompletedTask;
    }
}
