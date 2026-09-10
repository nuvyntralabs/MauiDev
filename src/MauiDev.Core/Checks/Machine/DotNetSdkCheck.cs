using System.Text.RegularExpressions;

namespace MauiDev.Checks;

public sealed class DotNetSdkCheck : CheckBase
{
    public override string Id => "dotnet-sdk";
    public override CheckCategory Category => CheckCategory.Machine;
    public override string Title => ".NET SDK";

    public override async Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        var result = await context.Process.RunAsync("dotnet", ["--list-sdks"], context.RootPath, context.Timeout, cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            return Fail( "dotnet was not found or --list-sdks failed.", "Install the .NET 10 SDK from https://dot.net");
        }

        var versions = Regex.Matches(result.StandardOutput, @"(\d+\.\d+\.\d+)")
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .ToArray();
        if (versions.Length == 0)
        {
            return Fail( "No .NET SDKs are installed.", "Install the .NET 10 SDK from https://dot.net");
        }

        var net10 = versions.Where(version => version.StartsWith("10.", StringComparison.Ordinal)).ToArray();
        if (net10.Length == 0)
        {
            return Fail( $"Installed SDKs: {string.Join(", ", versions)}.", "Install a 10.x SDK. MauiDev targets net10.0.");
        }

        return Pass( net10[^1]);
    }
}
