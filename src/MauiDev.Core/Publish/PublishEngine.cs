namespace MauiDev;

public sealed class PublishEngine
{
    public DoctorReport Run(CheckContext context)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        var maui = graph.MauiProjects;
        if (maui.Count == 0 && graph.PackableProjects.Count == 0)
        {
            return CommandReport.Create("publish", "publish", "Publish validate", CheckCategory.Packaging, [], "OK", "No MAUI or packable projects.");
        }

        var diagnostics = context.Filter(Analyze(context, graph, maui)).ToList();
        var results = new List<CheckResult>
        {
            CommandReport.Create(
                "publish",
                "publish",
                "Store identity",
                CheckCategory.Packaging,
                diagnostics,
                "Application ids look set").Results[0]
        };

        var packable = graph.Projects.Where(project => project.IsPackable && !IsTest(project)).ToArray();
        if (packable.Length > 0)
        {
            var package = new PackageValidator().Validate(context);
            if (package.WorstStatus is CheckStatus.Fail or CheckStatus.Warn)
            {
                results.Add(new CheckResult
                {
                    CheckId = "publish-package",
                    Title = "Pack metadata",
                    Category = CheckCategory.Packaging,
                    Status = package.WorstStatus,
                    Detail = "Pack validate failed (MD803).",
                    Recommendation = "Fix maui-dev package --validate before a store or NuGet publish.",
                    Diagnostics = context.Filter(package.Results.SelectMany(result => result.Diagnostics).Select(item => new DiagnosticFinding
                    {
                        Id = "MD803",
                        Message = item.Message,
                        File = item.File,
                        Line = item.Line,
                        Severity = item.Severity,
                        Why = item.Why,
                        NextStep = item.NextStep
                    })).ToArray()
                });
            }
            else
            {
                results.AddRange(package.Results);
            }
        }

        var recommendations = results
            .Select(result => result.Recommendation)
            .OfType<string>()
            .Where(text => text.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new DoctorReport
        {
            Command = "publish",
            Results = results,
            Recommendations = recommendations
        };
    }

    static IReadOnlyList<DiagnosticFinding> Analyze(CheckContext context, ProjectGraph graph, IReadOnlyList<ProjectDocument> maui)
    {
        var diagnostics = new List<DiagnosticFinding>();
        foreach (var project in maui.Where(item => item.HasAndroid))
        {
            var manifest = graph.Manifests.FirstOrDefault(path =>
                path.StartsWith(Path.GetDirectoryName(project.Path) ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            var package = project.ApplicationId
                          ?? (manifest is null ? null : ProjectGraph.ReadManifestPackage(context.Files, manifest));
            if (ProjectGraph.IsPlaceholderId(package))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD800",
                    Message = $"{Path.GetFileName(project.Path)} Android ApplicationId is missing or com.companyname.*.",
                    File = project.Path,
                    Severity = CheckStatus.Fail,
                    Why = "Play Console rejects the MAUI template id.",
                    NextStep = "Set <ApplicationId> to a real reverse-DNS id."
                });
            }
        }

        foreach (var project in maui.Where(item => item.HasIos))
        {
            var plist = graph.Plists.FirstOrDefault(path =>
                path.StartsWith(Path.GetDirectoryName(project.Path) ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            var bundle = project.ApplicationId
                         ?? (plist is null ? null : ProjectGraph.ReadPlistString(context.Files, plist, "CFBundleIdentifier"));
            if (ProjectGraph.IsPlaceholderId(bundle))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD801",
                    Message = $"{Path.GetFileName(project.Path)} iOS CFBundleIdentifier is missing or com.companyname.*.",
                    File = plist ?? project.Path,
                    Severity = CheckStatus.Fail,
                    Why = "App Store Connect rejects the MAUI template bundle id.",
                    NextStep = "Set CFBundleIdentifier / ApplicationId to a real reverse-DNS id."
                });
            }
        }

        if (maui.Any(project => project.HasIos))
        {
            var privacy = context.Files.GetFiles(context.RootPath, "PrivacyInfo.xcprivacy", recursive: true)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
                               && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (privacy.Length == 0)
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD802",
                    Message = "No PrivacyInfo.xcprivacy was found for iOS targets.",
                    Severity = CheckStatus.Warn,
                    Why = "Apple requires a privacy manifest for many APIs.",
                    NextStep = "Add Platforms/iOS/PrivacyInfo.xcprivacy (required-reason APIs as needed)."
                });
            }
        }

        return diagnostics;
    }

    static bool IsTest(ProjectDocument project) =>
        project.Path.Contains(".Tests", StringComparison.OrdinalIgnoreCase) ||
        project.Path.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
}
