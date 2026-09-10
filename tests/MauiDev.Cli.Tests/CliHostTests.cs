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
        Assert.Contains("maui-dev doctor", stdout.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownCommandIsUsageError()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = await CliHost.RunAsync(["migrate"], stdout, stderr);
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
