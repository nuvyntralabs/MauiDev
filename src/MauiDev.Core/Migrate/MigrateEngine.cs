namespace MauiDev;

public sealed class MigrateEngine
{
    public DoctorReport Run(CheckContext context)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        if (graph.Projects.Count == 0)
        {
            return CommandReport.Create("migrate", "migrate", "Migrate", CheckCategory.Project, [], "OK", "No projects found.");
        }

        var diagnostics = context.Filter(Analyze(context, graph));
        return CommandReport.Create(
            "migrate",
            "migrate",
            "Migrate",
            CheckCategory.Project,
            diagnostics,
            "No net8/net9 TFMs or Xamarin leftovers");
    }

    static IReadOnlyList<DiagnosticFinding> Analyze(CheckContext context, ProjectGraph graph)
    {
        var diagnostics = new List<DiagnosticFinding>();
        foreach (var project in graph.Projects)
        {
            foreach (var tfm in project.TargetFrameworks.Where(IsLegacyMauiTfm))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD600",
                    Message = $"{Path.GetFileName(project.Path)} still targets {tfm}.",
                    File = project.Path,
                    Severity = CheckStatus.Warn,
                    Why = "MauiEssentials packages target net10.0 by default.",
                    NextStep = "Retarget to net10.0-android / net10.0-ios after testing. maui-dev will not edit TFMs."
                });
            }

            foreach (var reference in project.PackageReferences.Where(item =>
                         item.Id.StartsWith("Xamarin.Forms", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(item.Id, "Xamarin.Essentials", StringComparison.OrdinalIgnoreCase)))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD601",
                    Message = $"{Path.GetFileName(project.Path)} still references {reference.Id}.",
                    File = project.Path,
                    Severity = CheckStatus.Fail,
                    Why = "Xamarin.Forms / Xamarin.Essentials do not belong in a MAUI project.",
                    NextStep = "Remove the PackageReference and migrate APIs to .NET MAUI."
                });
            }
        }

        foreach (var path in graph.SourceFiles.Where(file => file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            string text;
            try
            {
                text = context.Files.ReadAllText(path);
            }
            catch (IOException)
            {
                continue;
            }

            if (text.Contains("Forms.Init", StringComparison.Ordinal)
                || text.Contains("LoadApplication(", StringComparison.Ordinal))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD602",
                    Message = $"{Path.GetFileName(path)} still calls Forms.Init / LoadApplication.",
                    File = path,
                    Severity = CheckStatus.Fail,
                    Why = "Those APIs are Xamarin.Forms startup, not MAUI.",
                    NextStep = "Replace with MauiApp / MauiProgram. maui-dev will not rewrite the file."
                });
            }
        }

        return diagnostics;
    }

    static bool IsLegacyMauiTfm(string tfm)
    {
        var value = tfm.Trim();
        return value.StartsWith("net8.0", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("net9.0", StringComparison.OrdinalIgnoreCase);
    }
}
