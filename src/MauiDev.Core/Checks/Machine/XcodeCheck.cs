using System.Text.RegularExpressions;

namespace MauiDev.Checks;

public sealed class XcodeCheck : CheckBase
{
    public override string Id => "xcode";
    public override CheckCategory Category => CheckCategory.Machine;
    public override string Title => "Xcode";

    public override async Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        if (!context.Host.IsMacOs)
        {
            return Skip( "Xcode is only required on macOS.");
        }

        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        if (graph.Projects.Count > 0 && graph.Projects.All(project => !project.HasIos && !project.TargetFrameworks.Any(tfm => tfm.Contains("maccatalyst", StringComparison.OrdinalIgnoreCase))))
        {
            return Skip( "No iOS or Mac Catalyst target framework.");
        }

        var result = await context.Process.RunAsync("xcodebuild", ["-version"], context.RootPath, context.Timeout, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            return Fail( "xcodebuild was not found.", "Install Xcode and run xcode-select --install.");
        }

        var match = Regex.Match(result.StandardOutput, @"Xcode\s+(\d+\.\d+)");
        return Pass( match.Success ? match.Groups[1].Value : "Installed");
    }
}
