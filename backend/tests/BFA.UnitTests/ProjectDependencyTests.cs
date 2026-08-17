using System.Reflection;

namespace BFA.UnitTests;

public sealed class ProjectDependencyTests
{
    [Fact]
    public void Domain_does_not_reference_application_or_infrastructure()
    {
        var references = GetProjectReferences("BFA.Domain");

        Assert.DoesNotContain("BFA.Application", references);
        Assert.DoesNotContain("BFA.Infrastructure", references);
    }

    [Fact]
    public void Application_does_not_reference_infrastructure()
    {
        var references = GetProjectReferences("BFA.Application");

        Assert.DoesNotContain("BFA.Infrastructure", references);
    }

    [Fact]
    public void Domain_does_not_reference_persistence_frameworks()
    {
        var references = Assembly.Load("BFA.Domain")
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty);

        Assert.DoesNotContain(references, reference =>
            reference.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal)
            || reference.StartsWith("Npgsql", StringComparison.Ordinal));
    }

    private static IReadOnlyCollection<string> GetProjectReferences(string assemblyName)
    {
        return Assembly.Load(assemblyName)
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(reference => reference.StartsWith("BFA.", StringComparison.Ordinal))
            .ToArray();
    }
}
