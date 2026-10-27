using TalepPazari.ArchitectureTests.Discovery;
using TalepPazari.ArchitectureTests.Support;

namespace TalepPazari.ArchitectureTests;

// Rules 1-7. Each rule is checked on direct project references (.csproj) and on type dependencies
// (compiled assemblies, including method bodies). Transitive access alone is not a violation:
// for example Infrastructure reaches Domain through Application without referencing it.
public sealed class ModuleReferenceTests
{
    private static Solution Solution => Solution.Current;

    // Rule 1
    [Fact]
    public void A_module_references_another_module_only_through_its_Contracts_project()
    {
        var projects = ArchAssert.NotEmpty(Solution.ModuleProjects, "module projects");

        ArchAssert.NoViolations(
            "A module may reference another module only through that module's Contracts project.",
            from project in projects
            from reference in Solution.ProjectReferences(project)
            where reference.Kind == ProjectKind.Module && reference.Module != project.Module && reference.Layer != Layers.Contracts
            select $"{project} -> {reference} (project reference)");
    }

    // Rule 1
    [Fact]
    public void A_module_uses_types_of_another_module_only_from_its_Contracts_assembly()
    {
        var projects = ArchAssert.NotEmpty(Solution.ModuleProjects, "module projects");

        ArchAssert.NoViolations(
            "A module may use types of another module only from that module's Contracts assembly.",
            projects.SelectMany(project => TypeDependencies.NotAllowed(project, assembly =>
                Solution.ProjectOf(assembly) is not { Kind: ProjectKind.Module } target
                || target.Module == project.Module
                || target.Layer == Layers.Contracts)));
    }

    // Rule 2
    [Fact]
    public void Contracts_projects_reference_only_SharedKernel()
    {
        var projects = ArchAssert.NotEmpty(LayerProjects(Layers.Contracts), "Contracts projects");

        ArchAssert.NoViolations(
            "Contracts may reference only SharedKernel (no other layer, no other module, no other project).",
            ReferencesOutside(projects, (_, reference) => reference.Kind == ProjectKind.SharedKernel));
    }

    // Rule 2
    [Fact]
    public void Contracts_types_depend_only_on_the_BCL_SharedKernel_and_their_own_assembly()
    {
        var projects = ArchAssert.NotEmpty(LayerProjects(Layers.Contracts), "Contracts projects");
        ArchAssert.NotEmpty(projects.SelectMany(Solution.TypesOf), "Contracts types");

        ArchAssert.NoViolations(
            "Contracts may depend only on the BCL, SharedKernel and itself. No EF Core, ASP.NET Core or other library: "
            + "a new entry in this allowlist is an architecture decision, ask before adding one.",
            projects.SelectMany(project => TypeDependencies.NotAllowed(project, assembly =>
                Bcl.Contains(assembly) || assembly == Solution.SharedKernelName || assembly == project.Name)));
    }

    // Rule 3
    [Fact]
    public void Domain_projects_reference_only_SharedKernel()
    {
        var projects = ArchAssert.NotEmpty(LayerProjects(Layers.Domain), "Domain projects");

        ArchAssert.NoViolations(
            "Domain may reference only SharedKernel (not Application, Infrastructure, Api or any other project).",
            ReferencesOutside(projects, (_, reference) => reference.Kind == ProjectKind.SharedKernel));
    }

    // Rule 3
    [Fact]
    public void Domain_types_depend_only_on_the_BCL_SharedKernel_and_their_own_assembly()
    {
        var projects = ArchAssert.NotEmpty(LayerProjects(Layers.Domain), "Domain projects");
        ArchAssert.NotEmpty(projects.SelectMany(Solution.TypesOf), "Domain types");

        ArchAssert.NoViolations(
            "Domain may depend only on the BCL, SharedKernel and itself. No EF Core, ASP.NET Core, messaging or provider library: "
            + "a new entry in this allowlist is an architecture decision, ask before adding one.",
            projects.SelectMany(project => TypeDependencies.NotAllowed(project, assembly =>
                Bcl.Contains(assembly) || assembly == Solution.SharedKernelName || assembly == project.Name)));
    }

    // Rule 4
    [Fact]
    public void Application_projects_reference_only_their_own_Domain_and_Contracts_and_SharedKernel()
    {
        var projects = ArchAssert.NotEmpty(LayerProjects(Layers.Application), "Application projects");

        ArchAssert.NoViolations(
            "Application may reference only the Domain and Contracts projects of its own module and SharedKernel.",
            ReferencesOutside(projects, (project, reference) =>
                reference.Kind == ProjectKind.SharedKernel
                || reference.IsLayerOf(project.Module!, Layers.Domain)
                || reference.IsLayerOf(project.Module!, Layers.Contracts)));
    }

    // Rule 4
    [Fact]
    public void Application_types_depend_only_on_their_own_Domain_and_Contracts_the_BCL_SharedKernel_and_approved_libraries()
    {
        string[] approvedLibraries = ["FluentValidation", "Microsoft.Extensions.DependencyInjection.Abstractions"];
        var projects = ArchAssert.NotEmpty(LayerProjects(Layers.Application), "Application projects");
        ArchAssert.NotEmpty(projects.SelectMany(Solution.TypesOf), "Application types");

        ArchAssert.NoViolations(
            "Application may depend only on its own Domain and Contracts, the BCL, SharedKernel, itself and the approved libraries ("
            + string.Join(", ", approvedLibraries) + "). Not on Infrastructure, Api, EF Core or ASP.NET Core.",
            projects.SelectMany(project => TypeDependencies.NotAllowed(project, assembly =>
                Bcl.Contains(assembly)
                || approvedLibraries.Contains(assembly)
                || assembly == Solution.SharedKernelName
                || assembly == project.Name
                || assembly == Solution.Find(project.Module!, Layers.Domain)?.Name
                || assembly == Solution.Find(project.Module!, Layers.Contracts)?.Name)));
    }

    // Rule 5
    [Fact]
    public void Module_Api_projects_do_not_reference_their_Domain_or_Infrastructure()
    {
        var projects = ArchAssert.NotEmpty(LayerProjects(Layers.Api), "module Api projects");

        ArchAssert.NoViolations(
            "A module Api project may depend on its Application and its own Contracts project; its Domain and Infrastructure are forbidden.",
            from project in projects
            from reference in Solution.ProjectReferences(project)
            where reference.IsLayerOf(project.Module!, Layers.Domain) || reference.IsLayerOf(project.Module!, Layers.Infrastructure)
            select $"{project} -> {reference} (project reference)");
    }

    // Rule 5
    [Fact]
    public void Module_Api_types_do_not_use_their_Domain_or_Infrastructure()
    {
        var projects = ArchAssert.NotEmpty(LayerProjects(Layers.Api), "module Api projects");
        ArchAssert.NotEmpty(projects.SelectMany(Solution.TypesOf), "module Api types");

        ArchAssert.NoViolations(
            "A module Api project may depend on its Application and its own Contracts project; its Domain and Infrastructure are forbidden.",
            projects.SelectMany(project => TypeDependencies.NotAllowed(project, assembly =>
                assembly != Solution.Find(project.Module!, Layers.Domain)?.Name
                && assembly != Solution.Find(project.Module!, Layers.Infrastructure)?.Name)));
    }

    // Rule 6
    [Fact]
    public void Hosts_reference_modules_only_through_Infrastructure_and_Api_projects()
    {
        var hosts = ArchAssert.NotEmpty(Solution.Hosts, "host projects");

        ArchAssert.NoViolations(
            "Hosts reference modules only through their Infrastructure and Api projects, for composition.",
            from host in hosts
            from reference in Solution.ProjectReferences(host)
            where reference.Kind == ProjectKind.Module && reference.Layer is not (Layers.Infrastructure or Layers.Api)
            select $"{host} -> {reference} (project reference)");
    }

    // Rule 6
    [Fact]
    public void Hosts_use_module_types_only_from_Infrastructure_and_Api_assemblies()
    {
        var hosts = ArchAssert.NotEmpty(Solution.Hosts, "host projects");
        ArchAssert.NotEmpty(hosts.SelectMany(Solution.TypesOf), "host types");

        ArchAssert.NoViolations(
            "Hosts use module types only from Infrastructure and Api assemblies, for composition.",
            hosts.SelectMany(host => TypeDependencies.NotAllowed(host, assembly =>
                Solution.ProjectOf(assembly) is not { Kind: ProjectKind.Module } target
                || target.Layer is Layers.Infrastructure or Layers.Api)));
    }

    // Rule 7
    [Fact]
    public void SharedKernel_references_no_module()
    {
        var sharedKernel = ArchAssert.NotEmpty(Solution.Projects.Where(p => p.Kind == ProjectKind.SharedKernel), "SharedKernel project").Single();
        var assembly = Solution.Architecture.Assemblies.Single(a => a.Name == sharedKernel.Name);
        var moduleAssemblies = Solution.ModuleProjects.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);

        // Assembly references cover every type use, also while SharedKernel has no types.
        ArchAssert.NoViolations(
            "SharedKernel references no module (project references and assembly references).",
            Solution.ProjectReferences(sharedKernel)
                .Where(r => r.Kind == ProjectKind.Module)
                .Select(r => $"{sharedKernel} -> {r} (project reference)")
                .Concat(assembly.ReferencedAssemblyNames
                    .Select(n => new System.Reflection.AssemblyName(n).Name!)
                    .Where(moduleAssemblies.Contains)
                    .Select(n => $"{sharedKernel} -> {n} (assembly reference)")));
    }

    private static IEnumerable<ProductionProject> LayerProjects(string layer) =>
        Solution.ModuleProjects.Where(p => p.Layer == layer);

    private static IEnumerable<string> ReferencesOutside(
        IEnumerable<ProductionProject> projects, Func<ProductionProject, ProductionProject, bool> isAllowed) =>
        from project in projects
        from reference in Solution.ProjectReferences(project)
        where !isAllowed(project, reference)
        select $"{project} -> {reference} (project reference)";
}
