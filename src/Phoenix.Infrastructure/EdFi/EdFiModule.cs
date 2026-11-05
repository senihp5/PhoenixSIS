using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phoenix.Core.Modules;

namespace Phoenix.Infrastructure.EdFi;

public class EdFiModule : IModule
{
    public string FeatureFlagName => "EdFi";

    public string DisplayName => "Ed-Fi Integration";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Ed-Fi services (API client + options) are registered in PHX-4.2.
        // Registration runs for every module at startup regardless of flag
        // state, so it must stay safe while the module is disabled: no
        // network calls and no required secrets are read here.
    }

    public Task InitializeAsync(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("Ed-Fi module initialized");
        return Task.CompletedTask;
    }
}
