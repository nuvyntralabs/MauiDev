namespace MauiDev.Checks;

public sealed class CocoaPodsCheck : CheckBase
{
    public override string Id => "cocoapods";
    public override CheckCategory Category => CheckCategory.Machine;
    public override string Title => "CocoaPods";

    public override async Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        if (!context.Host.IsMacOs)
        {
            return Skip( "CocoaPods is only required on macOS.");
        }

        var result = await context.Process.RunAsync("pod", ["--version"], context.RootPath, context.Timeout, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            return Warn( "Not installed", "Install CocoaPods: sudo gem install cocoapods");
        }

        var version = result.StandardOutput.Trim().Split('\n')[0].Trim();
        return Pass( string.IsNullOrEmpty(version) ? "Installed" : version);
    }
}
