namespace MauiDev.Checks;

public sealed class SigningCheck : CheckBase
{
    public override string Id => "signing";
    public override CheckCategory Category => CheckCategory.Project;
    public override string Title => "Signing";

    public override Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        var android = graph.Projects.Where(project => project.HasAndroid).ToArray();
        var ios = graph.Projects.Where(project => project.HasIos).ToArray();
        if (android.Length == 0 && ios.Length == 0)
        {
            return Task.FromResult(Skip("No Android or iOS target frameworks."));
        }

        var warnings = new List<string>();
        if (android.Any(project => !project.HasAndroidSigning))
        {
            warnings.Add("AndroidSigning* properties are not set (debug keystore is fine locally).");
        }

        if (ios.Length > 0 && graph.Plists.Count == 0)
        {
            warnings.Add("No Info.plist / entitlements were found for iOS targets.");
        }

        if (warnings.Count == 0)
        {
            return Task.FromResult(Pass("Configured or using platform defaults"));
        }

        return Task.FromResult(Warn(
            string.Join(" ", warnings),
            "Set store signing in CI secrets. maui-dev never writes keystores or provisioning profiles."));
    }
}
