namespace MauiDev;

public sealed class CleanRequest
{
    public bool NugetHttpCache { get; init; }
    public bool WorkloadTemp { get; init; }
    public bool NugetGlobal { get; init; }
    public bool Force { get; init; }
    public bool Yes { get; init; }
}

public sealed class CleanEngine
{
    public DoctorReport Run(CheckContext context, CleanRequest request)
    {
        var deleted = new List<string>();
        var skipped = new List<string>();

        foreach (var folder in FindBinObj(context.Files, context.RootPath))
        {
            deleted.Add(folder);
            if (!context.DryRun)
            {
                context.Files.DeleteDirectory(folder);
            }
        }

        if (request.NugetHttpCache)
        {
            var cache = Path.Combine(context.Host.UserProfile, ".local", "share", "NuGet", "http-cache");
            var windows = Path.Combine(context.Host.UserProfile, "AppData", "Local", "NuGet", "v3-cache");
            TryDelete(context, request.Yes, cache, deleted, skipped);
            TryDelete(context, request.Yes, windows, deleted, skipped);
        }

        if (request.WorkloadTemp)
        {
            var temp = Path.Combine(context.Host.UserProfile, ".dotnet", "workload-temp");
            TryDelete(context, request.Yes, temp, deleted, skipped);
        }

        if (request.NugetGlobal)
        {
            var packages = Path.Combine(context.Host.UserProfile, ".nuget", "packages");
            if (!request.Force)
            {
                skipped.Add(packages + " (pass --force to delete the global NuGet cache)");
            }
            else
            {
                TryDelete(context, request.Yes || request.Force, packages, deleted, skipped);
            }
        }

        var result = new CheckResult
        {
            CheckId = "clean",
            Title = "Clean",
            Category = CheckCategory.Project,
            Status = CheckStatus.Pass,
            Detail = context.DryRun
                ? $"Would remove {deleted.Count} folder(s)"
                : $"Removed {deleted.Count} folder(s)",
            Diagnostics = deleted.Select(path => new DiagnosticFinding
            {
                Id = "MD300",
                Message = (context.DryRun ? "Would delete " : "Deleted ") + path,
                File = path,
                Severity = CheckStatus.Pass
            }).Concat(skipped.Select(path => new DiagnosticFinding
            {
                Id = "MD301",
                Message = "Skipped " + path,
                Severity = CheckStatus.Warn
            })).ToArray()
        };

        return new DoctorReport
        {
            Command = "clean",
            Results = [result],
            Recommendations = skipped.Count == 0 ? [] : skipped.ToArray()
        };
    }

    static void TryDelete(CheckContext context, bool confirmed, string path, List<string> deleted, List<string> skipped)
    {
        if (!context.Files.DirectoryExists(path))
        {
            return;
        }

        if (!confirmed && !context.DryRun)
        {
            skipped.Add(path + " (pass --yes)");
            return;
        }

        deleted.Add(path);
        if (!context.DryRun)
        {
            context.Files.DeleteDirectory(path);
        }
    }

    static IEnumerable<string> FindBinObj(IFileSystem files, string root)
    {
        var found = new List<string>();
        Walk(files, root, found, depth: 0);
        return found;
    }

    static void Walk(IFileSystem files, string directory, List<string> found, int depth)
    {
        if (depth > 12)
        {
            return;
        }

        foreach (var child in files.GetDirectories(directory))
        {
            var name = Path.GetFileName(child);
            if (name.Equals("bin", StringComparison.OrdinalIgnoreCase) || name.Equals("obj", StringComparison.OrdinalIgnoreCase))
            {
                found.Add(child);
                continue;
            }

            if (name.Equals("node_modules", StringComparison.OrdinalIgnoreCase) || name.Equals(".git", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Walk(files, child, found, depth + 1);
        }
    }
}
