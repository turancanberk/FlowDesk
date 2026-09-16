using System.Xml.Linq;

namespace FlowDesk.UnitTests.Architecture;

/// <summary>
/// Locates the backend solution on disk and reads the dependencies each project
/// declares, so architecture tests can assert on the declared dependency graph.
/// </summary>
/// <remarks>
/// Reading the project files rather than only reflecting over compiled
/// assemblies is deliberate. The compiler drops references a project never
/// actually uses, so a Domain project that wrongly references Infrastructure but
/// happens not to call into it yet would pass a reflection-only check. The
/// declaration is the thing we want to keep honest.
/// </remarks>
internal static class SolutionLayout
{
    private const string SolutionFileName = "FlowDesk.slnx";

    /// <summary>Directory containing <c>FlowDesk.slnx</c>.</summary>
    public static DirectoryInfo BackendRoot { get; } = FindBackendRoot();

    public static ProjectDependencies ReadProject(string projectName)
    {
        var projectFile = BackendRoot
            .EnumerateFiles($"{projectName}.csproj", SearchOption.AllDirectories)
            .SingleOrDefault()
            ?? throw new InvalidOperationException(
                $"Project file '{projectName}.csproj' was not found under '{BackendRoot.FullName}'.");

        var document = XDocument.Load(projectFile.FullName);

        var projectReferences = document
            .Descendants("ProjectReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => Path.GetFileNameWithoutExtension(include!.Replace('\\', Path.DirectorySeparatorChar)))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var packageReferences = document
            .Descendants("PackageReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var frameworkReferences = document
            .Descendants("FrameworkReference")
            .Select(element => (string?)element.Attribute("Include"))
            .Where(include => !string.IsNullOrWhiteSpace(include))
            .Select(include => include!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        return new ProjectDependencies(projectName, projectReferences, packageReferences, frameworkReferences);
    }

    private static DirectoryInfo FindBackendRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (directory.EnumerateFiles(SolutionFileName).Any())
            {
                return directory;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not locate '{SolutionFileName}' walking up from '{AppContext.BaseDirectory}'.");
    }
}

internal sealed record ProjectDependencies(
    string ProjectName,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<string> PackageReferences,
    IReadOnlyList<string> FrameworkReferences);
