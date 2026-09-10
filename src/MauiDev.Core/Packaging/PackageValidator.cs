namespace MauiDev;

public sealed class PackageValidator
{
    public DoctorReport Validate(CheckContext context)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        var packable = graph.Projects.Where(project => project.IsPackable && !IsTest(project)).ToArray();
        var diagnostics = new List<DiagnosticFinding>();

        if (packable.Length == 0)
        {
            return Report(CheckStatus.Warn, "No packable src projects.", [
                new DiagnosticFinding
                {
                    Id = "MD400",
                    Message = "No packable csproj was found.",
                    Severity = CheckStatus.Warn,
                    NextStep = "Set IsPackable=true on the library or tool project."
                }
            ]);
        }

        var versions = packable
            .Select(project => project.PackageVersion ?? project.Version)
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (versions.Length > 1)
        {
            diagnostics.Add(new DiagnosticFinding
            {
                Id = "MD401",
                Message = "Packable project versions do not match: " + string.Join(", ", versions),
                Severity = CheckStatus.Fail,
                Why = "MauiEssentials CI requires every packable src Version to be identical.",
                NextStep = "Align Version / PackageVersion across packable projects."
            });
        }

        foreach (var project in packable)
        {
            if (string.IsNullOrWhiteSpace(project.PackageVersion ?? project.Version))
            {
                diagnostics.Add(Finding(project, "MD402", "Missing Version / PackageVersion.", CheckStatus.Fail));
            }

            if (string.IsNullOrWhiteSpace(project.PackageReadmeFile))
            {
                diagnostics.Add(Finding(project, "MD403", "Missing PackageReadmeFile.", CheckStatus.Warn));
            }

            if (string.IsNullOrWhiteSpace(project.PackageIcon))
            {
                diagnostics.Add(Finding(project, "MD404", "Missing PackageIcon.", CheckStatus.Warn));
            }

            if (string.IsNullOrWhiteSpace(project.PackageLicenseExpression) && string.IsNullOrWhiteSpace(project.PackageLicenseFile))
            {
                diagnostics.Add(Finding(project, "MD405", "Missing PackageLicenseExpression / PackageLicenseFile.", CheckStatus.Warn));
            }

            if (project.PackAsTool && string.IsNullOrWhiteSpace(project.ToolCommandName))
            {
                diagnostics.Add(Finding(project, "MD406", "PackAsTool project is missing ToolCommandName.", CheckStatus.Fail));
            }
        }

        var status = diagnostics.Any(item => item.Severity == CheckStatus.Fail)
            ? CheckStatus.Fail
            : diagnostics.Count > 0 ? CheckStatus.Warn : CheckStatus.Pass;

        return Report(status, status == CheckStatus.Pass ? $"{packable.Length} packable project(s), versions aligned" : diagnostics.Count + " issue(s)", diagnostics);
    }

    public async Task<(DoctorReport Report, int ExitCode)> PackAsync(CheckContext context, CancellationToken cancellationToken)
    {
        var report = Validate(context);
        if (report.WorstStatus == CheckStatus.Fail)
        {
            return (report, ExitCodes.Issues);
        }

        var result = await context.Process.RunAsync(
            "dotnet",
            ["pack", "--configuration", "Release", "--output", Path.Combine(context.RootPath, "artifacts", "nuget")],
            context.RootPath,
            TimeSpan.FromMinutes(5),
            cancellationToken).ConfigureAwait(false);

        var packResult = new CheckResult
        {
            CheckId = "package-pack",
            Title = "dotnet pack",
            Category = CheckCategory.Packaging,
            Status = result.ExitCode == 0 ? CheckStatus.Pass : CheckStatus.Fail,
            Detail = result.ExitCode == 0 ? "Packed locally (not pushed)" : result.StandardError.Trim(),
            Recommendation = result.ExitCode == 0
                ? "Publishing is pipeline-only. Do not run dotnet nuget push from this clone."
                : "dotnet pack failed."
        };

        var combined = new DoctorReport
        {
            Command = "package",
            Results = report.Results.Concat([packResult]).ToArray(),
            Recommendations = report.Recommendations.Concat(packResult.Recommendation is null ? [] : [packResult.Recommendation]).ToArray()
        };
        return (combined, combined.ExitCode(context.WarnAsError));
    }

    static DoctorReport Report(CheckStatus status, string detail, IReadOnlyList<DiagnosticFinding> diagnostics) =>
        new()
        {
            Command = "package",
            Results =
            [
                new CheckResult
                {
                    CheckId = "package",
                    Title = "Package validate",
                    Category = CheckCategory.Packaging,
                    Status = status,
                    Detail = detail,
                    Recommendation = diagnostics.FirstOrDefault(item => item.NextStep is not null)?.NextStep,
                    Diagnostics = diagnostics
                }
            ],
            Recommendations = diagnostics.Select(item => item.NextStep).Where(step => !string.IsNullOrWhiteSpace(step)).Distinct()!.ToArray()!
        };

    static DiagnosticFinding Finding(ProjectDocument project, string id, string message, CheckStatus severity) =>
        new()
        {
            Id = id,
            Message = message,
            File = project.Path,
            Severity = severity
        };

    static bool IsTest(ProjectDocument project) =>
        project.Path.Contains(".Tests", StringComparison.OrdinalIgnoreCase) ||
        project.Path.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
}
