using ArchUnitNET.Domain;
using TalepPazari.ArchitectureTests.Discovery;
using TalepPazari.ArchitectureTests.Support;

namespace TalepPazari.ArchitectureTests;

// Rule 8.
public sealed class PersistenceTests
{
    private const string SchemaConstant = "Schema";

    private static Solution Solution => Solution.Current;

    // Exactly one per module. A read-model-only module (for example Search in ADR-002) would have none
    // or more than one; revisit this rule when such a module is added.
    [Fact]
    public void Each_module_has_exactly_one_internal_DbContext_in_its_Infrastructure_project()
    {
        var modules = ArchAssert.NotEmpty(Solution.Modules, "modules");
        var violations = new List<string>();
        foreach (var module in modules)
        {
            var contexts = Solution.ModuleProjects.Where(p => p.Module == module).SelectMany(Solution.TypesOf).Where(TypeFacts.IsDbContext).ToList();
            if (contexts.Count != 1)
            {
                violations.Add($"module {module} has {contexts.Count} DbContext types: [{string.Join(", ", contexts.Select(c => c.FullName))}]");
            }

            foreach (var context in contexts)
            {
                if (Solution.ProjectOf(context)?.Layer != Layers.Infrastructure)
                {
                    violations.Add($"{context.FullName} is in {Solution.ProjectOf(context)}, not in the module's Infrastructure project");
                }

                if (context.Visibility != Visibility.Internal)
                {
                    violations.Add($"{context.FullName} is {context.Visibility}, not internal");
                }
            }
        }

        ArchAssert.NoViolations("Each module has exactly one DbContext, defined in its Infrastructure project and internal.", violations);
    }

    [Fact]
    public void No_DbContext_exists_outside_module_Infrastructure_projects()
    {
        var types = ArchAssert.NotEmpty(Solution.Projects.SelectMany(Solution.TypesOf), "production types");

        ArchAssert.NoViolations(
            "DbContext types exist only in module Infrastructure projects (not in hosts, SharedKernel or other layers).",
            types.Where(TypeFacts.IsDbContext)
                .Where(t => Solution.ProjectOf(t) is not { Kind: ProjectKind.Module, Layer: Layers.Infrastructure })
                .Select(t => $"{t.FullName} in {Solution.ProjectOf(t)}"));
    }

    [Fact]
    public void No_module_uses_the_DbContext_of_another_module()
    {
        var contexts = ArchAssert.NotEmpty(Solution.ModuleProjects.SelectMany(Solution.TypesOf).Where(TypeFacts.IsDbContext), "DbContext types");

        ArchAssert.NoViolations(
            "A DbContext is used only by its own module.",
            from project in Solution.ModuleProjects
            from type in Solution.TypesOf(project)
            from target in TypeDependencies.Targets(type)
            where contexts.Contains(target) && Solution.ProjectOf(target)!.Module != project.Module
            select $"{type.FullName} ({project.Module}) -> {target.FullName}");
    }

    // Static IL check, so no DbContext is created and no database is needed.
    [Fact]
    public void Each_module_DbContext_uses_its_Schema_constant_as_default_schema()
    {
        var contexts = ArchAssert.NotEmpty(Solution.ModuleProjects.SelectMany(Solution.TypesOf).Where(TypeFacts.IsDbContext), "DbContext types");
        var violations = new List<string>();
        foreach (var context in contexts)
        {
            using var metadata = AssemblyMetadata.Open(Solution.AssemblyPath(Solution.ProjectOf(context)!));
            var schema = metadata.StringConstant(context.FullName, SchemaConstant);
            if (string.IsNullOrWhiteSpace(schema))
            {
                violations.Add($"{context.FullName} has no non-empty `const string {SchemaConstant}`");
                continue;
            }

            var defaultSchemas = metadata.StringLiteralsPassedTo(context.FullName, "OnModelCreating", "HasDefaultSchema");
            if (!defaultSchemas.SequenceEqual([schema]))
            {
                violations.Add($"{context.FullName}.OnModelCreating must call HasDefaultSchema({SchemaConstant}) once with "
                    + $"{SchemaConstant} = \"{schema}\"; found [{string.Join(", ", defaultSchemas.Select(s => $"\"{s}\""))}]");
            }
        }

        ArchAssert.NoViolations(
            "Each module DbContext sets its Schema constant as the default schema. Limitation: this static check recognizes only "
            + "the `modelBuilder.HasDefaultSchema(Schema)` style with a const string; other styles must be checked by hand. "
            + "The migrations history table schema is not checked.",
            violations);
    }

    [Fact]
    public void Module_schemas_are_unique()
    {
        var contexts = ArchAssert.NotEmpty(Solution.ModuleProjects.SelectMany(Solution.TypesOf).Where(TypeFacts.IsDbContext), "DbContext types");
        var schemas = contexts.Select(context =>
        {
            using var metadata = AssemblyMetadata.Open(Solution.AssemblyPath(Solution.ProjectOf(context)!));
            return (Context: context.FullName, Schema: metadata.StringConstant(context.FullName, SchemaConstant));
        });

        ArchAssert.NoViolations(
            "Each module owns its own schema: Schema constants are unique across modules.",
            schemas.Where(s => s.Schema is not null)
                .GroupBy(s => s.Schema, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => $"schema \"{g.Key}\" is used by {string.Join(", ", g.Select(s => s.Context))}"));
    }
}
