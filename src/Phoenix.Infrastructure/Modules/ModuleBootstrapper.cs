using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phoenix.Core.Features;
using Phoenix.Core.Modules;

namespace Phoenix.Infrastructure.Modules;

public static class ModuleBootstrapper
{
    /// <summary>
    /// The explicit module registry. New modules are added here — one line
    /// per module, in initialization order.
    /// </summary>
    private static readonly IModule[] Modules =
    [
        new CoreModule()
    ];

    /// <summary>
    /// Registers every module's services with the DI container. Called from
    /// Program.cs before Build(). Runs for all modules regardless of flag
    /// state — flag gating happens at initialization and at each module's
    /// own service entry points.
    /// </summary>
    public static void RegisterModules(IServiceCollection services, IConfiguration configuration)
    {
        foreach (var module in Modules)
        {
            module.RegisterServices(services, configuration);
            services.AddSingleton<IModule>(module);
        }
    }

    /// <summary>
    /// Logs each module's flag state and initializes only enabled modules.
    /// Called from Program.cs after Build().
    /// </summary>
    public static async Task InitializeModulesAsync(IServiceProvider services)
    {
        var features = services.GetRequiredService<IFeatureFlagService>();
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Phoenix.Modules");

        foreach (var module in Modules)
        {
            var alwaysOn = string.IsNullOrEmpty(module.FeatureFlagName);
            var enabled = alwaysOn || features.IsEnabled(module.FeatureFlagName);

            logger.LogInformation(
                "Module '{DisplayName}' registered — {State}",
                module.DisplayName,
                enabled ? "ENABLED" : "disabled");

            if (enabled)
            {
                await module.InitializeAsync(services, logger);
            }
        }
    }
}
