namespace MauiDev;

public sealed class CheckContext
{
    public required string RootPath { get; init; }
    public required IProcessRunner Process { get; init; }
    public required IFileSystem Files { get; init; }
    public required IHostEnvironment Host { get; init; }
    public bool Ci { get; init; }
    public bool Fix { get; init; }
    public bool DryRun { get; init; }
    public bool WarnAsError { get; init; }
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
    public IReadOnlySet<string> IgnoreIds { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public static CheckContext CreateDefault(string rootPath, bool ci = false, bool fix = false, bool dryRun = false, bool warnAsError = false, TimeSpan? timeout = null)
    {
        var files = new PhysicalFileSystem();
        return new CheckContext
        {
            RootPath = Path.GetFullPath(rootPath),
            Process = new ProcessRunner(),
            Files = files,
            Host = new SystemHostEnvironment(),
            Ci = ci,
            Fix = fix,
            DryRun = dryRun,
            WarnAsError = warnAsError || ci,
            Timeout = timeout ?? TimeSpan.FromSeconds(30),
            IgnoreIds = MauiDevIgnore.Load(files, rootPath)
        };
    }
}
