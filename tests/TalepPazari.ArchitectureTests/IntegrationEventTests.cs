using System.Text.RegularExpressions;
using ArchUnitNET.Domain;
using TalepPazari.ArchitectureTests.Discovery;
using TalepPazari.ArchitectureTests.Support;

namespace TalepPazari.ArchitectureTests;

// Rule 11. An integration event is any type in an IntegrationEvents namespace.
public sealed partial class IntegrationEventTests
{
    private const string IntegrationEvents = "IntegrationEvents";

    private static Solution Solution => Solution.Current;

    [Fact]
    public void Integration_events_are_public_sealed_records_named_Name_V_N_in_Contracts_IntegrationEvents()
    {
        var events = ArchAssert.NotEmpty(
            from project in Solution.ModuleProjects.Where(p => p.Layer == Layers.Contracts)
            from type in Solution.TypesOf(project)
            where !type.IsCompilerGenerated && InIntegrationEventsNamespace(type)
            select (Project: project, Type: type),
            "integration events");

        ArchAssert.NoViolations(
            "Integration events are public sealed records named <Name>V<N> in namespace <Contracts project>.IntegrationEvents.",
            events.SelectMany(e => Problems(e.Project, e.Type).Select(problem => $"{e.Type.FullName}: {problem}")));
    }

    [Fact]
    public void No_IntegrationEvents_namespace_exists_outside_Contracts()
    {
        var types = ArchAssert.NotEmpty(Solution.Projects.Where(p => p.Layer != Layers.Contracts).SelectMany(Solution.TypesOf), "types outside Contracts");

        ArchAssert.NoViolations(
            "IntegrationEvents namespaces exist only in module Contracts projects.",
            types.Where(InIntegrationEventsNamespace).Select(t => $"{t.FullName} in {Solution.ProjectOf(t)}"));
    }

    private static IEnumerable<string> Problems(ProductionProject project, IType type)
    {
        if (type.Namespace.FullName != $"{project.Name}.{IntegrationEvents}")
        {
            yield return $"namespace must be {project.Name}.{IntegrationEvents}";
        }

        if (!TypeFacts.IsPublicApi(type))
        {
            yield return "must be public";
        }

        if (type is not Class { IsRecord: true })
        {
            yield return "must be a record class";
        }

        if (type is not Class { IsSealed: true })
        {
            yield return "must be sealed";
        }

        if (!VersionedName().IsMatch(type.Name))
        {
            yield return "name must be <Name>V<N>, for example ExampleItemCreatedV1";
        }
    }

    private static bool InIntegrationEventsNamespace(IType type) =>
        type.Namespace.FullName.Split('.').Contains(IntegrationEvents);

    [GeneratedRegex("^[A-Z][A-Za-z0-9]*V[1-9][0-9]*$")]
    private static partial Regex VersionedName();
}
