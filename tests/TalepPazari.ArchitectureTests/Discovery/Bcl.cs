namespace TalepPazari.ArchitectureTests.Discovery;

// The BCL is the Microsoft.NETCore.App shared framework the tests run on. ASP.NET Core and
// Microsoft.Extensions.* ship in other frameworks or packages, so they are not part of it.
internal static class Bcl
{
    public static readonly IReadOnlySet<string> AssemblyNames = Directory
        .GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll")
        .Select(Path.GetFileNameWithoutExtension)
        .OfType<string>()
        .ToHashSet(StringComparer.Ordinal);

    public static bool Contains(string assemblyName) => AssemblyNames.Contains(assemblyName);
}
