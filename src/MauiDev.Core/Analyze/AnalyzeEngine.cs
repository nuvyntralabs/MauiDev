using System.Text.RegularExpressions;

namespace MauiDev;

public sealed class AnalyzeEngine
{
    static readonly Regex EventPlus = new(@"(\w+)\s*\+=\s*", RegexOptions.Compiled);
    static readonly Regex EventMinus = new(@"(\w+)\s*-=\s*", RegexOptions.Compiled);
    static readonly Regex HttpClient = new(@"new\s+HttpClient\s*\(", RegexOptions.Compiled);
    static readonly Regex DiscardedTask = new(@"(_\s*=\s*)?Task\.Run\s*\(", RegexOptions.Compiled);
    static readonly Regex FireForget = new(@"_ =\s+\w+", RegexOptions.Compiled);
    static readonly Regex MainThread = new(@"MainThread\.BeginInvokeOnMainThread", RegexOptions.Compiled);
    static readonly Regex ServiceAttr = new(@"\[\s*Service\b", RegexOptions.Compiled);
    static readonly Regex PlatformType = new(@"\b(Android\.|UIKit\.|Java\.Lang\.)", RegexOptions.Compiled);

    public DoctorReport Run(CheckContext context)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        var diagnostics = new List<DiagnosticFinding>();
        var csharp = graph.SourceFiles.Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));

        foreach (var path in csharp)
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

            var relative = Path.GetRelativePath(context.RootPath, path);
            AnalyzeFile(relative, text, diagnostics);
        }

        foreach (var manifest in graph.Manifests)
        {
            var hasService = csharp.Any(path =>
            {
                try
                {
                    return ServiceAttr.IsMatch(context.Files.ReadAllText(path));
                }
                catch (IOException)
                {
                    return false;
                }
            });
            if (hasService && !ProjectGraph.LooksLikeForegroundService(context.Files, manifest))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD106",
                    Message = "Android [Service] found without a foreground-service declaration.",
                    File = Path.GetRelativePath(context.RootPath, manifest),
                    Severity = CheckStatus.Warn,
                    Why = "Background services without a foreground type can be killed or rejected on modern Android.",
                    NextStep = "Declare foregroundServiceType in AndroidManifest.xml if the service runs in the background."
                });
            }
        }

        var status = diagnostics.Any(item => item.Severity == CheckStatus.Fail)
            ? CheckStatus.Fail
            : diagnostics.Count > 0 ? CheckStatus.Warn : CheckStatus.Pass;

        var result = new CheckResult
        {
            CheckId = "analyze",
            Title = "Source heuristics",
            Category = CheckCategory.Project,
            Status = status,
            Detail = diagnostics.Count == 0 ? "No heuristic issues" : diagnostics.Count + " finding(s)",
            Recommendation = diagnostics.Count == 0
                ? null
                : "Heuristics can false-positive. Confirm before changing code. Visual-tree leaks: Plugin.Maui.LeakAnalyser.",
            Diagnostics = diagnostics
        };

        return new DoctorReport
        {
            Command = "analyze",
            Results = [result],
            Recommendations = result.Recommendation is null ? [] : [result.Recommendation]
        };
    }

    static void AnalyzeFile(string relative, string text, List<DiagnosticFinding> diagnostics)
    {
        var plus = EventPlus.Matches(text).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        var minus = EventMinus.Matches(text).Select(match => match.Groups[1].Value).ToHashSet(StringComparer.Ordinal);
        foreach (var name in plus.Except(minus, StringComparer.Ordinal))
        {
            if (name is "property" or "field")
            {
                continue;
            }

            diagnostics.Add(Finding("MD100", relative, text, name + " +=", CheckStatus.Warn,
                "Event handler may cause object retention.",
                $"{name} is subscribed with += and never unsubscribed in this file.",
                "Unsubscribe in OnDisappearing/Dispose, or use Plugin.Maui.LeakAnalyser."));
        }

        if (HttpClient.IsMatch(text))
        {
            diagnostics.Add(Finding("MD101", relative, text, "new HttpClient(", CheckStatus.Warn,
                "HttpClient created in source.",
                "Creating HttpClient per request can exhaust sockets.",
                "Reuse a typed HttpClient via IHttpClientFactory or Plugin.Maui.HttpForge."));
        }

        if (DiscardedTask.IsMatch(text) || FireForget.IsMatch(text))
        {
            diagnostics.Add(Finding("MD102", relative, text, "Task.Run", CheckStatus.Warn,
                "Fire-and-forget Task detected.",
                "Discarded tasks hide exceptions and skip cancellation.",
                "Await the task or pass a CancellationToken."));
        }

        var mainThreadCount = MainThread.Matches(text).Count;
        if (mainThreadCount >= 3)
        {
            diagnostics.Add(Finding("MD103", relative, text, "MainThread.BeginInvokeOnMainThread", CheckStatus.Warn,
                $"MainThread.BeginInvokeOnMainThread used {mainThreadCount} times.",
                "Repeated marshaling often means work should already be on the UI thread.",
                "Collapse hops or use IMainThread from MVVMExpress."));
        }

        if (PlatformType.IsMatch(text) && !text.Contains("#if", StringComparison.Ordinal))
        {
            diagnostics.Add(Finding("MD105", relative, text, "Android.", CheckStatus.Warn,
                "Platform-specific API used without a platform guard.",
                "Android./UIKit types in shared code fail on other TFMs.",
                "Wrap with #if ANDROID / #if IOS or OperatingSystem.IsAndroid()."));
        }
    }

    static DiagnosticFinding Finding(string id, string file, string text, string needle, CheckStatus severity, string message, string why, string next)
    {
        var index = text.IndexOf(needle, StringComparison.Ordinal);
        var line = index < 0 ? 1 : text[..index].Count(ch => ch == '\n') + 1;
        return new DiagnosticFinding
        {
            Id = id,
            Message = message,
            File = file,
            Line = line,
            Severity = severity,
            Why = why,
            NextStep = next
        };
    }
}
