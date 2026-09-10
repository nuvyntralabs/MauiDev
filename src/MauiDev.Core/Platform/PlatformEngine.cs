using System.Text.RegularExpressions;

namespace MauiDev;

public sealed class PlatformEngine
{
    static readonly Regex PlatformType = new(@"\b(Android\.|UIKit\.|WinRT\.)", RegexOptions.Compiled);

    public DoctorReport Run(CheckContext context)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        var maui = graph.MauiProjects;
        if (maui.Count == 0)
        {
            return CommandReport.Create("platform", "platform", "Platform layout", CheckCategory.Project, [], "OK", "No MAUI projects found.");
        }

        var diagnostics = context.Filter(Analyze(context, graph, maui));
        return CommandReport.Create(
            "platform",
            "platform",
            "Platform layout",
            CheckCategory.Project,
            diagnostics,
            "TFMs match Platforms/ folders");
    }

    static IReadOnlyList<DiagnosticFinding> Analyze(CheckContext context, ProjectGraph graph, IReadOnlyList<ProjectDocument> maui)
    {
        var diagnostics = new List<DiagnosticFinding>();
        foreach (var project in maui)
        {
            var projectDir = Path.GetDirectoryName(project.Path) ?? context.RootPath;
            if (project.HasAndroid && !HasPlatformFolder(context.Files, projectDir, "Android"))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD050",
                    Message = $"{Path.GetFileName(project.Path)} targets Android without Platforms/Android.",
                    File = project.Path,
                    Severity = CheckStatus.Fail,
                    Why = "Android TFMs expect a Platforms/Android folder (manifest, MainActivity).",
                    NextStep = "Add Platforms/Android or drop the net*-android TFM."
                });
            }

            if (project.HasIos && !HasPlatformFolder(context.Files, projectDir, "iOS") && !HasPlist(graph, projectDir))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD051",
                    Message = $"{Path.GetFileName(project.Path)} targets iOS without Platforms/iOS or Info.plist.",
                    File = project.Path,
                    Severity = CheckStatus.Fail,
                    Why = "iOS TFMs expect Platforms/iOS (Info.plist, AppDelegate).",
                    NextStep = "Add Platforms/iOS/Info.plist or drop the net*-ios TFM."
                });
            }

            if (project.HasWindows || project.HasMacCatalyst)
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD053",
                    Message = $"{Path.GetFileName(project.Path)} includes Windows / Mac Catalyst TFMs.",
                    File = project.Path,
                    Severity = CheckStatus.Warn,
                    Why = "Native MauiEssentials plugins stay Android + iOS. The app TFM can still be valid.",
                    NextStep = "Do not treat Plugin.Maui.* native APIs as Windows / Catalyst solutions."
                });
            }

            if (string.IsNullOrWhiteSpace(project.SupportedOsPlatformVersion))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD054",
                    Message = $"{Path.GetFileName(project.Path)} is missing SupportedOSPlatformVersion.",
                    File = project.Path,
                    Severity = CheckStatus.Warn,
                    Why = "MAUI defaults are Android API 21 and iOS 15.",
                    NextStep = "Set <SupportedOSPlatformVersion> (21+ / 15.0+). maui-dev will not bump it."
                });
            }
            else if (IsBelowMauiDefault(project))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD054",
                    Message = $"{Path.GetFileName(project.Path)} SupportedOSPlatformVersion '{project.SupportedOsPlatformVersion}' is below MAUI defaults.",
                    File = project.Path,
                    Severity = CheckStatus.Warn,
                    Why = "MAUI requires Android API 21+ and iOS 15+.",
                    NextStep = "Raise SupportedOSPlatformVersion after testing. maui-dev will not bump it."
                });
            }
        }

        foreach (var path in graph.SourceFiles.Where(file => file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)))
        {
            if (ProjectGraph.IsPlatformSource(path))
            {
                continue;
            }

            string text;
            try
            {
                text = context.Files.ReadAllText(path);
            }
            catch (IOException)
            {
                continue;
            }

            if (PlatformType.IsMatch(text) && !text.Contains("#if", StringComparison.Ordinal))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD052",
                    Message = "Platform-specific API used without a platform guard.",
                    File = Path.GetRelativePath(context.RootPath, path),
                    Line = LineOf(text, PlatformType),
                    Severity = CheckStatus.Warn,
                    Why = "Android./UIKit./WinRT types in shared code fail on other TFMs.",
                    NextStep = "Wrap with #if ANDROID / #if IOS or move the file under Platforms/."
                });
            }
        }

        return diagnostics;
    }

    static bool HasPlatformFolder(IFileSystem files, string projectDir, string platform) =>
        files.DirectoryExists(Path.Combine(projectDir, "Platforms", platform));

    static bool HasPlist(ProjectGraph graph, string projectDir) =>
        graph.Plists.Any(path => path.StartsWith(projectDir, StringComparison.OrdinalIgnoreCase));

    static bool IsBelowMauiDefault(ProjectDocument project)
    {
        var digits = new string((project.SupportedOsPlatformVersion ?? string.Empty).TakeWhile(ch => char.IsDigit(ch) || ch == '.').ToArray());
        if (!Version.TryParse(digits.Contains('.', StringComparison.Ordinal) ? digits : digits + ".0", out var version))
        {
            return false;
        }

        if (project.HasAndroid && version.Major < 21)
        {
            return true;
        }

        return project.HasIos && !project.HasAndroid && version.Major < 15;
    }

    static int LineOf(string text, Regex regex)
    {
        var match = regex.Match(text);
        return match.Success ? text[..match.Index].Count(ch => ch == '\n') + 1 : 1;
    }
}
