namespace MauiDev.Checks;

public sealed class AndroidSdkCheck : CheckBase
{
    public override string Id => "android-sdk";
    public override CheckCategory Category => CheckCategory.Machine;
    public override string Title => "Android SDK";

    public override Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        if (graph.Projects.Count > 0 && graph.Projects.All(project => !project.HasAndroid))
        {
            return Task.FromResult(Skip( "No Android target framework in this tree."));
        }

        var home = context.Host.GetVariable("ANDROID_HOME")
                   ?? context.Host.GetVariable("ANDROID_SDK_ROOT");
        if (string.IsNullOrWhiteSpace(home) || !context.Files.DirectoryExists(home))
        {
            return Task.FromResult(Fail( "ANDROID_HOME / ANDROID_SDK_ROOT is not set.", "Install the Android SDK and export ANDROID_HOME."));
        }

        var platforms = Path.Combine(home, "platforms");
        var apis = context.Files.GetDirectories(platforms)
            .Select(Path.GetFileName)
            .Where(name => name is not null && name.StartsWith("android-", StringComparison.OrdinalIgnoreCase))
            .Select(name => name![ "android-".Length.. ])
            .ToArray();
        if (apis.Length == 0)
        {
            return Task.FromResult(Warn( home, "Install at least one Android platform package (API 36 recommended)."));
        }

        var latest = apis.OrderByDescending(api => int.TryParse(api, out var value) ? value : 0).First();
        return Task.FromResult(Pass( "API " + latest));
    }
}
