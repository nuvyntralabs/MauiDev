namespace MauiDev;

public sealed class TelemetryEngine
{
    static readonly (string Id, string[] Tokens)[] Reporters =
    [
        ("Plugin.Maui.Diagnostics", ["Plugin.Maui.Diagnostics", "UseMauiDiagnostics", "IDiagnostics"]),
        ("Plugin.Maui.Observability", ["Plugin.Maui.Observability", "UseMauiObservability"]),
        ("Sentry", ["Sentry.Maui", "SentrySdk", "UseSentry"]),
        ("Firebase Crashlytics", ["Firebase.Crashlytics", "Crashlytics", "FirebaseCrashlytics"]),
        ("Application Insights", ["Microsoft.ApplicationInsights", "TelemetryClient"])
    ];

    static readonly string[] AppCenterTokens =
    [
        "Microsoft.AppCenter",
        "AppCenter.Start",
        "AppCenter.Analytics",
        "AppCenter.Crashes",
        "Microsoft.AppCenter.Analytics",
        "Microsoft.AppCenter.Crashes"
    ];

    public DoctorReport Run(CheckContext context)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        var maui = graph.MauiProjects;
        if (maui.Count == 0)
        {
            return CommandReport.Create("telemetry", "telemetry", "Telemetry scan", CheckCategory.Project, [], "OK", "No MAUI projects found.");
        }

        var diagnostics = context.Filter(Analyze(context, graph));
        return CommandReport.Create(
            "telemetry",
            "telemetry",
            "Telemetry scan",
            CheckCategory.Project,
            diagnostics,
            "Crash / analytics SDK scan complete (CLI collects nothing)");
    }

    static IReadOnlyList<DiagnosticFinding> Analyze(CheckContext context, ProjectGraph graph)
    {
        var blob = ProjectGraph.SourceBlob(context.Files, graph);
        var packages = string.Join('\n', graph.Projects.SelectMany(project => project.PackageReferences.Select(item => item.Id)));
        var haystack = blob + "\n" + packages;
        var diagnostics = new List<DiagnosticFinding>();
        var found = new List<string>();

        foreach (var (id, tokens) in Reporters)
        {
            if (tokens.Any(token => haystack.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                found.Add(id);
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD700",
                    Message = id + " detected.",
                    Severity = CheckStatus.Pass,
                    Why = "A crash / analytics SDK is already referenced.",
                    NextStep = id.StartsWith("Plugin.Maui.", StringComparison.Ordinal)
                        ? null
                        : "Plugin.Maui.Diagnostics is the catalog sibling for MAUI crash / ANR breadcrumbs."
                });
            }
        }

        var appCenter = AppCenterTokens.Any(token => haystack.Contains(token, StringComparison.OrdinalIgnoreCase));
        if (appCenter)
        {
            diagnostics.Add(new DiagnosticFinding
            {
                Id = "MD702",
                Message = "App Center Analytics / Crashes is still referenced.",
                Severity = CheckStatus.Warn,
                Why = "Visual Studio App Center was retired.",
                NextStep = "Replace with Plugin.Maui.Diagnostics (or Sentry / Firebase)."
            });
        }

        if (found.Count == 0 && !appCenter)
        {
            diagnostics.Add(new DiagnosticFinding
            {
                Id = "MD701",
                Message = "No crash reporter was found in this MAUI app.",
                Severity = CheckStatus.Warn,
                Why = "Production apps need crash / ANR reporting.",
                NextStep = "Add Plugin.Maui.Diagnostics. maui-dev does not collect telemetry itself."
            });
        }

        return diagnostics;
    }
}
