using System.Text.RegularExpressions;

namespace MauiDev.Checks;

public sealed class UseMauiCheck : CheckBase
{
    public override string Id => "use-maui";
    public override CheckCategory Category => CheckCategory.Project;
    public override string Title => "UseMaui";
    public override bool CanFix => true;

    public override Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        var missing = graph.Projects
            .Where(project => project.LooksLikeMaui && !project.UseMaui)
            .ToArray();
        if (missing.Length == 0)
        {
            var maui = graph.Projects.Count(project => project.UseMaui);
            return Task.FromResult(maui == 0
                ? Skip("No MAUI project signals in this tree.")
                : Pass($"{maui} project(s)"));
        }

        var names = string.Join(", ", missing.Select(project => Path.GetFileName(project.Path)));
        return Task.FromResult(Fail(
            $"Missing <UseMaui>true</UseMaui> in {names}.",
            "Add <UseMaui>true</UseMaui> to the first PropertyGroup.",
            canFix: true));
    }

    public override Task<FixResult> FixAsync(CheckContext context, CheckResult result, CancellationToken cancellationToken)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        var changes = new List<string>();
        foreach (var project in graph.Projects.Where(project => project.LooksLikeMaui && !project.UseMaui))
        {
            var updated = InsertUseMaui(project.Xml);
            if (string.Equals(updated, project.Xml, StringComparison.Ordinal))
            {
                continue;
            }

            changes.Add(project.Path);
            if (!context.DryRun)
            {
                context.Files.WriteAllText(project.Path, updated);
            }
        }

        return Task.FromResult(new FixResult
        {
            Applied = changes.Count > 0 && !context.DryRun,
            Message = context.DryRun
                ? $"Would add UseMaui to {changes.Count} project(s)."
                : $"Added UseMaui to {changes.Count} project(s).",
            Changes = changes
        });
    }

    internal static string InsertUseMaui(string xml)
    {
        if (Regex.IsMatch(xml, @"<UseMaui\s*>\s*true\s*</UseMaui>", RegexOptions.IgnoreCase))
        {
            return xml;
        }

        var match = Regex.Match(xml, @"<PropertyGroup\b[^>]*>", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return xml.Replace("</Project>", "  <PropertyGroup>\n    <UseMaui>true</UseMaui>\n  </PropertyGroup>\n</Project>", StringComparison.Ordinal);
        }

        return string.Concat(xml.AsSpan(0, match.Index + match.Length), "\n    <UseMaui>true</UseMaui>", xml.AsSpan(match.Index + match.Length));
    }
}
