using System.Text.Json;
using System.Text.RegularExpressions;

namespace MauiDev;

public sealed class IconsEngine
{
    public DoctorReport Run(CheckContext context)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        var maui = graph.MauiProjects;
        if (maui.Count == 0)
        {
            return CommandReport.Create("icons", "icons", "Icons", CheckCategory.Resources, [], "OK", "No MAUI projects found.");
        }

        var diagnostics = context.Filter(Analyze(context, graph, maui));
        return CommandReport.Create(
            "icons",
            "icons",
            "Icons",
            CheckCategory.Resources,
            diagnostics,
            "MauiIcon / splash present");
    }

    static IReadOnlyList<DiagnosticFinding> Analyze(CheckContext context, ProjectGraph graph, IReadOnlyList<ProjectDocument> maui)
    {
        var diagnostics = new List<DiagnosticFinding>();
        var icons = maui.SelectMany(project => project.MauiItems.Where(item => item.Kind is "MauiIcon" or "MauiSplashScreen")).ToArray();
        if (icons.Length == 0)
        {
            diagnostics.Add(new DiagnosticFinding
            {
                Id = "MD210",
                Message = "No MauiIcon / MauiSplashScreen items.",
                Severity = CheckStatus.Warn,
                Why = "Store listings and the splash screen need an app icon and splash asset.",
                NextStep = "Add <MauiIcon> and <MauiSplashScreen> items."
            });
        }

        foreach (var item in maui.SelectMany(project => project.MauiItems.Where(entry => entry.Kind == "MauiIcon")))
        {
            var projectDir = Path.GetDirectoryName(item.ProjectPath) ?? context.RootPath;
            var full = Path.GetFullPath(Path.Combine(projectDir, item.Include.Replace('\\', Path.DirectorySeparatorChar)));
            if (!context.Files.FileExists(full))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD211",
                    Message = $"Missing file for MauiIcon '{item.Include}'",
                    File = item.ProjectPath,
                    Severity = CheckStatus.Fail,
                    Why = "The csproj references an icon that is not on disk.",
                    NextStep = "Add the file or remove the MauiIcon item."
                });
            }
        }

        foreach (var xml in context.Files.GetFiles(context.RootPath, "*.xml", recursive: true)
                     .Where(path => path.Contains("mipmap-anydpi", StringComparison.OrdinalIgnoreCase)
                                    || path.Contains("adaptive", StringComparison.OrdinalIgnoreCase)))
        {
            string text;
            try
            {
                text = context.Files.ReadAllText(xml);
            }
            catch (IOException)
            {
                continue;
            }

            if (text.Contains("foreground", StringComparison.OrdinalIgnoreCase)
                && !text.Contains("background", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD212",
                    Message = "Android adaptive icon has a foreground without a background.",
                    File = xml,
                    Severity = CheckStatus.Warn,
                    Why = "Adaptive icons need both layers or the launcher shows a default plate.",
                    NextStep = "Add an ic_launcher_background layer."
                });
            }
        }

        foreach (var contents in context.Files.GetFiles(context.RootPath, "Contents.json", recursive: true)
                     .Where(path => path.Contains(".appiconset", StringComparison.OrdinalIgnoreCase)))
        {
            if (!HasMarketingSize(context.Files.ReadAllText(contents)))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD213",
                    Message = "iOS AppIcon set is missing the 1024 marketing size.",
                    File = contents,
                    Severity = CheckStatus.Warn,
                    Why = "App Store Connect requires a 1024×1024 marketing icon.",
                    NextStep = "Add a 1024×1024 image to the AppIcon.appiconset."
                });
            }
        }

        return diagnostics;
    }

    static bool HasMarketingSize(string json)
    {
        if (json.Contains("1024x1024", StringComparison.OrdinalIgnoreCase)
            || json.Contains("\"1024\"", StringComparison.Ordinal)
            || Regex.IsMatch(json, @"ios-marketing", RegexOptions.IgnoreCase))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var image in images.EnumerateArray())
            {
                var size = image.TryGetProperty("size", out var sizeValue) ? sizeValue.GetString() : null;
                var idiom = image.TryGetProperty("idiom", out var idiomValue) ? idiomValue.GetString() : null;
                if (string.Equals(size, "1024x1024", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(idiom, "ios-marketing", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }
}
