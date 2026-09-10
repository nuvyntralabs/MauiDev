using Spectre.Console;

namespace MauiDev;

public sealed class HumanReporter : IReporter
{
    public ReportFormat Format => ReportFormat.Human;

    public void Write(DoctorReport report, TextWriter writer)
    {
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.Detect,
            ColorSystem = ColorSystemSupport.Detect,
            Out = new AnsiConsoleOutput(writer)
        });

        console.WriteLine();
        console.MarkupLine("[bold].NET MAUI Developer Doctor[/]");
        console.WriteLine("────────────────────────────────────");
        console.WriteLine();

        WriteSection(console, "Machine", report.Results.Where(result => result.Category == CheckCategory.Machine));
        WriteSection(console, "Project", report.Results.Where(result => result.Category == CheckCategory.Project));
        WriteSection(console, "Resources", report.Results.Where(result => result.Category == CheckCategory.Resources));
        WriteSection(console, "Packaging", report.Results.Where(result => result.Category == CheckCategory.Packaging));

        if (report.Recommendations.Count > 0)
        {
            console.MarkupLine("[bold]Recommendations[/]");
            console.WriteLine("────────────────────────────────────");
            console.WriteLine();
            var index = 1;
            foreach (var recommendation in report.Recommendations)
            {
                console.WriteLine($"[{index}] {recommendation}");
                index++;
            }

            console.WriteLine();
        }

        if (report.Fixes.Count > 0)
        {
            console.MarkupLine("[bold]Fixes[/]");
            console.WriteLine("────────────────────────────────────");
            console.WriteLine();
            foreach (var fix in report.Fixes)
            {
                console.WriteLine(fix.Message);
                foreach (var change in fix.Changes)
                {
                    console.WriteLine("  " + change);
                }
            }

            console.WriteLine();
        }
    }

    static void WriteSection(IAnsiConsole console, string title, IEnumerable<CheckResult> results)
    {
        var items = results.ToArray();
        if (items.Length == 0)
        {
            return;
        }

        console.MarkupLine($"[bold]{Markup.Escape(title)}[/]");
        console.WriteLine("────────────────────────────────────");
        console.WriteLine();
        foreach (var result in items)
        {
            var mark = result.Status switch
            {
                CheckStatus.Pass => "[green]✓[/]",
                CheckStatus.Warn => "[yellow]⚠[/]",
                CheckStatus.Fail => "[red]✗[/]",
                _ => "[grey]·[/]"
            };
            var detail = string.IsNullOrWhiteSpace(result.Detail) ? result.Status.ToString() : result.Detail;
            console.MarkupLine($"{mark} {Markup.Escape(result.Title),-22} {Markup.Escape(detail)}");
        }

        console.WriteLine();
    }
}

public static class ReporterFactory
{
    public static IReporter Create(ReportFormat format) => format switch
    {
        ReportFormat.Json => new JsonReporter(),
        ReportFormat.Sarif => new SarifReporter(),
        _ => new HumanReporter()
    };

    public static ReportFormat Parse(string? value, bool ci)
    {
        if (ci)
        {
            return ReportFormat.Json;
        }

        return value?.ToLowerInvariant() switch
        {
            "json" => ReportFormat.Json,
            "sarif" => ReportFormat.Sarif,
            _ => ReportFormat.Human
        };
    }
}
