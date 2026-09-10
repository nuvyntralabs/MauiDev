namespace MauiDev;

public sealed class ResourceEngine
{
    public DoctorReport Run(CheckContext context)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        var diagnostics = new List<DiagnosticFinding>();
        var items = graph.Projects.SelectMany(project => project.MauiItems).ToArray();

        var byLogical = items
            .GroupBy(item => Path.GetFileName(item.Include), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Select(item => Path.GetFileName(item.Include)).Distinct(StringComparer.Ordinal).Count() > 1);

        foreach (var group in byLogical)
        {
            diagnostics.Add(new DiagnosticFinding
            {
                Id = "MD200",
                Message = $"Case-duplicate resource name '{group.Key}'",
                Severity = CheckStatus.Fail,
                Why = "Case-insensitive filesystems hide collisions that break Linux CI.",
                NextStep = "Rename so logical names differ by more than case."
            });
        }

        foreach (var item in items)
        {
            var projectDir = Path.GetDirectoryName(item.ProjectPath) ?? context.RootPath;
            var full = Path.GetFullPath(Path.Combine(projectDir, item.Include.Replace('\\', Path.DirectorySeparatorChar)));
            if (!context.Files.FileExists(full))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD201",
                    Message = $"Missing file for {item.Kind} '{item.Include}'",
                    File = item.ProjectPath,
                    Severity = CheckStatus.Fail,
                    Why = "The csproj references a resource that is not on disk.",
                    NextStep = "Add the file or remove the item."
                });
            }
        }

        string blob;
        try
        {
            blob = string.Join('\n', graph.SourceFiles.Select(context.Files.ReadAllText));
        }
        catch (IOException)
        {
            blob = string.Empty;
        }

        foreach (var item in items.Where(item => item.Kind is "MauiImage" or "MauiFont"))
        {
            var name = Path.GetFileNameWithoutExtension(item.Include);
            if (name.Length > 0 && blob.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0)
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD202",
                    Message = $"Unused {item.Kind} '{item.Include}'",
                    File = item.ProjectPath,
                    Severity = CheckStatus.Warn,
                    Why = "No C# or XAML reference to this resource name was found.",
                    NextStep = "Remove the item if it is leftover."
                });
            }
        }

        var status = diagnostics.Any(item => item.Severity == CheckStatus.Fail)
            ? CheckStatus.Fail
            : diagnostics.Count > 0 ? CheckStatus.Warn : CheckStatus.Pass;

        var result = new CheckResult
        {
            CheckId = "resources",
            Title = "MAUI resources",
            Category = CheckCategory.Resources,
            Status = items.Length == 0 && diagnostics.Count == 0 ? CheckStatus.Skip : status,
            Detail = items.Length == 0 ? "No MAUI resource items" : diagnostics.Count == 0 ? "OK" : diagnostics.Count + " issue(s)",
            Recommendation = diagnostics.FirstOrDefault(item => item.NextStep is not null)?.NextStep,
            Diagnostics = diagnostics
        };

        return new DoctorReport
        {
            Command = "resources",
            Results = [result],
            Recommendations = result.Recommendation is null ? [] : [result.Recommendation]
        };
    }
}
