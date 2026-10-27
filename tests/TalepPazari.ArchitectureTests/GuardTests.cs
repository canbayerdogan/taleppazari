using TalepPazari.ArchitectureTests.Discovery;
using TalepPazari.ArchitectureTests.Support;

namespace TalepPazari.ArchitectureTests;

// Rule 12. Each rule also guards its own selection with ArchAssert.NotEmpty, so no rule passes vacuously.
public sealed class GuardTests
{
    private static Solution Solution => Solution.Current;

    [Fact]
    public void Module_discovery_finds_at_least_one_module()
    {
        ArchAssert.NotEmpty(Solution.Modules, "modules under src/Modules");
    }

    [Fact]
    public void Every_project_under_src_follows_a_known_layout()
    {
        ArchAssert.NoViolations(
            "Every project under src/ is a module layer (src/Modules/<Name>/TalepPazari.Modules.<Name>.<Layer>), "
            + "a host (src/Hosts/<Name>) or SharedKernel; otherwise no architecture rule covers it.",
            Solution.UnrecognizedProjects.Select(p => $"{Path.GetRelativePath(Solution.Root, p.ProjectFile)}"));
    }

    [Fact]
    public void Every_production_assembly_is_built_and_loaded()
    {
        ArchAssert.NotEmpty(Solution.Architecture.Types, "loaded types");
        ArchAssert.NoViolations(
            "Every production project's assembly exists at bin/<Configuration>/<TargetFramework>/<ProjectName>.dll "
            + "(projects must not override AssemblyName or the output path).",
            Solution.MissingAssemblies);
    }

    [Fact]
    public void The_BCL_allowlist_is_read_from_the_shared_framework()
    {
        Assert.Contains("System.Private.CoreLib", Bcl.AssemblyNames);
        Assert.Contains("System.Runtime", Bcl.AssemblyNames);
        Assert.DoesNotContain("Microsoft.AspNetCore.Http", Bcl.AssemblyNames);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", Bcl.AssemblyNames);
    }
}
