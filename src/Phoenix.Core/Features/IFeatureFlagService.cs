namespace Phoenix.Core.Features;

public interface IFeatureFlagService
{
    bool IsEnabled(string featureName);
    IReadOnlyDictionary<string, FeatureFlagState> GetAll();
    void RequireEnabled(string featureName);
}

public record FeatureFlagState(
    string Name,
    bool IsEnabled,
    string? Description
);

public class FeatureNotEnabledException(string featureName)
    : InvalidOperationException(
        $"Feature '{featureName}' is not enabled in this deployment.");
