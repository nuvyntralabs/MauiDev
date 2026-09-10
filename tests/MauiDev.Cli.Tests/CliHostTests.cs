using MauiDev;
using MauiDev.Cli;

namespace MauiDev.Cli.Tests;

public sealed class CliHostTests
{
    [Fact]
    public async Task NoArgsPrintsUsage()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = await CliHost.RunAsync([], stdout, stderr);
        Assert.Equal(0, code);
        var usage = stdout.ToString();
        Assert.Contains("maui-dev doctor", usage, StringComparison.Ordinal);
        Assert.Contains("maui-dev permissions", usage, StringComparison.Ordinal);
        Assert.Contains("maui-dev version", usage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PermissionsIsAKnownCommand()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = await CliHost.RunAsync(["permissions", "--path", Path.GetTempPath()], stdout, stderr);
        Assert.NotEqual(ExitCodes.Usage, code);
        Assert.DoesNotContain("Unknown command", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task VersionRejectsInvalidBump()
    {
        var stderr = new StringWriter();
        var code = await CliHost.RunAsync(["version", "--bump", "sideways"], new StringWriter(), stderr);
        Assert.Equal(ExitCodes.Usage, code);
        Assert.Contains("patch, minor, or major", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownCommandIsUsageError()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = await CliHost.RunAsync(["not-a-command"], stdout, stderr);
        Assert.Equal(ExitCodes.Usage, code);
        Assert.Contains("Unknown command", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task HelpIsSuccess()
    {
        var stdout = new StringWriter();
        var code = await CliHost.RunAsync(["--help"], stdout, new StringWriter());
        Assert.Equal(0, code);
        Assert.Contains("maui-dev", stdout.ToString(), StringComparison.Ordinal);
    }
}
