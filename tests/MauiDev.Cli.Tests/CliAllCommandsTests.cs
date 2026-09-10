using MauiDev;
using MauiDev.Cli;

namespace MauiDev.Cli.Tests;

public sealed class CliAllCommandsTests
{
    static readonly string[] Commands =
    [
        "doctor", "analyze", "resources", "permissions", "platform", "signing",
        "workload", "version", "dependencies", "icons", "publish", "migrate",
        "telemetry", "benchmark", "clean", "package"
    ];

    [Fact]
    public async Task UsageLists12Commands()
    {
        var stdout = new StringWriter();
        await CliHost.RunAsync([], stdout, new StringWriter());
        var usage = stdout.ToString();
        Assert.Contains("maui-dev publish", usage, StringComparison.Ordinal);
        Assert.Contains("maui-dev migrate", usage, StringComparison.Ordinal);
        Assert.Contains("maui-dev telemetry", usage, StringComparison.Ordinal);
        Assert.Contains("maui-dev benchmark", usage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EveryCommandProducesJsonAndIsNotAUsageError()
    {
        using var workspace = new TempMauiWorkspace();
        foreach (var command in Commands)
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var code = await CliHost.RunAsync([command, "--path", workspace.Root, "--format", "json"], stdout, stderr);
            Assert.True(code != ExitCodes.Usage, command + " was treated as unknown: " + stderr);
            Assert.Contains("\"command\": \"" + command + "\"", stdout.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task PublishValidateWorksAndPushIsRejected()
    {
        using var workspace = new TempMauiWorkspace();
        var stdout = new StringWriter();
        var code = await CliHost.RunAsync(["publish", "--validate", "--path", workspace.Root, "--format", "json"], stdout, new StringWriter());
        Assert.NotEqual(ExitCodes.Usage, code);
        Assert.Contains("\"command\": \"publish\"", stdout.ToString(), StringComparison.Ordinal);

        var stderr = new StringWriter();
        var push = await CliHost.RunAsync(["publish", "--push", "--path", workspace.Root], new StringWriter(), stderr);
        Assert.Equal(ExitCodes.Usage, push);
        Assert.Contains("--push is not supported", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MigrateAndTelemetryAndBenchmarkAreKnown()
    {
        using var workspace = new TempMauiWorkspace();
        foreach (var command in new[] { "migrate", "telemetry", "benchmark" })
        {
            var stderr = new StringWriter();
            var stdout = new StringWriter();
            var code = await CliHost.RunAsync([command, "--path", workspace.Root, "--ci"], stdout, stderr);
            Assert.NotEqual(ExitCodes.Usage, code);
            Assert.DoesNotContain("Unknown command", stderr.ToString(), StringComparison.Ordinal);
            Assert.Contains("\"command\": \"" + command + "\"", stdout.ToString(), StringComparison.Ordinal);
        }
    }

    sealed class TempMauiWorkspace : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "maui-dev-cli-" + Guid.NewGuid().ToString("N"));

        public TempMauiWorkspace()
        {
            Directory.CreateDirectory(Path.Combine(Root, "Platforms", "Android"));
            Directory.CreateDirectory(Path.Combine(Root, "Platforms", "iOS"));
            Directory.CreateDirectory(Path.Combine(Root, "Resources", "Images"));
            Directory.CreateDirectory(Path.Combine(Root, "Resources", "Splash"));
            File.WriteAllText(Path.Combine(Root, "App.csproj"),
                """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFrameworks>net10.0-android;net10.0-ios</TargetFrameworks>
                    <UseMaui>true</UseMaui>
                    <SupportedOSPlatformVersion>24</SupportedOSPlatformVersion>
                    <ApplicationId>com.nuvyntra.cli.test</ApplicationId>
                    <IsPackable>false</IsPackable>
                  </PropertyGroup>
                  <ItemGroup>
                    <MauiImage Include="Resources/Images/logo.png" />
                    <MauiSplashScreen Include="Resources/Splash/splash.svg" />
                    <PackageReference Include="Plugin.Maui.Diagnostics" Version="1.0.0" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(Root, "Resources", "Images", "logo.png"), "png");
            File.WriteAllText(Path.Combine(Root, "Resources", "Splash", "splash.svg"), "<svg/>");
            File.WriteAllText(Path.Combine(Root, "Platforms", "Android", "AndroidManifest.xml"),
                """<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.nuvyntra.cli.test"></manifest>""");
            File.WriteAllText(Path.Combine(Root, "Platforms", "iOS", "Info.plist"),
                "<plist><dict><key>CFBundleIdentifier</key><string>com.nuvyntra.cli.test</string></dict></plist>");
            File.WriteAllText(Path.Combine(Root, "Platforms", "iOS", "PrivacyInfo.xcprivacy"), "<plist/>");
            File.WriteAllText(Path.Combine(Root, "MauiProgram.cs"), "builder.UseMauiDiagnostics();");
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
