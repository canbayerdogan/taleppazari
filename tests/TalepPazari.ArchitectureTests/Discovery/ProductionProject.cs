namespace TalepPazari.ArchitectureTests.Discovery;

internal enum ProjectKind
{
    Module,
    Host,
    SharedKernel,

    // A project under src/ that matches no convention, or a referenced project outside src/.
    Unrecognized,
}

internal static class Layers
{
    public const string Contracts = "Contracts";
    public const string Domain = "Domain";
    public const string Application = "Application";
    public const string Infrastructure = "Infrastructure";
    public const string Api = "Api";

    public static readonly IReadOnlyList<string> All = [Contracts, Domain, Application, Infrastructure, Api];
}

// A project under src/. Its assembly name is the project name (no project overrides AssemblyName).
internal sealed record ProductionProject(string Name, string ProjectFile, ProjectKind Kind, string? Module, string? Layer)
{
    public string Directory => Path.GetDirectoryName(ProjectFile)!;

    public bool IsLayerOf(string module, string layer) =>
        Kind == ProjectKind.Module && Module == module && Layer == layer;

    public override string ToString() => Name;
}
