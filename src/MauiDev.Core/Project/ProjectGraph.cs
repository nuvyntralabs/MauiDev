using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MauiDev;

public sealed class MauiItem
{
    public required string Kind { get; init; }
    public required string Include { get; init; }
    public required string ProjectPath { get; init; }
}

public sealed class ProjectDocument
{
    public required string Path { get; init; }
    public required string Xml { get; init; }
    public required IReadOnlyList<string> TargetFrameworks { get; init; }
    public required bool UseMaui { get; init; }
    public required bool IsPackable { get; init; }
    public required bool PackAsTool { get; init; }
    public string? ToolCommandName { get; init; }
    public string? Version { get; init; }
    public string? PackageVersion { get; init; }
    public string? PackageId { get; init; }
    public string? PackageReadmeFile { get; init; }
    public string? PackageIcon { get; init; }
    public string? PackageLicenseExpression { get; init; }
    public string? PackageLicenseFile { get; init; }
    public string? SupportedOsPlatformVersion { get; init; }
    public string? ApplicationId { get; init; }
    public bool HasAndroidSigning { get; init; }
    public string? AndroidSigningKeyStore { get; init; }
    public string? CodesignEntitlements { get; init; }
    public IReadOnlyList<MauiItem> MauiItems { get; init; } = [];
    public IReadOnlyList<string> UsesPermissions { get; init; } = [];
    public IReadOnlyList<string> NoneIncludes { get; init; } = [];
    public IReadOnlyList<PackageReferenceItem> PackageReferences { get; init; } = [];

    public bool HasAndroid => TargetFrameworks.Any(IsAndroidTfm);
    public bool HasIos => TargetFrameworks.Any(IsIosTfm);
    public bool HasWindows => TargetFrameworks.Any(IsWindowsTfm);
    public bool HasMacCatalyst => TargetFrameworks.Any(IsMacCatalystTfm);
    public bool LooksLikeMaui =>
        UseMaui ||
        HasAndroid ||
        HasIos ||
        MauiItems.Count > 0 ||
        TargetFrameworks.Any(tfm => tfm.Contains("-maccatalyst", StringComparison.OrdinalIgnoreCase));

    public static bool IsAndroidTfm(string tfm) => tfm.Contains("-android", StringComparison.OrdinalIgnoreCase);
    public static bool IsIosTfm(string tfm) => tfm.Contains("-ios", StringComparison.OrdinalIgnoreCase) && !IsMacCatalystTfm(tfm);
    public static bool IsWindowsTfm(string tfm) => tfm.Contains("-windows", StringComparison.OrdinalIgnoreCase);
    public static bool IsMacCatalystTfm(string tfm) => tfm.Contains("-maccatalyst", StringComparison.OrdinalIgnoreCase);
}

public sealed class PackageReferenceItem
{
    public required string Id { get; init; }
    public string? Version { get; init; }
    public required string ProjectPath { get; init; }
}

public sealed class ManifestPermission
{
    public required string Name { get; init; }
    public string? MaxSdkVersion { get; init; }
    public required string File { get; init; }
}

public sealed class ProjectGraph
{
    public required string RootPath { get; init; }
    public IReadOnlyList<ProjectDocument> Projects { get; init; } = [];
    public IReadOnlyList<string> Manifests { get; init; } = [];
    public IReadOnlyList<string> Plists { get; init; } = [];
    public IReadOnlyList<string> SourceFiles { get; init; } = [];

    public IReadOnlyList<ProjectDocument> MauiProjects =>
        Projects.Where(project => project.LooksLikeMaui).ToArray();

    public IReadOnlyList<ProjectDocument> PackableProjects =>
        Projects.Where(project => project.IsPackable).ToArray();

    public static ProjectGraph Load(IFileSystem files, string rootPath)
    {
        var projects = files.GetFiles(rootPath, "*.csproj", recursive: true)
            .Where(path => !IsBuildOutput(path))
            .Select(path => ParseProject(files, path))
            .ToArray();

        var manifests = files.GetFiles(rootPath, "AndroidManifest.xml", recursive: true)
            .Where(path => !IsBuildOutput(path))
            .ToArray();
        var plists = files.GetFiles(rootPath, "Info.plist", recursive: true)
            .Where(path => !IsBuildOutput(path))
            .ToArray();
        var sources = files.GetFiles(rootPath, "*.cs", recursive: true)
            .Concat(files.GetFiles(rootPath, "*.xaml", recursive: true))
            .Where(path => !IsBuildOutput(path))
            .ToArray();

        return new ProjectGraph
        {
            RootPath = rootPath,
            Projects = projects,
            Manifests = manifests,
            Plists = plists,
            SourceFiles = sources
        };
    }

    public static ProjectDocument ParseProject(IFileSystem files, string path)
    {
        var xml = files.ReadAllText(path);
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return new ProjectDocument
            {
                Path = path,
                Xml = xml,
                TargetFrameworks = [],
                UseMaui = false,
                IsPackable = false,
                PackAsTool = false
            };
        }

        var properties = document.Descendants()
            .Where(element => element.Name.LocalName is not ("ItemGroup" or "Project" or "PropertyGroup" or "PackageReference" or "ProjectReference" or "None" or "Compile"))
            .Where(element => !element.HasElements)
            .GroupBy(element => element.Name.LocalName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value.Trim(), StringComparer.OrdinalIgnoreCase);

        var tfms = new List<string>();
        if (properties.TryGetValue("TargetFrameworks", out var frameworks))
        {
            tfms.AddRange(frameworks.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        if (properties.TryGetValue("TargetFramework", out var framework) && !string.IsNullOrWhiteSpace(framework))
        {
            tfms.Add(framework);
        }

        var mauiKinds = new[] { "MauiImage", "MauiSplashScreen", "MauiIcon", "MauiFont", "MauiAsset" };
        var items = document.Descendants()
            .Where(element => mauiKinds.Contains(element.Name.LocalName, StringComparer.OrdinalIgnoreCase))
            .Select(element => new MauiItem
            {
                Kind = element.Name.LocalName,
                Include = element.Attribute("Include")?.Value ?? element.Attribute("Update")?.Value ?? string.Empty,
                ProjectPath = path
            })
            .Where(item => item.Include.Length > 0)
            .ToArray();

        var permissions = document.Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "UsesPermission", StringComparison.OrdinalIgnoreCase))
            .Select(element => element.Attribute("Include")?.Value ?? element.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();

        var noneIncludes = document.Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "None", StringComparison.OrdinalIgnoreCase))
            .Select(element => element.Attribute("Include")?.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToArray();

        var packageReferences = document.Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "PackageReference", StringComparison.OrdinalIgnoreCase))
            .Select(element => new PackageReferenceItem
            {
                Id = (element.Attribute("Include")?.Value ?? string.Empty).Trim(),
                Version = element.Attribute("Version")?.Value?.Trim()
                          ?? element.Elements().FirstOrDefault(child => string.Equals(child.Name.LocalName, "Version", StringComparison.OrdinalIgnoreCase))?.Value.Trim(),
                ProjectPath = path
            })
            .Where(item => item.Id.Length > 0)
            .ToArray();

        var isPackable = !string.Equals(Get(properties, "IsPackable"), "false", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(Get(properties, "IsTestProject"), "true", StringComparison.OrdinalIgnoreCase);
        if (string.Equals(Get(properties, "OutputType"), "Exe", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Get(properties, "PackAsTool"), "true", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(Get(properties, "PackageId")))
        {
            isPackable = !string.Equals(Get(properties, "IsPackable"), "false", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(Get(properties, "IsTestProject"), "true", StringComparison.OrdinalIgnoreCase);
        }

        var useMaui = string.Equals(Get(properties, "UseMaui"), "true", StringComparison.OrdinalIgnoreCase);
        return new ProjectDocument
        {
            Path = path,
            Xml = xml,
            TargetFrameworks = tfms.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            UseMaui = useMaui,
            IsPackable = isPackable,
            PackAsTool = string.Equals(Get(properties, "PackAsTool"), "true", StringComparison.OrdinalIgnoreCase),
            ToolCommandName = Get(properties, "ToolCommandName"),
            Version = Get(properties, "Version"),
            PackageVersion = Get(properties, "PackageVersion"),
            PackageId = Get(properties, "PackageId"),
            PackageReadmeFile = Get(properties, "PackageReadmeFile"),
            PackageIcon = Get(properties, "PackageIcon"),
            PackageLicenseExpression = Get(properties, "PackageLicenseExpression"),
            PackageLicenseFile = Get(properties, "PackageLicenseFile"),
            SupportedOsPlatformVersion = Get(properties, "SupportedOSPlatformVersion"),
            ApplicationId = Get(properties, "ApplicationId"),
            HasAndroidSigning = properties.Keys.Any(key => key.StartsWith("AndroidSigning", StringComparison.OrdinalIgnoreCase)),
            AndroidSigningKeyStore = Get(properties, "AndroidSigningKeyStore"),
            CodesignEntitlements = Get(properties, "CodesignEntitlements"),
            MauiItems = items,
            UsesPermissions = permissions,
            NoneIncludes = noneIncludes,
            PackageReferences = packageReferences
        };
    }

    public static IReadOnlyList<string> ReadManifestPermissions(IFileSystem files, string manifestPath) =>
        ReadManifestPermissionEntries(files, manifestPath)
            .Select(entry => entry.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    public static IReadOnlyList<ManifestPermission> ReadManifestPermissionEntries(IFileSystem files, string manifestPath)
    {
        var xml = files.ReadAllText(manifestPath);
        var entries = new List<ManifestPermission>();
        foreach (Match match in Regex.Matches(xml, @"<uses-permission\b([^>]*)/?>", RegexOptions.IgnoreCase))
        {
            var attrs = match.Groups[1].Value;
            var name = Regex.Match(attrs, @"android:name\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
            if (!name.Success || !name.Groups[1].Value.StartsWith("android.permission.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var maxSdk = Regex.Match(attrs, @"android:maxSdkVersion\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
            entries.Add(new ManifestPermission
            {
                Name = name.Groups[1].Value,
                MaxSdkVersion = maxSdk.Success ? maxSdk.Groups[1].Value : null,
                File = manifestPath
            });
        }

        return entries;
    }

    public static string? ReadPlistString(IFileSystem files, string plistPath, string key)
    {
        var text = files.ReadAllText(plistPath);
        var match = Regex.Match(
            text,
            $@"<key>\s*{Regex.Escape(key)}\s*</key>\s*<string>([^<]*)</string>",
            RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    public static string? ReadManifestPackage(IFileSystem files, string manifestPath)
    {
        var xml = files.ReadAllText(manifestPath);
        var match = Regex.Match(xml, @"(?:package|android:package)\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    public static bool IsPlaceholderId(string? value) =>
        string.IsNullOrWhiteSpace(value)
        || value.StartsWith("com.companyname.", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> ReadPlistKeys(IFileSystem files, string plistPath)
    {
        var text = files.ReadAllText(plistPath);
        return Regex.Matches(text, @"<key>\s*([^<]+?)\s*</key>", RegexOptions.IgnoreCase)
            .Select(match => match.Groups[1].Value.Trim())
            .Where(key => key.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public static string SourceBlob(IFileSystem files, ProjectGraph graph) =>
        string.Join('\n', graph.SourceFiles.Select(path =>
        {
            try
            {
                return files.ReadAllText(path);
            }
            catch (IOException)
            {
                return string.Empty;
            }
        }));

    public static bool IsPlatformSource(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/Platforms/", StringComparison.OrdinalIgnoreCase);
    }

    public static int? ReadMinSdk(IFileSystem files, string? manifestPath, ProjectDocument? project)
    {
        if (project?.SupportedOsPlatformVersion is { Length: > 0 } version && int.TryParse(Digits(version), out var fromProject))
        {
            return fromProject;
        }

        if (manifestPath is null || !files.FileExists(manifestPath))
        {
            return null;
        }

        var xml = files.ReadAllText(manifestPath);
        var match = Regex.Match(xml, @"android:minSdkVersion\s*=\s*""(\d+)""", RegexOptions.IgnoreCase);
        return match.Success && int.TryParse(match.Groups[1].Value, out var min) ? min : null;
    }

    public static bool LooksLikeForegroundService(IFileSystem files, string manifestPath)
    {
        var xml = files.ReadAllText(manifestPath);
        return xml.Contains("foregroundServiceType", StringComparison.OrdinalIgnoreCase)
               || xml.Contains("FOREGROUND_SERVICE", StringComparison.OrdinalIgnoreCase);
    }

    static string? Get(IReadOnlyDictionary<string, string> properties, string name) =>
        properties.TryGetValue(name, out var value) && value.Length > 0 ? value : null;

    static string Digits(string value) => new(value.TakeWhile(char.IsDigit).ToArray());

    static bool IsBuildOutput(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return parts.Any(part => part.Equals("bin", StringComparison.OrdinalIgnoreCase) || part.Equals("obj", StringComparison.OrdinalIgnoreCase));
    }
}
