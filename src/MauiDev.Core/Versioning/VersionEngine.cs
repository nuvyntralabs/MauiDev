using System.Text.Json;
using System.Text.RegularExpressions;

namespace MauiDev;

public sealed class VersionRequest
{
    public bool Align { get; init; }
    public string? Bump { get; init; }
}

public sealed class VersionEngine
{
    static readonly Regex Element = new(@"<(Version|PackageVersion)>([^<]*)</\1>", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public DoctorReport Run(CheckContext context, VersionRequest request)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        var packable = graph.Projects.Where(project => project.IsPackable && !IsTest(project)).ToArray();
        var props = context.Files.GetFiles(context.RootPath, "Directory.Build.props", recursive: true)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var packageJson = Path.Combine(context.RootPath, "extension", "vscode", "package.json");
        var hasPackageJson = context.Files.FileExists(packageJson);

        var diagnostics = new List<DiagnosticFinding>();
        var versions = new List<(string Source, string Version)>();
        foreach (var project in packable)
        {
            var version = project.PackageVersion ?? project.Version;
            if (string.IsNullOrWhiteSpace(version))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD412",
                    Message = $"{Path.GetFileName(project.Path)} is missing Version / PackageVersion.",
                    File = project.Path,
                    Severity = CheckStatus.Fail,
                    NextStep = "Set Version or run maui-dev version --align."
                });
                continue;
            }

            versions.Add((project.Path, version));
        }

        foreach (var path in props)
        {
            var match = Element.Match(context.Files.ReadAllText(path));
            if (match.Success && match.Groups[2].Value.Trim().Length > 0)
            {
                versions.Add((path, match.Groups[2].Value.Trim()));
            }
        }

        string? jsonVersion = null;
        if (hasPackageJson)
        {
            jsonVersion = ReadJsonVersion(context.Files.ReadAllText(packageJson));
            if (jsonVersion is { Length: > 0 })
            {
                versions.Add((packageJson, jsonVersion));
            }
        }

        var distinct = versions.Select(item => item.Version).Distinct(StringComparer.Ordinal).ToArray();
        if (distinct.Length > 1)
        {
            diagnostics.Add(new DiagnosticFinding
            {
                Id = "MD410",
                Message = "Packable project versions do not match: " + string.Join(", ", distinct),
                Severity = CheckStatus.Fail,
                Why = "CLI, Directory.Build.props, and the VS Code extension must share one Version.",
                NextStep = "maui-dev version --align --dry-run"
            });
        }

        if (hasPackageJson && jsonVersion is { Length: > 0 })
        {
            var cliVersions = packable
                .Select(project => project.PackageVersion ?? project.Version)
                .Where(version => !string.IsNullOrWhiteSpace(version))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (cliVersions.Length == 1 && !string.Equals(cliVersions[0], jsonVersion, StringComparison.Ordinal))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD411",
                    Message = $"CLI Version {cliVersions[0]} does not match extension/vscode/package.json {jsonVersion}.",
                    File = packageJson,
                    Severity = CheckStatus.Fail,
                    NextStep = "maui-dev version --align"
                });
            }
        }

        diagnostics = context.Filter(diagnostics).ToList();
        var write = request.Align || !string.IsNullOrWhiteSpace(request.Bump);
        List<FixResult>? fixes = null;
        if (write)
        {
            var target = ChooseTarget(versions.Select(item => item.Version), request.Bump);
            if (target is null)
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD412",
                    Message = "No Version exists to align or bump.",
                    Severity = CheckStatus.Fail,
                    NextStep = "Set Version on a packable project first."
                });
            }
            else
            {
                fixes = [WriteVersions(context, packable, props, hasPackageJson ? packageJson : null, target)];
                diagnostics = diagnostics.Where(item => item.Id is not ("MD410" or "MD411")).ToList();
            }
        }

        return CommandReport.Create(
            "version",
            "version",
            "Version alignment",
            CheckCategory.Packaging,
            diagnostics,
            distinct.Length == 0 ? "No packable versions" : distinct[0],
            packable.Length == 0 && props.Length == 0 ? "No packable src projects." : null,
            canFix: false,
            fixes: fixes);
    }

    static FixResult WriteVersions(
        CheckContext context,
        IReadOnlyList<ProjectDocument> packable,
        IReadOnlyList<string> props,
        string? packageJson,
        string version)
    {
        var changes = new List<string>();
        foreach (var project in packable)
        {
            var updated = ReplaceOrInsert(project.Xml, version);
            if (!string.Equals(updated, project.Xml, StringComparison.Ordinal))
            {
                changes.Add(project.Path);
                if (!context.DryRun)
                {
                    context.Files.WriteAllText(project.Path, updated);
                }
            }
        }

        foreach (var path in props)
        {
            var xml = context.Files.ReadAllText(path);
            var updated = ReplaceOrInsert(xml, version);
            if (!string.Equals(updated, xml, StringComparison.Ordinal))
            {
                changes.Add(path);
                if (!context.DryRun)
                {
                    context.Files.WriteAllText(path, updated);
                }
            }
        }

        if (packageJson is not null)
        {
            var json = context.Files.ReadAllText(packageJson);
            var updated = Regex.Replace(json, @"""version""\s*:\s*""[^""]*""", $"\"version\": \"{version}\"", RegexOptions.IgnoreCase);
            if (!string.Equals(updated, json, StringComparison.Ordinal))
            {
                changes.Add(packageJson);
                if (!context.DryRun)
                {
                    context.Files.WriteAllText(packageJson, updated);
                }
            }
        }

        return new FixResult
        {
            Applied = changes.Count > 0 && !context.DryRun,
            Message = context.DryRun
                ? $"Would set Version to {version} in {changes.Count} file(s)."
                : $"Set Version to {version} in {changes.Count} file(s).",
            Changes = changes
        };
    }

    static string? ChooseTarget(IEnumerable<string> versions, string? bump)
    {
        var parsed = versions
            .Select(value => Version.TryParse(value, out var version) ? version : null)
            .Where(version => version is not null)
            .Select(version => version!)
            .ToArray();
        if (parsed.Length == 0)
        {
            return null;
        }

        var highest = parsed.Max()!;
        if (string.IsNullOrWhiteSpace(bump))
        {
            return highest.ToString();
        }

        return bump.Trim().ToLowerInvariant() switch
        {
            "major" => new Version(highest.Major + 1, 0, 0).ToString(),
            "minor" => new Version(highest.Major, highest.Minor + 1, 0).ToString(),
            "patch" => new Version(highest.Major, highest.Minor, Math.Max(highest.Build, 0) + 1).ToString(),
            _ => highest.ToString()
        };
    }

    static string ReplaceOrInsert(string xml, string version)
    {
        if (Element.IsMatch(xml))
        {
            return Element.Replace(xml, match => $"<{match.Groups[1].Value}>{version}</{match.Groups[1].Value}>");
        }

        var match = Regex.Match(xml, @"<PropertyGroup\b[^>]*>", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return xml;
        }

        return string.Concat(xml.AsSpan(0, match.Index + match.Length), $"{Environment.NewLine}    <Version>{version}</Version>", xml.AsSpan(match.Index + match.Length));
    }

    static string? ReadJsonVersion(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("version", out var version) ? version.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static bool IsTest(ProjectDocument project) =>
        project.Path.Contains(".Tests", StringComparison.OrdinalIgnoreCase) ||
        project.Path.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
}
