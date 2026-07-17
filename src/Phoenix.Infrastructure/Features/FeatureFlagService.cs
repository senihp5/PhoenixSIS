using Phoenix.Core.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Phoenix.Infrastructure.Features;

public class FeatureFlagService : IFeatureFlagService
{
    private readonly IConfiguration _config;
    private readonly ILogger<FeatureFlagService> _logger;
    private readonly Dictionary<string, FeatureFlagState> _cache;

    public FeatureFlagService(
        IConfiguration config,
        ILogger<FeatureFlagService> logger)
    {
        _config = config;
        _logger = logger;
        _cache = LoadFlags();
    }

    private Dictionary<string, FeatureFlagState> LoadFlags()
    {
        var flags = new Dictionary<string, FeatureFlagState>(
            StringComparer.OrdinalIgnoreCase);

        var section = _config.GetSection("Features");
        foreach (var child in section.GetChildren())
        {
            var enabled = child.GetValue<bool>("Enabled", false);
            var description = child.GetValue<string?>("Description");
            flags[child.Key] = new FeatureFlagState(
                child.Key, enabled, description);

            _logger.LogInformation(
                "Feature '{Name}' is {State}",
                child.Key,
                enabled ? "ENABLED" : "disabled");
        }

        return flags;
    }

    public bool IsEnabled(string featureName) =>
        _cache.TryGetValue(featureName, out var flag) && flag.IsEnabled;

    public IReadOnlyDictionary<string, FeatureFlagState> GetAll() => _cache;

    public void RequireEnabled(string featureName)
    {
        if (!IsEnabled(featureName))
            throw new FeatureNotEnabledException(featureName);
    }
}
