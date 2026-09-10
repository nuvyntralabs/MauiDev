namespace MauiDev.Checks;

public sealed class MauiWorkloadCheck : CheckBase
{
    public override string Id => "maui-workload";
    public override CheckCategory Category => CheckCategory.Machine;
    public override string Title => "MAUI Workload";

    public override async Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        var result = await context.Process.RunAsync("dotnet", ["workload", "list"], context.RootPath, context.Timeout, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            return Fail( "dotnet workload list failed.", "Run: dotnet workload install maui-android");
        }

        var output = result.StandardOutput + Environment.NewLine + result.StandardError;
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        var needed = new List<string>();
        if (graph.Projects.Any(project => project.HasAndroid) || graph.Projects.Count == 0)
        {
            needed.Add("maui-android");
        }

        if (graph.Projects.Any(project => project.HasIos) && context.Host.IsMacOs)
        {
            needed.Add("maui-ios");
        }

        if (graph.Projects.Any(project => project.TargetFrameworks.Any(tfm => tfm.Contains("maccatalyst", StringComparison.OrdinalIgnoreCase))) && context.Host.IsMacOs)
        {
            needed.Add("maui-maccatalyst");
        }

        if (graph.Projects.Any(project => project.TargetFrameworks.Any(tfm => tfm.Contains("-windows", StringComparison.OrdinalIgnoreCase))) && context.Host.IsWindows)
        {
            needed.Add("maui-windows");
        }

        if (needed.Count == 0)
        {
            needed.Add("maui-android");
        }

        var missing = needed
            .Where(id => !output.Contains(id, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (missing.Length > 0)
        {
            var command = "dotnet workload install " + string.Join(' ', missing);
            return Fail( "Missing: " + string.Join(", ", missing) + ".", command);
        }

        var versionMatch = System.Text.RegularExpressions.Regex.Match(output, @"maui[^\n]*?(\d+\.\d+\.\d+)");
        return Pass( versionMatch.Success ? versionMatch.Groups[1].Value : "Installed");
    }
}
