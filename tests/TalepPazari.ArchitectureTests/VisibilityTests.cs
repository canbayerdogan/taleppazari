using TalepPazari.ArchitectureTests.Discovery;
using TalepPazari.ArchitectureTests.Support;

namespace TalepPazari.ArchitectureTests;

// Rule 10. Public types in Application and Domain are allowed.
public sealed class VisibilityTests
{
    private static Solution Solution => Solution.Current;

    // EF Core migrations are excluded by base type: the tool generates them public.
    [Fact]
    public void Infrastructure_exposes_only_its_Module_class_as_public()
    {
        var projects = ArchAssert.NotEmpty(Solution.ModuleProjects.Where(p => p.Layer == Layers.Infrastructure), "Infrastructure projects");

        ArchAssert.NoViolations(
            "Infrastructure exposes exactly one public type, <Name>Module (EF Core migrations excluded).",
            projects.SelectMany(project => PublicApiOtherThan(project, $"{project.Name}.{project.Module}Module")));
    }

    [Fact]
    public void Module_Api_exposes_only_its_Endpoints_class_as_public()
    {
        var projects = ArchAssert.NotEmpty(Solution.ModuleProjects.Where(p => p.Layer == Layers.Api), "module Api projects");

        ArchAssert.NoViolations(
            "A module Api project exposes exactly one public type, <Name>Endpoints.",
            projects.SelectMany(project => PublicApiOtherThan(project, $"{project.Name}.{project.Module}Endpoints")));
    }

    [Fact]
    public void FluentValidation_validators_are_internal()
    {
        var validators = ArchAssert.NotEmpty(Solution.ModuleProjects.SelectMany(Solution.TypesOf).Where(TypeFacts.IsValidator), "validators (AbstractValidator<T>)");

        ArchAssert.NoViolations(
            "FluentValidation validators (AbstractValidator<T>) are internal.",
            validators.Where(TypeFacts.IsPublicApi).Select(v => $"{v.FullName} in {Solution.ProjectOf(v)}"));
    }

    // A repository implementation: an Infrastructure class that implements an interface of its own module's Application.
    [Fact]
    public void Repository_implementations_are_internal()
    {
        var repositories = ArchAssert.NotEmpty(
            from project in Solution.ModuleProjects.Where(p => p.Layer == Layers.Infrastructure)
            from type in Solution.TypesOf(project)
            where type.ImplementedInterfaces.Any(i => Solution.ProjectOf(i)?.IsLayerOf(project.Module!, Layers.Application) == true)
            select type,
            "repository implementations");

        ArchAssert.NoViolations(
            "Repository implementations (Infrastructure classes implementing an Application interface) are internal.",
            repositories.Where(TypeFacts.IsPublicApi).Select(r => $"{r.FullName} in {Solution.ProjectOf(r)}"));
    }

    private static IEnumerable<string> PublicApiOtherThan(ProductionProject project, string expectedFullName)
    {
        var publicTypes = Solution.TypesOf(project)
            .Where(t => !t.IsCompilerGenerated && !TypeFacts.IsEfToolGenerated(t) && TypeFacts.IsPublicApi(t))
            .Select(t => t.FullName)
            .ToList();

        if (!publicTypes.Contains(expectedFullName))
        {
            yield return $"{project}: public type {expectedFullName} is missing";
        }

        foreach (var type in publicTypes.Where(t => t != expectedFullName))
        {
            yield return $"{project}: {type} is public";
        }
    }
}
