using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Phoenix.Core.Modules;
using Phoenix.Core.Students;

namespace Phoenix.Infrastructure.Students;

/// <summary>
/// PHX-8.3 student pages module. The student, enrollment and audit services
/// are core and always registered in Program.cs; this module adds only the
/// flag-guarded read side the admin pages use.
/// </summary>
public class StudentsModule : IModule
{
    public string FeatureFlagName => "Students";

    public string DisplayName => "Students";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IStudentDirectory, StudentDirectory>();
    }

    public Task InitializeAsync(IServiceProvider services, ILogger logger)
    {
        logger.LogInformation("Students module initialized");
        return Task.CompletedTask;
    }
}
