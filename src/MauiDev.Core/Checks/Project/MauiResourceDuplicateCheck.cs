using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MauiDev.Checks;

public sealed class MauiResourceDuplicateCheck : CheckBase
{
    public override string Id => "maui-resources";
    public override CheckCategory Category => CheckCategory.Project;
    public override string Title => "Duplicate resources";
    public override bool CanFix => true;

    public override Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        var duplicates = FindDuplicates(graph);
        if (duplicates.Count == 0)
        {
            var count = graph.Projects.Sum(project => project.MauiItems.Count);
            return Task.FromResult(count == 0
                ? Skip("No MauiImage / MauiSplashScreen / MauiIcon / MauiFont items.")
                : Pass("None detected"));
        }

        var diagnostics = duplicates.Select(item => new DiagnosticFinding
        {
            Id = "MD030",
            Message = $"Duplicate {item.Kind} '{item.Include}'",
            File = item.ProjectPath,
            Severity = CheckStatus.Fail,
            Why = "Identical MAUI resource items confuse pack and can break splash/icon generation.",
            NextStep = "Keep one Include and delete the extras (maui-dev doctor --fix)."
        }).ToArray();

        return Task.FromResult(Fail(
            $"{duplicates.Count} detected",
            "Remove duplicate MauiSplashScreen / MauiImage entries.",
            diagnostics,
            canFix: true));
    }

    public override Task<FixResult> FixAsync(CheckContext context, CheckResult result, CancellationToken cancellationToken)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        var changes = new List<string>();
        foreach (var project in graph.Projects)
        {
            var updated = DeduplicateItems(project.Xml);
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
                ? $"Would deduplicate MAUI resource items in {changes.Count} project(s)."
                : $"Deduplicated MAUI resource items in {changes.Count} project(s).",
            Changes = changes
        });
    }

    internal static IReadOnlyList<MauiItem> FindDuplicates(ProjectGraph graph) =>
        graph.Projects
            .SelectMany(project => project.MauiItems)
            .GroupBy(item => (item.Kind, Normalize(item.Include)), item => item)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Skip(1))
            .ToArray();

    internal static string DeduplicateItems(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var remove = new List<XElement>();
            foreach (var element in document.Descendants().Where(IsMauiItem).ToArray())
            {
                var include = element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value ?? string.Empty;
                var key = element.Name.LocalName + "|" + Normalize(include);
                if (!seen.Add(key))
                {
                    remove.Add(element);
                }
            }

            foreach (var element in remove)
            {
                element.Remove();
            }

            return document.Declaration is null
                ? document.ToString()
                : document.Declaration + Environment.NewLine + document;
        }
        catch (System.Xml.XmlException)
        {
            return Regex.Replace(xml, @"(<MauiSplashScreen\b[^>]*/>\s*)\1+", "$1", RegexOptions.IgnoreCase);
        }
    }

    static bool IsMauiItem(XElement element) =>
        element.Name.LocalName is "MauiImage" or "MauiSplashScreen" or "MauiIcon" or "MauiFont" or "MauiAsset";

    static string Normalize(string include) => include.Replace('\\', '/').Trim();
}
