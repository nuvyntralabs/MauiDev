namespace MauiDev;

public static class ToolResolver
{
    public static string? Find(CheckContext context, string command)
    {
        var names = context.Host.IsWindows
            ? new[] { command + ".exe", command }
            : new[] { command };
        var candidates = new List<string>();
        foreach (var entry in context.Host.PathEntries)
        {
            foreach (var name in names)
            {
                candidates.Add(Path.Combine(entry, name));
            }
        }

        var home = context.Host.UserProfile;
        foreach (var name in names)
        {
            candidates.Add(Path.Combine(home, ".dotnet", "tools", name));
        }

        return candidates.FirstOrDefault(context.Files.FileExists);
    }
}
