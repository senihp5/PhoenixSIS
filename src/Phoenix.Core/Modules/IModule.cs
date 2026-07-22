using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Phoenix.Core.Modules;

public interface IModule
{
    /// <summary>
    /// Feature flag name that controls this module. Empty string means the
    /// module is always enabled (Core).
    /// </summary>
    string FeatureFlagName { get; }

    /// <summary>
    /// Human-readable module name for logging and diagnostics.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Register the module's services with the DI container. Called at
    /// startup for every module regardless of flag state, so registrations
    /// must be safe when the module is disabled.
    /// </summary>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>
    /// Initialize the module. Called after the app is built, and only when
    /// the module's feature flag is enabled.
    /// </summary>
    Task InitializeAsync(IServiceProvider services, ILogger logger);
}
