namespace MauiDev.Checks;

public sealed class PermissionsCheck : CheckBase
{
    static readonly HashSet<string> CommonUnusedHints = new(StringComparer.OrdinalIgnoreCase)
    {
        "android.permission.ACCESS_FINE_LOCATION",
        "android.permission.ACCESS_COARSE_LOCATION",
        "android.permission.CAMERA",
        "android.permission.RECORD_AUDIO",
        "android.permission.READ_CONTACTS",
        "android.permission.BLUETOOTH_CONNECT",
        "android.permission.BLUETOOTH_SCAN",
        "android.permission.NFC"
    };

    public override string Id => "android-permissions";
    public override CheckCategory Category => CheckCategory.Project;
    public override string Title => "Android permissions";

    public override Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        if (graph.Projects.All(project => !project.HasAndroid) && graph.Manifests.Count == 0)
        {
            return Task.FromResult(Skip("No Android manifests."));
        }

        var declared = new List<string>();
        foreach (var manifest in graph.Manifests)
        {
            declared.AddRange(ProjectGraph.ReadManifestPermissions(context.Files, manifest));
        }

        foreach (var project in graph.Projects)
        {
            declared.AddRange(project.UsesPermissions);
        }

        var unique = declared.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var duplicates = declared
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        var blob = string.Join('\n', graph.SourceFiles.Select(path =>
        {
            try
            {
                return context.Files.ReadAllText(path);
            }
            catch (IOException)
            {
                return string.Empty;
            }
        }));

        var unused = unique
            .Where(permission => CommonUnusedHints.Contains(permission))
            .Where(permission => !IsReferenced(blob, permission))
            .ToArray();

        var diagnostics = unused.Select(permission => new DiagnosticFinding
        {
            Id = "MD020",
            Message = permission + " looks unused",
            Severity = CheckStatus.Warn,
            Why = "The permission is declared but no source/XAML reference was found.",
            NextStep = "Remove it from AndroidManifest.xml / UsesPermission only if native code does not need it."
        }).ToArray();

        if (duplicates.Length > 0)
        {
            return Task.FromResult(Warn(
                $"{duplicates.Length} duplicate permission(s)",
                "Deduplicate UsesPermission / AndroidManifest entries.",
                diagnostics.Concat(duplicates.Select(name => new DiagnosticFinding
                {
                    Id = "MD021",
                    Message = "Duplicate " + name,
                    Severity = CheckStatus.Warn
                })).ToArray()));
        }

        if (unused.Length > 0)
        {
            return Task.FromResult(Warn(
                $"{unused.Length} unused permission(s)",
                "Remove unused permissions after confirming native code does not need them.",
                diagnostics));
        }

        return Task.FromResult(Pass(unique.Length == 0 ? "None declared" : unique.Length + " declared"));
    }

    static bool IsReferenced(string blob, string permission)
    {
        var shortName = permission.Split('.')[^1];
        return blob.Contains(permission, StringComparison.OrdinalIgnoreCase)
               || blob.Contains(shortName, StringComparison.OrdinalIgnoreCase);
    }
}
