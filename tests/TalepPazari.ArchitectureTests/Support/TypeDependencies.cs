using ArchUnitNET.Domain;
using TalepPazari.ArchitectureTests.Discovery;

namespace TalepPazari.ArchitectureTests.Support;

internal static class TypeDependencies
{
    // Every type a type uses (signatures, base types, attributes and method bodies), including generic arguments.
    // Generic parameters (the T in Foo<T>) belong to no assembly and are skipped.
    public static IEnumerable<IType> Targets(IType type) =>
        type.Dependencies
            .SelectMany(d => Expand(d.Target, d.TargetGenericArguments))
            .Where(t => !t.IsGenericParameter);

    // Type dependencies of a project whose target assembly is not allowed, as "Origin -> Target (assembly)".
    // ArchUnitNET names the origin type but skips compiler-generated methods such as top-level statements,
    // so the assembly's TypeRef table is checked as well; those findings name the project instead.
    public static IEnumerable<string> NotAllowed(ProductionProject project, Func<string, bool> isAllowedAssembly)
    {
        var solution = Solution.Current;
        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in solution.TypesOf(project))
        {
            foreach (var target in Targets(type))
            {
                var assembly = Solution.AssemblyNameOf(target);
                if (!isAllowedAssembly(assembly))
                {
                    reported.Add(target.FullName);
                    yield return $"{type.FullName} -> {target.FullName} ({assembly})";
                }
            }
        }

        using var metadata = AssemblyMetadata.Open(Solution.AssemblyPath(project));
        foreach (var (assembly, type) in metadata.ReferencedTypes().ToList())
        {
            if (!isAllowedAssembly(assembly) && !reported.Contains(type))
            {
                yield return $"{project} (found in assembly metadata, for example in top-level statements or a lambda) -> {type} ({assembly})";
            }
        }
    }

    private static IEnumerable<IType> Expand(IType target, IEnumerable<GenericArgument> arguments)
    {
        yield return target;
        foreach (var argument in arguments)
        {
            foreach (var nested in Expand(argument.Type, argument.GenericArguments))
            {
                yield return nested;
            }
        }
    }
}
