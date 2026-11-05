using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phoenix.Core.DataMigration;
using Phoenix.Core.Modules;

namespace Phoenix.Infrastructure.DataMigration;

public class MigrationModule : IModule
{
    public string FeatureFlagName => "Migration";

    public string DisplayName => "Data Migration";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // Safe while disabled: registrations only, no work until the
        // flag-guarded wizard page invokes them.
        services.AddScoped<IMigrationService, MigrationService>();
        services.AddScoped<ISisSourceConnector, SampleSourceConnector>();
    }

    public Task InitializeAsync(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("Data Migration module initialized");
        return Task.CompletedTask;
    }
}
