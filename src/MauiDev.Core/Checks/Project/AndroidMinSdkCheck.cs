namespace MauiDev.Checks;

public sealed class AndroidMinSdkCheck : CheckBase
{
    public const int RecommendedMin = 24;
    public const int MauiDefaultMin = 21;

    public override string Id => "android-min-sdk";
    public override CheckCategory Category => CheckCategory.Project;
    public override string Title => "Android Min SDK";

    public override Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        var android = graph.Projects.Where(project => project.HasAndroid).ToArray();
        if (android.Length == 0)
        {
            return Task.FromResult(Skip("No Android target framework."));
        }

        var mins = new List<int>();
        foreach (var project in android)
        {
            var manifest = graph.Manifests.FirstOrDefault(path =>
                path.StartsWith(Path.GetDirectoryName(project.Path) ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            var min = ProjectGraph.ReadMinSdk(context.Files, manifest, project);
            if (min is int value)
            {
                mins.Add(value);
            }
        }

        if (mins.Count == 0)
        {
            return Task.FromResult(Warn("Not declared", "Set SupportedOSPlatformVersion or android:minSdkVersion (21+)."));
        }

        var lowest = mins.Min();
        if (lowest < MauiDefaultMin)
        {
            return Task.FromResult(Warn($"{lowest}", $"MAUI requires Android API {MauiDefaultMin}+. Do not auto-bump without testing."));
        }

        if (lowest < RecommendedMin)
        {
            return Task.FromResult(Warn($"{lowest}", $"Play Console currently expects minSdk {RecommendedMin}+ for new apps. Review before changing."));
        }

        return Task.FromResult(Pass(lowest.ToString()));
    }
}
