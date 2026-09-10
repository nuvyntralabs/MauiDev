using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MauiDev;

public sealed class PermissionsEngine
{
    static readonly HashSet<string> UnusedHints = new(StringComparer.OrdinalIgnoreCase)
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

    static readonly HashSet<string> Android13 = new(StringComparer.OrdinalIgnoreCase)
    {
        "android.permission.READ_MEDIA_IMAGES",
        "android.permission.READ_MEDIA_VIDEO",
        "android.permission.READ_MEDIA_AUDIO",
        "android.permission.POST_NOTIFICATIONS"
    };

    static readonly (string Hint, string[] Keys)[] IosUsage =
    [
        ("Camera", ["NSCameraUsageDescription"]),
        ("MediaPicker", ["NSCameraUsageDescription", "NSPhotoLibraryUsageDescription", "NSPhotoLibraryAddUsageDescription"]),
        ("Geolocation", ["NSLocationWhenInUseUsageDescription", "NSLocationAlwaysAndWhenInUseUsageDescription", "NSLocationAlwaysUsageDescription"]),
        ("Geolocator", ["NSLocationWhenInUseUsageDescription", "NSLocationAlwaysAndWhenInUseUsageDescription"]),
        ("Bluetooth", ["NSBluetoothAlwaysUsageDescription", "NSBluetoothPeripheralUsageDescription"]),
        ("Contacts", ["NSContactsUsageDescription"]),
        ("Photo", ["NSPhotoLibraryUsageDescription", "NSPhotoLibraryAddUsageDescription"]),
        ("Microphone", ["NSMicrophoneUsageDescription"]),
        ("Nfc", ["NFCReaderUsageDescription"])
    ];

    public DoctorReport Run(CheckContext context)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        var hasAndroid = graph.Projects.Any(project => project.HasAndroid) || graph.Manifests.Count > 0;
        var hasIos = graph.Projects.Any(project => project.HasIos) || graph.Plists.Count > 0;
        if (!hasAndroid && !hasIos)
        {
            return CommandReport.Create("permissions", "permissions", "Permissions", CheckCategory.Project, [], "None declared", "No Android or iOS targets.");
        }

        var diagnostics = context.Filter(Analyze(context, graph, hasAndroid, hasIos));
        var canFix = diagnostics.Any(item => item.Id == "MD021");
        List<FixResult>? fixes = null;
        if (context.Fix && canFix)
        {
            fixes = [Deduplicate(context, graph)];
        }

        return CommandReport.Create(
            "permissions",
            "permissions",
            "Permissions",
            CheckCategory.Project,
            diagnostics,
            "No unused, duplicate, or missing usage strings",
            canFix: canFix,
            fixes: fixes);
    }

    static IReadOnlyList<DiagnosticFinding> Analyze(CheckContext context, ProjectGraph graph, bool hasAndroid, bool hasIos)
    {
        var diagnostics = new List<DiagnosticFinding>();
        var blob = ProjectGraph.SourceBlob(context.Files, graph);

        if (hasAndroid)
        {
            var declared = new List<string>();
            var entries = new List<ManifestPermission>();
            foreach (var manifest in graph.Manifests)
            {
                entries.AddRange(ProjectGraph.ReadManifestPermissionEntries(context.Files, manifest));
            }

            declared.AddRange(entries.Select(entry => entry.Name));
            foreach (var project in graph.Projects)
            {
                declared.AddRange(project.UsesPermissions);
            }

            foreach (var group in declared.GroupBy(name => name, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD021",
                    Message = "Duplicate " + group.Key,
                    Severity = CheckStatus.Warn,
                    Why = "The same permission is declared more than once.",
                    NextStep = "Keep one UsesPermission / AndroidManifest entry (maui-dev permissions --fix)."
                });
            }

            foreach (var permission in declared.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (UnusedHints.Contains(permission) && !IsReferenced(blob, permission))
                {
                    diagnostics.Add(new DiagnosticFinding
                    {
                        Id = "MD020",
                        Message = permission + " looks unused",
                        Severity = CheckStatus.Warn,
                        Why = "The permission is declared but no source/XAML reference was found.",
                        NextStep = "Remove it from AndroidManifest.xml / UsesPermission only if native code does not need it."
                    });
                }
            }

            var thirteenPath = blob.Contains("Tiramisu", StringComparison.OrdinalIgnoreCase)
                               || blob.Contains("SdkInt >= 33", StringComparison.Ordinal)
                               || blob.Contains("BuildVersionCodes.Tiramisu", StringComparison.Ordinal);
            foreach (var entry in entries.Where(entry => Android13.Contains(entry.Name)))
            {
                if (string.IsNullOrWhiteSpace(entry.MaxSdkVersion) && !thirteenPath)
                {
                    diagnostics.Add(new DiagnosticFinding
                    {
                        Id = "MD024",
                        Message = entry.Name + " is an Android 13+ permission without maxSdkVersion or a 13+ code path",
                        File = entry.File,
                        Severity = CheckStatus.Warn,
                        Why = "READ_MEDIA_* / POST_NOTIFICATIONS require API 33 handling.",
                        NextStep = "Add a Tiramisu / SDK 33 guard or set android:maxSdkVersion on the legacy permission."
                    });
                }
            }
        }

        if (hasIos)
        {
            var keys = graph.Plists
                .SelectMany(path => ProjectGraph.ReadPlistKeys(context.Files, path).Select(key => (path, key)))
                .ToArray();
            var keySet = keys.Select(item => item.key).ToHashSet(StringComparer.Ordinal);
            foreach (var (hint, required) in IosUsage)
            {
                var referenced = blob.Contains(hint, StringComparison.OrdinalIgnoreCase);
                var present = required.Any(keySet.Contains);
                if (referenced && !present)
                {
                    diagnostics.Add(new DiagnosticFinding
                    {
                        Id = "MD022",
                        Message = $"Missing {required[0]} for {hint} usage",
                        File = graph.Plists.FirstOrDefault(),
                        Severity = CheckStatus.Fail,
                        Why = "iOS rejects APIs that lack a matching NS*UsageDescription.",
                        NextStep = $"Add {required[0]} to Info.plist."
                    });
                }
            }

            var known = IosUsage.SelectMany(map => map.Keys).ToHashSet(StringComparer.Ordinal);
            foreach (var (path, key) in keys.Where(item => item.key.StartsWith("NS", StringComparison.Ordinal) && item.key.Contains("UsageDescription", StringComparison.Ordinal)))
            {
                if (!known.Contains(key))
                {
                    continue;
                }

                var hints = IosUsage.Where(map => map.Keys.Contains(key)).Select(map => map.Hint).ToArray();
                if (hints.Length > 0 && hints.All(hint => blob.IndexOf(hint, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    diagnostics.Add(new DiagnosticFinding
                    {
                        Id = "MD023",
                        Message = key + " looks unused",
                        File = path,
                        Severity = CheckStatus.Warn,
                        Why = "The usage string is present but no matching API reference was found.",
                        NextStep = "Remove the Info.plist key if the API is unused."
                    });
                }
            }
        }

        return diagnostics;
    }

    static FixResult Deduplicate(CheckContext context, ProjectGraph graph)
    {
        var changes = new List<string>();
        foreach (var project in graph.Projects)
        {
            var updated = DeduplicateUsesPermission(project.Xml);
            if (string.Equals(updated, project.Xml, StringComparison.Ordinal))
            {
                continue;
            }

            changes.Add(project.Path);
            if (!context.DryRun)
            {
                context.Files.WriteAllText(project.Path, updated);
            }
        }

        foreach (var manifest in graph.Manifests)
        {
            var original = context.Files.ReadAllText(manifest);
            var updated = DeduplicateManifest(original);
            if (string.Equals(updated, original, StringComparison.Ordinal))
            {
                continue;
            }

            changes.Add(manifest);
            if (!context.DryRun)
            {
                context.Files.WriteAllText(manifest, updated);
            }
        }

        return new FixResult
        {
            Applied = changes.Count > 0 && !context.DryRun,
            Message = context.DryRun
                ? $"Would deduplicate Android permissions in {changes.Count} file(s)."
                : $"Deduplicated Android permissions in {changes.Count} file(s).",
            Changes = changes
        };
    }

    internal static string DeduplicateUsesPermission(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var remove = new List<XElement>();
            foreach (var element in document.Descendants().Where(item => string.Equals(item.Name.LocalName, "UsesPermission", StringComparison.OrdinalIgnoreCase)).ToArray())
            {
                var include = element.Attribute("Include")?.Value ?? element.Value;
                if (string.IsNullOrWhiteSpace(include) || !seen.Add(include.Trim()))
                {
                    if (!string.IsNullOrWhiteSpace(include))
                    {
                        remove.Add(element);
                    }
                }
            }

            foreach (var element in remove)
            {
                element.Remove();
            }

            return document.Declaration is null
                ? document.ToString()
                : document.Declaration + Environment.NewLine + document;
        }
        catch (System.Xml.XmlException)
        {
            return xml;
        }
    }

    internal static string DeduplicateManifest(string xml)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return Regex.Replace(xml, @"[ \t]*<uses-permission\b[^>]*/?>\s*", match =>
        {
            var name = Regex.Match(match.Value, @"android:name\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (!name.Success)
            {
                return match.Value;
            }

            return seen.Add(name.Groups[1].Value) ? match.Value : string.Empty;
        }, RegexOptions.IgnoreCase);
    }

    static bool IsReferenced(string blob, string permission)
    {
        var shortName = permission.Split('.')[^1];
        return blob.Contains(permission, StringComparison.OrdinalIgnoreCase)
               || blob.Contains(shortName, StringComparison.OrdinalIgnoreCase);
    }
}
