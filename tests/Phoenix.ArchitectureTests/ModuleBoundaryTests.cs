using NetArchTest.Rules;
using Phoenix.Core.Features;
using Phoenix.Infrastructure.Features;

namespace Phoenix.ArchitectureTests;

public class ModuleBoundaryTests
{
    [Fact]
    public void Core_DoesNotDependOnAnyOtherPhoenixProject()
    {
        var result = Types
            .InAssembly(typeof(IFeatureFlagService).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Phoenix.Infrastructure", "Phoenix.Web", "Phoenix.Modules")
            .GetResult();

        Assert.True(result.IsSuccessful, Failures("Phoenix.Core", result));
    }

    [Fact]
    public void Infrastructure_DependsOnlyOnCore()
    {
        var result = Types
            .InAssembly(typeof(FeatureFlagService).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Phoenix.Web", "Phoenix.Modules")
            .GetResult();

        Assert.True(result.IsSuccessful, Failures("Phoenix.Infrastructure", result));
    }

    [Fact]
    public void Modules_DoNotReferenceOtherModulesInternals()
    {
        // Dormant until Phoenix.Modules.* assemblies exist (Phase 2 modules
        // per MODULE_TEMPLATE.md). Activates automatically as they appear.
        var moduleAssemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => (a.GetName().Name ?? string.Empty).StartsWith("Phoenix.Modules."))
            .ToList();

        foreach (var assembly in moduleAssemblies)
        {
            var assemblyName = assembly.GetName().Name!;
            var moduleRoot = string.Join('.', assemblyName.Split('.').Take(3));

            var otherModuleRoots = moduleAssemblies
                .Select(a => string.Join('.', (a.GetName().Name ?? string.Empty).Split('.').Take(3)))
                .Where(root => root != moduleRoot)
                .Distinct()
                .ToArray();

            if (otherModuleRoots.Length == 0)
            {
                continue;
            }

            var result = Types
                .InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(otherModuleRoots)
                .GetResult();

            Assert.True(result.IsSuccessful, Failures(assemblyName, result));
        }
    }

    private static string Failures(string assemblyName, TestResult result)
    {
        var failing = result.FailingTypeNames is null
            ? string.Empty
            : string.Join(", ", result.FailingTypeNames);

        return $"{assemblyName} has forbidden dependencies: {failing}";
    }
}
