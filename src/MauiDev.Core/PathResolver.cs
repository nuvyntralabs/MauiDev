namespace MauiDev;

public static class PathResolver
{
    public static string ResolveRoot(IFileSystem files, string? path)
    {
        var start = string.IsNullOrWhiteSpace(path)
            ? Environment.CurrentDirectory
            : Path.GetFullPath(path);

        if (files.FileExists(start) &&
            (start.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
             start.EndsWith(".sln", StringComparison.OrdinalIgnoreCase) ||
             start.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)))
        {
            return Path.GetDirectoryName(start) ?? start;
        }

        var current = files.DirectoryExists(start) ? start : Path.GetDirectoryName(start);
        while (!string.IsNullOrEmpty(current))
        {
            if (files.GetFiles(current, "*.sln", recursive: false).Count > 0 ||
                files.GetFiles(current, "*.slnx", recursive: false).Count > 0 ||
                files.GetFiles(current, "*.csproj", recursive: false).Count > 0)
            {
                return current;
            }

            var parent = Directory.GetParent(current)?.FullName;
            if (parent is null || string.Equals(parent, current, StringComparison.Ordinal))
            {
                break;
            }

            current = parent;
        }

        return files.DirectoryExists(start) ? start : Environment.CurrentDirectory;
    }
}
