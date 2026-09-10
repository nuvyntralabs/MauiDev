using System.Text.RegularExpressions;

namespace MauiDev.Checks;

public sealed class JdkCheck : CheckBase
{
    public override string Id => "jdk";
    public override CheckCategory Category => CheckCategory.Machine;
    public override string Title => "JDK";

    public override async Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        var graph = ProjectGraph.Load(context.Files, context.RootPath);
        if (graph.Projects.Count > 0 && graph.Projects.All(project => !project.HasAndroid))
        {
            return Skip( "No Android target framework in this tree.");
        }

        var javaHome = context.Host.GetVariable("JAVA_HOME");
        var java = !string.IsNullOrWhiteSpace(javaHome)
            ? Path.Combine(javaHome, "bin", context.Host.IsWindows ? "java.exe" : "java")
            : "java";

        var result = await context.Process.RunAsync(java, ["-version"], context.RootPath, context.Timeout, cancellationToken).ConfigureAwait(false);
        var text = result.StandardError + result.StandardOutput;
        var match = Regex.Match(text, @"(\d+)\.(\d+)\.(\d+)");
        if (result.ExitCode == 127 || !match.Success)
        {
            return Fail( "A JDK was not found.", "Install Microsoft OpenJDK 17 or 21 and set JAVA_HOME.");
        }

        var major = int.Parse(match.Groups[1].Value);
        var version = $"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}";
        if (major is 17 or 21)
        {
            return Pass( version);
        }

        return Warn( version, "MAUI currently expects JDK 17 or 21.");
    }
}
