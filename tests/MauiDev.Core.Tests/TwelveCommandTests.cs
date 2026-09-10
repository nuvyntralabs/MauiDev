using MauiDev;
using MauiDev.Tests.Fakes;

namespace MauiDev.Tests;

public sealed class TwelveCommandTests
{
    [Fact]
    public void PublishFlagsPlaceholderIdsMissingPrivacyAndPackIssues()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-pub-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0-android;net10.0-ios</TargetFrameworks>
                <UseMaui>true</UseMaui>
                <ApplicationId>com.companyname.app</ApplicationId>
                <IsPackable>true</IsPackable>
                <Version>1.0.0</Version>
              </PropertyGroup>
            </Project>
            """);
        files.AddFile(Path.Combine(root, "Lib", "Lib.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <IsPackable>true</IsPackable>
                <Version>1.0.1</Version>
              </PropertyGroup>
            </Project>
            """);

        var report = new PublishEngine().Run(TestContextFactory.Create(root, files, new FakeProcessRunner()));
        var ids = report.Results.SelectMany(result => result.Diagnostics).Select(item => item.Id).ToArray();
        Assert.Equal("publish", report.Command);
        Assert.Contains("MD800", ids);
        Assert.Contains("MD801", ids);
        Assert.Contains("MD802", ids);
        Assert.Contains("MD803", ids);
        Assert.Equal(CheckStatus.Fail, report.WorstStatus);
    }

    [Fact]
    public void PublishPassesRealIdsWithPrivacyManifest()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-pub-ok-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0-android;net10.0-ios</TargetFrameworks>
                <UseMaui>true</UseMaui>
                <ApplicationId>com.nuvyntra.app</ApplicationId>
                <IsPackable>false</IsPackable>
              </PropertyGroup>
            </Project>
            """);
        files.AddFile(Path.Combine(root, "Platforms", "iOS", "PrivacyInfo.xcprivacy"), "<plist/>");
        files.AddFile(Path.Combine(root, "Platforms", "iOS", "Info.plist"),
            "<plist><dict><key>CFBundleIdentifier</key><string>com.nuvyntra.app</string></dict></plist>");

        var report = new PublishEngine().Run(TestContextFactory.Create(root, files, new FakeProcessRunner()));
        Assert.Equal(CheckStatus.Pass, report.WorstStatus);
        Assert.DoesNotContain(report.Results.SelectMany(result => result.Diagnostics), item => item.Id is "MD800" or "MD801" or "MD802" or "MD803");
    }

    [Fact]
    public void MigrateFindsLegacyTfmsXamarinAndFormsInit()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-mig-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net8.0-android;net9.0-ios</TargetFrameworks>
                <UseMaui>true</UseMaui>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Xamarin.Forms" Version="5.0.0" />
                <PackageReference Include="Xamarin.Essentials" Version="1.8.1" />
              </ItemGroup>
            </Project>
            """);
        files.AddFile(Path.Combine(root, "Platforms", "Android", "MainActivity.cs"),
            "Forms.Init(this, savedInstanceState); LoadApplication(new App());");

        var report = new MigrateEngine().Run(TestContextFactory.Create(root, files, new FakeProcessRunner()));
        var ids = report.Results[0].Diagnostics.Select(item => item.Id).ToArray();
        Assert.Equal("migrate", report.Command);
        Assert.Contains("MD600", ids);
        Assert.Contains("MD601", ids);
        Assert.Contains("MD602", ids);
        Assert.Equal(CheckStatus.Fail, report.WorstStatus);
    }

    [Fact]
    public void TelemetryWarnsWhenNoReporterAndWhenAppCenterRemains()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-tel-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, "App.csproj"), TestContextFactory.GoodCsproj);
        files.AddFile(Path.Combine(root, "MauiProgram.cs"), "builder.UseMauiApp<App>();");

        var missing = new TelemetryEngine().Run(TestContextFactory.Create(root, files, new FakeProcessRunner()));
        Assert.Contains(missing.Results[0].Diagnostics, item => item.Id == "MD701");

        files.AddFile(Path.Combine(root, "Crashes.cs"), "AppCenter.Start(typeof(Crashes));");
        var retired = new TelemetryEngine().Run(TestContextFactory.Create(root, files, new FakeProcessRunner()));
        Assert.Contains(retired.Results[0].Diagnostics, item => item.Id == "MD702");
        Assert.DoesNotContain(retired.Results[0].Diagnostics, item => item.Id == "MD701");
    }

    [Fact]
    public void TelemetryDetectsDiagnosticsAndCollectsNothing()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-tel-ok-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0-android;net10.0-ios</TargetFrameworks>
                <UseMaui>true</UseMaui>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Plugin.Maui.Diagnostics" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);

        var report = new TelemetryEngine().Run(TestContextFactory.Create(root, files, new FakeProcessRunner()));
        Assert.Contains(report.Results[0].Diagnostics, item => item.Id == "MD700" && item.Message.Contains("Diagnostics", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Results[0].Diagnostics, item => item.Id == "MD701");
        Assert.Equal(CheckStatus.Pass, report.WorstStatus);
        Assert.Contains("collects nothing", report.Results[0].Detail ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BenchmarkFailsWhenMauiPerfMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-bench-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, "App.csproj"), TestContextFactory.GoodCsproj);

        var report = await new BenchmarkEngine().RunAsync(
            TestContextFactory.Create(root, files, new FakeProcessRunner()),
            new BenchmarkRequest(),
            CancellationToken.None);
        Assert.Equal("benchmark", report.Command);
        Assert.Contains(report.Results[0].Diagnostics, item => item.Id == "MD900");
        Assert.Equal(CheckStatus.Fail, report.WorstStatus);
        Assert.Contains(report.Recommendations, text => text.Contains("Plugin.Maui.Performance.Cli", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BenchmarkForwardsArgsToMauiPerf()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-bench-ok-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        var process = new FakeProcessRunner();
        var host = new FakeHostEnvironment();
        var tool = Path.Combine(host.UserProfile, ".dotnet", "tools", "maui-perf");
        files.AddFile(tool, "");
        files.AddFile(Path.Combine(root, "App.csproj"), TestContextFactory.GoodCsproj);
        process.On(tool, "startup", new ProcessResult { ExitCode = 0, StandardOutput = "trace ok", StandardError = string.Empty });

        var report = await new BenchmarkEngine().RunAsync(
            TestContextFactory.Create(root, files, process, host),
            new BenchmarkRequest { Arguments = ["startup", "-f", "android"] },
            CancellationToken.None);
        Assert.Equal(CheckStatus.Pass, report.WorstStatus);
        Assert.Contains("trace ok", report.Results[0].Detail ?? "", StringComparison.Ordinal);
    }
}
