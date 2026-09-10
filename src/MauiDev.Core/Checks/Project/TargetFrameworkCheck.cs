namespace MauiDev.Checks;

public sealed class TargetFrameworkCheck : CheckBase
{
    public override string Id => "target-framework";
    public override CheckCategory Category => CheckCategory.Project;
    public override string Title => "Target Framework";

    public override Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        var maui = graph.MauiProjects;
        if (maui.Count == 0)
        {
            return Task.FromResult(Skip("No MAUI projects found."));
        }

        var diagnostics = new List<DiagnosticFinding>();
        foreach (var project in maui)
        {
            if (project.TargetFrameworks.Count == 0)
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD010",
                    Message = $"{Path.GetFileName(project.Path)} has no TargetFramework(s).",
                    File = project.Path,
                    Severity = CheckStatus.Fail,
                    Why = "A MAUI app must declare net*-android / net*-ios (or Catalyst / Windows).",
                    NextStep = "Add <TargetFrameworks>net10.0-android;net10.0-ios</TargetFrameworks>."
                });
                continue;
            }

            foreach (var tfm in project.TargetFrameworks)
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD011",
                    Message = tfm,
                    File = project.Path,
                    Severity = CheckStatus.Pass
                });
            }

            if (project.TargetFrameworks.All(tfm => !tfm.Contains('-', StringComparison.Ordinal)))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD012",
                    Message = $"{Path.GetFileName(project.Path)} only targets {string.Join(';', project.TargetFrameworks)} (shared TFM).",
                    File = project.Path,
                    Severity = CheckStatus.Warn,
                    Why = "A shared net10.0 reference assembly cannot run native MAUI APIs.",
                    NextStep = "Add net10.0-android and/or net10.0-ios if this is an app."
                });
            }
        }

        if (diagnostics.Any(item => item.Severity == CheckStatus.Fail))
        {
            return Task.FromResult(Fail("One or more projects are missing target frameworks.", diagnostics: diagnostics));
        }

        var tfms = maui.SelectMany(project => project.TargetFrameworks).Distinct(StringComparer.OrdinalIgnoreCase);
        var worst = diagnostics.Any(item => item.Severity == CheckStatus.Warn);
        return Task.FromResult(worst
            ? Warn(string.Join(", ", tfms), "Add platform TFMs to app projects.", diagnostics)
            : Pass(string.Join(", ", tfms)));
    }
}
