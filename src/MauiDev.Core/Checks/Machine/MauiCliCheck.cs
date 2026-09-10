namespace MauiDev.Checks;

public sealed class MauiCliCheck : CheckBase
{
    public override string Id => "maui-cli";
    public override CheckCategory Category => CheckCategory.Machine;
    public override string Title => "maui CLI";

    public override Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        var home = context.Host.UserProfile;
        var names = context.Host.IsWindows
            ? new[] { "maui.exe", "maui" }
            : new[] { "maui" };
        var candidates = new List<string>();
        foreach (var entry in context.Host.PathEntries)
        {
            foreach (var name in names)
            {
                candidates.Add(Path.Combine(entry, name));
            }
        }

        candidates.Add(Path.Combine(home, ".dotnet", "tools", context.Host.IsWindows ? "maui.exe" : "maui"));
        candidates.Add(Path.Combine(home, ".maui", context.Host.IsWindows ? "maui.exe" : "maui"));

        var found = candidates.FirstOrDefault(context.Files.FileExists);
        if (found is null)
        {
            return Task.FromResult(Warn( "Not on PATH", "Install the MAUI developer CLI if you use maui profile / maui-perf."));
        }

        return Task.FromResult(Pass( found));
    }
}
