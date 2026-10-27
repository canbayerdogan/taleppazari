using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;

namespace TalepPazari.ArchitectureTests.Discovery;

// Finds the production projects under src/ by convention and loads their compiled assemblies.
// Nothing here names a module, so a new module that follows the how-to is covered without changes.
internal sealed partial class Solution
{
    public const string SharedKernelName = "TalepPazari.SharedKernel";

    private static readonly Lazy<Solution> Instance = new(Load);

    private readonly Dictionary<string, ProductionProject> projectsByPath;
    private readonly Dictionary<string, ProductionProject> projectsByName;

    private Solution(string root, IReadOnlyList<ProductionProject> projects, IReadOnlyList<string> missingAssemblies, Architecture architecture)
    {
        Root = root;
        Projects = projects;
        MissingAssemblies = missingAssemblies;
        Architecture = architecture;
        projectsByPath = projects.ToDictionary(p => Path.GetFullPath(p.ProjectFile), StringComparer.OrdinalIgnoreCase);
        projectsByName = projects.ToDictionary(p => p.Name, StringComparer.Ordinal);
    }

    public static Solution Current => Instance.Value;

    public string Root { get; }

    public IReadOnlyList<ProductionProject> Projects { get; }

    // Expected assembly paths that do not exist (for example a project that overrides AssemblyName).
    public IReadOnlyList<string> MissingAssemblies { get; }

    public Architecture Architecture { get; }

    public IEnumerable<ProductionProject> ModuleProjects => Projects.Where(p => p.Kind == ProjectKind.Module);

    public IEnumerable<ProductionProject> Hosts => Projects.Where(p => p.Kind == ProjectKind.Host);

    public IEnumerable<ProductionProject> UnrecognizedProjects => Projects.Where(p => p.Kind == ProjectKind.Unrecognized);

    public IReadOnlyList<string> Modules => ModuleProjects.Select(p => p.Module!).Distinct().Order(StringComparer.Ordinal).ToList();

    public ProductionProject? Find(string module, string layer) =>
        ModuleProjects.SingleOrDefault(p => p.IsLayerOf(module, layer));

    public IEnumerable<ProductionProject> ProjectReferences(ProductionProject project) =>
        XDocument.Load(project.ProjectFile)
            .Descendants("ProjectReference")
            .Select(e => (string?)e.Attribute("Include"))
            .OfType<string>()
            .Select(include => Path.GetFullPath(Path.Combine(project.Directory, include.Replace('\\', Path.DirectorySeparatorChar))))
            .Select(path => projectsByPath.GetValueOrDefault(path)
                ?? new ProductionProject(Path.GetFileNameWithoutExtension(path), path, ProjectKind.Unrecognized, null, null));

    public IEnumerable<IType> TypesOf(ProductionProject project) =>
        Architecture.Types.Where(t => AssemblyNameOf(t) == project.Name);

    // The production project an assembly or type belongs to, or null for framework and package assemblies.
    public ProductionProject? ProjectOf(string assemblyName) => projectsByName.GetValueOrDefault(assemblyName);

    public ProductionProject? ProjectOf(IType type) => ProjectOf(AssemblyNameOf(type));

    public static string AssemblyPath(ProductionProject project) =>
        Path.Combine(project.Directory, "bin", BuildSetting("Configuration"), BuildSetting("TargetFramework"), project.Name + ".dll");

    public static string AssemblyNameOf(IType type) => new AssemblyName(type.Assembly.FullName).Name!;

    private static Solution Load()
    {
        var root = FindRoot();
        var src = Path.Combine(root, "src");
        var projects = Directory
            .GetFiles(src, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(src, path))
            .Select(path => Classify(src, path))
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToList();

        var loader = new ArchLoader();
        var missing = new List<string>();
        foreach (var project in projects.Where(p => p.Kind != ProjectKind.Unrecognized))
        {
            var path = AssemblyPath(project);
            if (File.Exists(path))
            {
                loader.LoadFilteredDirectory(Path.GetDirectoryName(path)!, Path.GetFileName(path), SearchOption.TopDirectoryOnly);
            }
            else
            {
                missing.Add(Path.GetRelativePath(root, path));
            }
        }

        return new Solution(root, projects, missing, loader.Build());
    }

    private static ProductionProject Classify(string src, string projectFile)
    {
        var name = Path.GetFileNameWithoutExtension(projectFile);
        var segments = Path.GetRelativePath(src, projectFile).Split(Path.DirectorySeparatorChar);

        // src/Modules/<Module>/TalepPazari.Modules.<Module>.<Layer>/TalepPazari.Modules.<Module>.<Layer>.csproj
        var match = ModuleProjectName().Match(name);
        if (segments is ["Modules", var folder, var projectFolder, _]
            && match.Success
            && match.Groups["module"].Value == folder
            && projectFolder == name)
        {
            return new ProductionProject(name, projectFile, ProjectKind.Module, folder, match.Groups["layer"].Value);
        }

        if (segments is ["Hosts", var hostFolder, _] && hostFolder == name)
        {
            return new ProductionProject(name, projectFile, ProjectKind.Host, null, null);
        }

        if (segments is ["Shared", SharedKernelName, _] && name == SharedKernelName)
        {
            return new ProductionProject(name, projectFile, ProjectKind.SharedKernel, null, null);
        }

        return new ProductionProject(name, projectFile, ProjectKind.Unrecognized, null, null);
    }

    private static bool IsBuildOutput(string src, string path) =>
        Path.GetRelativePath(src, path).Split(Path.DirectorySeparatorChar).Any(s => s is "bin" or "obj");

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TalepPazari.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Repository root (the folder with TalepPazari.slnx) not found above " + AppContext.BaseDirectory);
    }

    // Set by the test project file, so production assemblies are read from the same configuration and framework.
    private static string BuildSetting(string key) =>
        typeof(Solution).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(a => a.Key == key).Value!;

    [GeneratedRegex(@"^TalepPazari\.Modules\.(?<module>[A-Z][A-Za-z0-9]*)\.(?<layer>Contracts|Domain|Application|Infrastructure|Api)$")]
    private static partial Regex ModuleProjectName();
}
