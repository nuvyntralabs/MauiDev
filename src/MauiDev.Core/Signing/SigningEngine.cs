namespace MauiDev;

public sealed class SigningEngine
{
    public DoctorReport Run(CheckContext context)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        var android = graph.Projects.Where(project => project.HasAndroid).ToArray();
        var ios = graph.Projects.Where(project => project.HasIos).ToArray();
        if (android.Length == 0 && ios.Length == 0)
        {
            return CommandReport.Create("signing", "signing", "Signing", CheckCategory.Project, [], "OK", "No Android or iOS target frameworks.");
        }

        var diagnostics = context.Filter(Analyze(context, graph, android, ios));
        return CommandReport.Create(
            "signing",
            "signing",
            "Signing",
            CheckCategory.Project,
            diagnostics,
            "Configured or using platform defaults");
    }

    static IReadOnlyList<DiagnosticFinding> Analyze(
        CheckContext context,
        ProjectGraph graph,
        IReadOnlyList<ProjectDocument> android,
        IReadOnlyList<ProjectDocument> ios)
    {
        var diagnostics = new List<DiagnosticFinding>();
        foreach (var project in android.Where(item => !item.HasAndroidSigning))
        {
            diagnostics.Add(new DiagnosticFinding
            {
                Id = "MD072",
                Message = $"{Path.GetFileName(project.Path)} has no AndroidSigning* properties (debug keystore is fine locally).",
                File = project.Path,
                Severity = CheckStatus.Warn,
                Why = "Store builds need a keystore from CI secrets.",
                NextStep = "Set AndroidSigning* from CI secrets. maui-dev never writes keystores."
            });
        }

        foreach (var project in android)
        {
            if (string.IsNullOrWhiteSpace(project.AndroidSigningKeyStore))
            {
                continue;
            }

            var full = ResolveBeside(project.Path, project.AndroidSigningKeyStore);
            if (!context.Files.FileExists(full))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD070",
                    Message = $"AndroidSigningKeyStore '{project.AndroidSigningKeyStore}' is missing.",
                    File = project.Path,
                    Severity = CheckStatus.Fail,
                    Why = "The csproj points at a keystore that is not on disk.",
                    NextStep = "Place the keystore on the CI runner or remove the property for local debug."
                });
            }
        }

        if (ios.Count > 0 && graph.Plists.Count == 0)
        {
            diagnostics.Add(new DiagnosticFinding
            {
                Id = "MD072",
                Message = "No Info.plist / entitlements were found for iOS targets.",
                Severity = CheckStatus.Warn,
                Why = "iOS store signing needs a plist and a provisioning profile in CI.",
                NextStep = "Add Info.plist. maui-dev never writes provisioning profiles."
            });
        }

        foreach (var project in ios)
        {
            if (string.IsNullOrWhiteSpace(project.CodesignEntitlements))
            {
                continue;
            }

            var full = ResolveBeside(project.Path, project.CodesignEntitlements);
            if (!context.Files.FileExists(full))
            {
                diagnostics.Add(new DiagnosticFinding
                {
                    Id = "MD071",
                    Message = $"CodesignEntitlements '{project.CodesignEntitlements}' is missing.",
                    File = project.Path,
                    Severity = CheckStatus.Fail,
                    Why = "The csproj references an entitlements file that is not on disk.",
                    NextStep = "Add the entitlements file or remove the property."
                });
            }
        }

        if (diagnostics.Count == 0)
        {
            diagnostics.Add(new DiagnosticFinding
            {
                Id = "MD072",
                Message = "Store signing belongs in CI secrets.",
                Severity = CheckStatus.Pass,
                NextStep = "maui-dev never writes keystores or provisioning profiles."
            });
        }

        return diagnostics.Where(item => item.Severity is not CheckStatus.Pass).ToArray();
    }

    static string ResolveBeside(string projectPath, string relative)
    {
        var projectDir = Path.GetDirectoryName(projectPath) ?? string.Empty;
        return Path.GetFullPath(Path.Combine(projectDir, relative.Replace('\\', Path.DirectorySeparatorChar)));
    }
}
