namespace MauiDev;

public sealed class DependenciesEngine
{
    static readonly string[] MauiAlign = ["Microsoft.Maui.Controls", "Microsoft.Maui.Controls.Build.Tasks"];

    public DoctorReport Run(CheckContext context)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        if (graph.Projects.Count == 0)
        {
            return CommandReport.Create("dependencies", "dependencies", "Dependencies", CheckCategory.Project, [], "OK", "No projects found.");
        }

        var diagnostics = context.Filter(Analyze(context, graph));
        return CommandReport.Create(
            "dependencies",
            "dependencies",
            "Dependencies",
            CheckCategory.Project,
            diagnostics,
            "PackageReference hygiene looks sound");
    }

    static IReadOnlyList<DiagnosticFinding> Analyze(CheckContext context, ProjectGraph graph)
    {
        var diagnostics = new List<DiagnosticFinding>();
        var cpm = context.Files.GetFiles(context.RootPath, "Directory.Packages.props", recursive: true)
            .Any(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                         && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        foreach (var project in graph.Projects)
        {
            foreach (var group in project.PackageReferences.GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase))
            {
                var versions = group.Select(item => item.Version ?? "").Distinct(StringComparer.Ordinal).ToArray();
                if (group.Count() > 1 && versions.Length > 1)
                {
                    diagnostics.Add(new DiagnosticFinding
                    {
                        Id = "MD500",
                        Message = $"{Path.GetFileName(project.Path)} references {group.Key} at {string.Join(" and ", versions)}.",
                        File = project.Path,
                        Severity = CheckStatus.Fail,
                        Why = "The same PackageReference id with different versions is undefined.",
                        NextStep = "Keep one PackageReference version in this project."
                    });
                }
            }

            foreach (var reference in project.PackageReferences.Where(item => string.Equals(item.Id, "Plugin.Maui.MauiDev.Cli", StringComparison.OrdinalIgnoreCase)))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD503",
                    Message = $"{Path.GetFileName(project.Path)} PackageReferences Plugin.Maui.MauiDev.Cli.",
                    File = project.Path,
                    Severity = CheckStatus.Fail,
                    Why = "MauiDev is a dotnet tool, not an app library.",
                    NextStep = "Remove the PackageReference. Install with: dotnet tool install -g Plugin.Maui.MauiDev.Cli --source https://api.nuget.org/v3/index.json"
                });
            }

            if (cpm)
            {
                foreach (var reference in project.PackageReferences.Where(item => !string.IsNullOrWhiteSpace(item.Version)))
                {
                    diagnostics.Add(new DiagnosticFinding
                    {
                        Id = "MD502",
                        Message = $"{Path.GetFileName(project.Path)} sets Version on {reference.Id} while Directory.Packages.props exists.",
                        File = project.Path,
                        Severity = CheckStatus.Warn,
                        Why = "Central package management and a project-local Version fight each other.",
                        NextStep = "Move the version into Directory.Packages.props or drop CPM."
                    });
                }
            }
        }

        foreach (var id in MauiAlign)
        {
            var refs = graph.Projects.SelectMany(project => project.PackageReferences.Where(item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))).ToArray();
            var versions = refs.Select(item => item.Version).Where(version => !string.IsNullOrWhiteSpace(version)).Distinct(StringComparer.Ordinal).ToArray();
            if (versions.Length > 1)
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD501",
                    Message = id + " versions drift: " + string.Join(", ", versions),
                    Severity = CheckStatus.Warn,
                    Why = "Mixed Microsoft.Maui.Controls versions across projects break restore.",
                    NextStep = "Align Microsoft.Maui.Controls / Build.Tasks versions."
                });
            }
        }

        return diagnostics;
    }
}
