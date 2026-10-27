using ArchUnitNET.Domain;
using TalepPazari.ArchitectureTests.Discovery;

namespace TalepPazari.ArchitectureTests.Support;

internal static class TypeFacts
{
    public const string DbContext = "Microsoft.EntityFrameworkCore.DbContext";

    public static bool InheritsFrom(IType type, string baseFullName) =>
        type is Class c && c.InheritedClasses.Any(b => b.FullName == baseFullName || b.FullName.StartsWith(baseFullName + "<", StringComparison.Ordinal));

    public static bool IsDbContext(IType type) => InheritsFrom(type, DbContext);

    public static bool IsValidator(IType type) => InheritsFrom(type, "FluentValidation.AbstractValidator`1");

    // EF Core migrations and the model snapshot are tool-generated (the migration class is public by design).
    // They are recognized by base type, not by file path.
    public static bool IsEfToolGenerated(IType type) =>
        InheritsFrom(type, "Microsoft.EntityFrameworkCore.Migrations.Migration")
        || InheritsFrom(type, "Microsoft.EntityFrameworkCore.Infrastructure.ModelSnapshot");

    // Visible to other assemblies: public, and for a nested type every enclosing type is public too.
    public static bool IsPublicApi(IType type)
    {
        if (type.Visibility != Visibility.Public)
        {
            return false;
        }

        var separator = type.FullName.LastIndexOf('+');
        if (!type.IsNested || separator < 0)
        {
            return true;
        }

        var declaringName = type.FullName[..separator];
        var declaring = Solution.Current.Architecture.Types.SingleOrDefault(t => t.FullName == declaringName);
        return declaring is null || IsPublicApi(declaring);
    }
}
