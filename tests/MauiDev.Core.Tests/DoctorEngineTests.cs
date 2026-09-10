using MauiDev;
using MauiDev.Tests.Fakes;

namespace MauiDev.Tests;

public sealed class DoctorEngineTests
{
    [Fact]
    public async Task HealthyProjectPassesMachineAndProjectChecks()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-good-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        var process = new FakeProcessRunner();
        var host = new FakeHostEnvironment();
        var androidHome = Path.Combine(root, "android-sdk");
        TestContextFactory.SeedHealthyMachine(process, host, files, androidHome);
        files.AddFile(Path.Combine(root, "App.csproj"), TestContextFactory.GoodCsproj);
        files.AddFile(Path.Combine(root, "Resources", "Images", "logo.png"), "png");
        files.AddFile(Path.Combine(root, "Resources", "Splash", "splash.svg"), "<svg/>");
        files.AddFile(Path.Combine(root, "Platforms", "Android", "AndroidManifest.xml"),
            """<manifest xmlns:android="http://schemas.android.com/apk/res/android"></manifest>""");

        var context = TestContextFactory.Create(root, files, process, host);
        var report = await new DoctorEngine().RunAsync(context, CancellationToken.None);

        Assert.DoesNotContain(report.Results, result => result.Status == CheckStatus.Fail);
        Assert.Contains(report.Results, result => result.CheckId == "dotnet-sdk" && result.Status == CheckStatus.Pass);
        Assert.Contains(report.Results, result => result.CheckId == "use-maui" && result.Status == CheckStatus.Pass);
        Assert.Equal(0, report.ExitCode(warnAsError: false));
    }

    [Fact]
    public async Task BadProjectFailsDuplicatesAndCanDryRunFix()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-bad-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        var process = new FakeProcessRunner();
        var host = new FakeHostEnvironment();
        var androidHome = Path.Combine(root, "android-sdk");
        TestContextFactory.SeedHealthyMachine(process, host, files, androidHome);
        var csproj = Path.Combine(root, "App.csproj");
        files.AddFile(csproj, TestContextFactory.BadCsproj);
        files.AddFile(Path.Combine(root, "Resources", "Images", "logo.png"), "png");
        files.AddFile(Path.Combine(root, "Resources", "Splash", "splash.svg"), "<svg/>");

        var context = TestContextFactory.Create(root, files, process, host, fix: true, dryRun: true);
        var report = await new DoctorEngine().RunAsync(context, CancellationToken.None);

        Assert.Contains(report.Results, result => result.CheckId == "maui-resources" && result.Status == CheckStatus.Fail);
        Assert.Contains(report.Results, result => result.CheckId == "use-maui" && result.Status == CheckStatus.Fail);
        Assert.Contains(report.Results, result => result.CheckId == "android-min-sdk" && result.Status == CheckStatus.Warn);
        Assert.Contains(report.Fixes, fix => fix.Message.Contains("Would", StringComparison.Ordinal));
        Assert.Contains(TestContextFactory.BadCsproj, files.ReadAllText(csproj));
    }

    [Fact]
    public async Task FixWritesUseMauiAndDeduplicatesSplash()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-fix-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        var process = new FakeProcessRunner();
        var host = new FakeHostEnvironment();
        TestContextFactory.SeedHealthyMachine(process, host, files, Path.Combine(root, "android-sdk"));
        var csproj = Path.Combine(root, "App.csproj");
        files.AddFile(csproj, TestContextFactory.BadCsproj);

        var context = TestContextFactory.Create(root, files, process, host, fix: true);
        await new DoctorEngine().RunAsync(context, CancellationToken.None);

        var xml = files.ReadAllText(csproj);
        Assert.Contains("<UseMaui>true</UseMaui>", xml, StringComparison.Ordinal);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(xml, "MauiSplashScreen"));
    }

    [Fact]
    public async Task XcodeAndCocoaPodsSkipOnLinux()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-skip-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        var process = new FakeProcessRunner();
        var host = new FakeHostEnvironment { IsLinux = true, IsMacOs = false };
        TestContextFactory.SeedHealthyMachine(process, host, files, Path.Combine(root, "android-sdk"));
        files.AddFile(Path.Combine(root, "App.csproj"), TestContextFactory.GoodCsproj);

        var report = await new DoctorEngine().RunAsync(TestContextFactory.Create(root, files, process, host), CancellationToken.None);

        Assert.Contains(report.Results, result => result.CheckId == "xcode" && result.Status == CheckStatus.Skip);
        Assert.Contains(report.Results, result => result.CheckId == "cocoapods" && result.Status == CheckStatus.Skip);
    }
}
