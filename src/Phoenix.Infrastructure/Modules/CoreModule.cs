using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phoenix.Core.Modules;

namespace Phoenix.Infrastructure.Modules;

public class CoreModule : IModule
{
    public string FeatureFlagName => string.Empty;

    public string DisplayName => "Phoenix Core";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Core has no module-specific services yet. Shared services
        // (feature flags, auth) are registered directly in Program.cs.
        // Future always-on core services register here.
    }

    public Task InitializeAsync(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("Phoenix Core module initialized");
        return Task.CompletedTask;
    }
}
