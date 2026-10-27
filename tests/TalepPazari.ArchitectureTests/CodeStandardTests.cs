using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using TalepPazari.ArchitectureTests.Discovery;
using TalepPazari.ArchitectureTests.Support;

namespace TalepPazari.ArchitectureTests;

// Rule 9.
public sealed class CodeStandardTests
{
    private static readonly string[] AnnotationsAssemblies = ["System.ComponentModel.Annotations", "System.ComponentModel.DataAnnotations"];

    private static Solution Solution => Solution.Current;

    [Fact]
    public void Module_projects_use_no_Data_Annotations_attributes()
    {
        var projects = ArchAssert.NotEmpty(Solution.ModuleProjects, "module projects");

        ArchAssert.NoViolations(
            "No Data Annotations in module projects: use EF Fluent API, boundary validation, domain invariants and DB constraints.",
            projects.SelectMany(project =>
            {
                using var metadata = AssemblyMetadata.Open(Solution.AssemblyPath(project));
                return metadata.CustomAttributes()
                    .Where(a => IsDataAnnotation(a.AttributeType))
                    .Select(a => $"{project}: [{a.AttributeType}] on {a.AppliedTo}")
                    .ToList();
            }));
    }

    [Fact]
    public void Module_projects_do_not_reference_the_Data_Annotations_assembly()
    {
        var projects = ArchAssert.NotEmpty(Solution.ModuleProjects, "module projects");

        ArchAssert.NoViolations(
            "Module projects do not reference the Data Annotations assembly.",
            from project in projects
            from reference in Solution.Architecture.Assemblies.Single(a => a.Name == project.Name).ReferencedAssemblyNames
            let name = new AssemblyName(reference).Name
            where AnnotationsAssemblies.Contains(name)
            select $"{project} -> {name}");
    }

    // Checks the detector itself on a known sample, so it does not depend on what production code uses today.
    [Fact]
    public void The_Data_Annotations_check_flags_Required_but_not_CodeAnalysis_attributes_such_as_MemberNotNullWhen()
    {
        using var metadata = AssemblyMetadata.Open(typeof(CodeStandardTests).Assembly.Location);
        var sample = metadata.CustomAttributes().Where(a => a.AppliedTo.StartsWith(typeof(AttributeSample).FullName!, StringComparison.Ordinal)).ToList();

        Assert.Contains(sample, a => a.AttributeType == typeof(MemberNotNullWhenAttribute).FullName);
        Assert.Contains(sample, a => a.AttributeType == typeof(RequiredAttribute).FullName);
        Assert.False(IsDataAnnotation(typeof(MemberNotNullWhenAttribute).FullName!));
        Assert.True(IsDataAnnotation(typeof(RequiredAttribute).FullName!));
    }

    // Covers System.ComponentModel.DataAnnotations and System.ComponentModel.DataAnnotations.Schema.
    private static bool IsDataAnnotation(string attributeType) =>
        attributeType.StartsWith("System.ComponentModel.DataAnnotations.", StringComparison.Ordinal);

    private sealed class AttributeSample
    {
        [Required]
        public string? Value { get; init; }

        [MemberNotNullWhen(true, nameof(Value))]
        public bool HasValue => Value is not null;
    }
}
