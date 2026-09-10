using MauiDev;
using MauiDev.Tests.Fakes;

namespace MauiDev.Tests;

public sealed class NextCommandTests
{
    [Fact]
    public void PermissionsFindsDuplicateUnusedIosAndAndroid13()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-perm-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0-android;net10.0-ios</TargetFrameworks>
                <UseMaui>true</UseMaui>
              </PropertyGroup>
              <ItemGroup>
                <UsesPermission Include="android.permission.CAMERA" />
                <UsesPermission Include="android.permission.CAMERA" />
                <UsesPermission Include="android.permission.RECORD_AUDIO" />
              </ItemGroup>
            </Project>
            """);
        files.AddFile(Path.Combine(root, "Platforms", "Android", "AndroidManifest.xml"),
            """
            <manifest xmlns:android="http://schemas.android.com/apk/res/android">
              <uses-permission android:name="android.permission.POST_NOTIFICATIONS" />
            </manifest>
            """);
        files.AddFile(Path.Combine(root, "Platforms", "iOS", "Info.plist"),
            """
            <plist><dict>
              <key>NSPhotoLibraryUsageDescription</key><string>Photos</string>
            </dict></plist>
            """);
        files.AddFile(Path.Combine(root, "MainPage.xaml.cs"), "Geolocation.GetLocationAsync(); Camera.CaptureAsync();");

        var report = new PermissionsEngine().Run(TestContextFactory.Create(root, files, new FakeProcessRunner()));
        var ids = report.Results[0].Diagnostics.Select(item => item.Id).ToArray();
        Assert.Contains("MD020", ids);
        Assert.Contains("MD021", ids);
        Assert.Contains("MD022", ids);
        Assert.Contains("MD023", ids);
        Assert.Contains("MD024", ids);
        Assert.Equal("permissions", report.Command);
    }

    [Fact]
    public void PermissionsFixDeduplicatesUsesPermissionAndHonorsDryRun()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-perm-fix-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        var csproj = Path.Combine(root, "App.csproj");
        const string xml =
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0-android</TargetFrameworks>
                <UseMaui>true</UseMaui>
              </PropertyGroup>
              <ItemGroup>
                <UsesPermission Include="android.permission.CAMERA" />
                <UsesPermission Include="android.permission.CAMERA" />
              </ItemGroup>
            </Project>
            """;
        files.AddFile(csproj, xml);
        files.AddFile(Path.Combine(root, "Platforms", "Android", "AndroidManifest.xml"),
            """
            <manifest xmlns:android="http://schemas.android.com/apk/res/android">
              <uses-permission android:name="android.permission.CAMERA" />
              <uses-permission android:name="android.permission.CAMERA" />
            </manifest>
            """);

        var dry = new PermissionsEngine().Run(TestContextFactory.Create(root, files, new FakeProcessRunner(), fix: true, dryRun: true));
        Assert.Contains(dry.Fixes, fix => fix.Message.Contains("Would", StringComparison.Ordinal));
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(files.ReadAllText(csproj), "UsesPermission").Count);

        var applied = new PermissionsEngine().Run(TestContextFactory.Create(root, files, new FakeProcessRunner(), fix: true));
        Assert.Contains(applied.Fixes, fix => fix.Applied);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(files.ReadAllText(csproj), "UsesPermission"));
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(files.ReadAllText(Path.Combine(root, "Platforms", "Android", "AndroidManifest.xml")), "uses-permission", System.Text.RegularExpressions.RegexOptions.IgnoreCase));
    }

    [Fact]
    public void PlatformFindsMissingFoldersGuardsAndLowMinOs()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-plat-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0-android;net10.0-ios;net10.0-maccatalyst</TargetFrameworks>
                <UseMaui>true</UseMaui>
                <SupportedOSPlatformVersion>14</SupportedOSPlatformVersion>
              </PropertyGroup>
            </Project>
            """);
        files.AddFile(Path.Combine(root, "MainPage.xaml.cs"), "var x = Android.App.Application.Context;");

        var report = new PlatformEngine().Run(TestContextFactory.Create(root, files, new FakeProcessRunner()));
        var ids = report.Results[0].Diagnostics.Select(item => item.Id).ToArray();
        Assert.Contains("MD050", ids);
        Assert.Contains("MD051", ids);
        Assert.Contains("MD052", ids);
        Assert.Contains("MD053", ids);
        Assert.Contains("MD054", ids);
    }

    [Fact]
    public void PlatformPassesWhenFoldersExist()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-plat-ok-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, "App.csproj"), TestContextFactory.GoodCsproj);
        files.AddFile(Path.Combine(root, "Platforms", "Android", "AndroidManifest.xml"), "<manifest/>");
        files.AddFile(Path.Combine(root, "Platforms", "iOS", "Info.plist"), "<plist/>");

        var report = new PlatformEngine().Run(TestContextFactory.Create(root, files, new FakeProcessRunner()));
        Assert.Equal(CheckStatus.Pass, report.WorstStatus);
    }

    [Fact]
    public void SigningFindsMissingKeystoreAndEntitlements()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-sign-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0-android;net10.0-ios</TargetFrameworks>
                <UseMaui>true</UseMaui>
                <AndroidSigningKeyStore>missing.keystore</AndroidSigningKeyStore>
                <CodesignEntitlements>Entitlements.plist</CodesignEntitlements>
              </PropertyGroup>
            </Project>
            """);
        files.AddFile(Path.Combine(root, "Platforms", "iOS", "Info.plist"), "<plist/>");

        var report = new SigningEngine().Run(TestContextFactory.Create(root, files, new FakeProcessRunner()));
        var ids = report.Results[0].Diagnostics.Select(item => item.Id).ToArray();
        Assert.Contains("MD070", ids);
        Assert.Contains("MD071", ids);
    }

    [Fact]
    public async Task WorkloadFailsWhenMissingAndPrintsInstallCommand()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-wl-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        var process = new FakeProcessRunner();
        process.On("dotnet", "workload list", new ProcessResult { ExitCode = 0, StandardOutput = "wasm-tools\n", StandardError = string.Empty });
        files.AddFile(Path.Combine(root, "App.csproj"), TestContextFactory.GoodCsproj);

        var report = await new WorkloadEngine().RunAsync(TestContextFactory.Create(root, files, process), CancellationToken.None);
        Assert.Equal(CheckStatus.Fail, report.WorstStatus);
        Assert.Contains(report.Recommendations, text => text.Contains("dotnet workload install", StringComparison.Ordinal));
    }

    [Fact]
    public void VersionDetectsDriftAndAlignsOnDryRun()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-ver-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, "src", "Lib", "Lib.csproj"), Packable("1.0.1"));
        files.AddFile(Path.Combine(root, "src", "Cli", "Cli.csproj"), Packable("1.0.1"));
        files.AddFile(Path.Combine(root, "Directory.Build.props"), "<Project><PropertyGroup><Version>1.0.0</Version></PropertyGroup></Project>");
        files.AddFile(Path.Combine(root, "extension", "vscode", "package.json"), """{"version":"1.0.0"}""");

        var report = new VersionEngine().Run(TestContextFactory.Create(root, files, new FakeProcessRunner()), new VersionRequest());
        Assert.Contains(report.Results[0].Diagnostics, item => item.Id == "MD410");
        Assert.Contains(report.Results[0].Diagnostics, item => item.Id == "MD411");

        var dry = new VersionEngine().Run(
            TestContextFactory.Create(root, files, new FakeProcessRunner(), dryRun: true),
            new VersionRequest { Align = true });
        Assert.Contains(dry.Fixes, fix => fix.Message.Contains("Would set Version to 1.0.1", StringComparison.Ordinal));
        Assert.Contains("<Version>1.0.0</Version>", files.ReadAllText(Path.Combine(root, "Directory.Build.props")), StringComparison.Ordinal);

        var written = new VersionEngine().Run(TestContextFactory.Create(root, files, new FakeProcessRunner()), new VersionRequest { Bump = "minor" });
        Assert.Contains(written.Fixes, fix => fix.Applied);
        Assert.Contains("<Version>1.1.0</Version>", files.ReadAllText(Path.Combine(root, "src", "Lib", "Lib.csproj")), StringComparison.Ordinal);
        Assert.Contains("\"version\": \"1.1.0\"", files.ReadAllText(Path.Combine(root, "extension", "vscode", "package.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void DependenciesFindsDriftCpmAndToolPackageReference()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-dep-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, "Directory.Packages.props"), "<Project></Project>");
        files.AddFile(Path.Combine(root, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0-android</TargetFrameworks>
                <UseMaui>true</UseMaui>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.Maui.Controls" Version="10.0.0" />
                <PackageReference Include="Microsoft.Maui.Controls" Version="10.0.1" />
                <PackageReference Include="Plugin.Maui.MauiDev.Cli" Version="1.0.1" />
              </ItemGroup>
            </Project>
            """);
        files.AddFile(Path.Combine(root, "Lib", "Lib.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.Maui.Controls" Version="9.0.0" />
              </ItemGroup>
            </Project>
            """);

        var report = new DependenciesEngine().Run(TestContextFactory.Create(root, files, new FakeProcessRunner()));
        var ids = report.Results[0].Diagnostics.Select(item => item.Id).ToArray();
        Assert.Contains("MD500", ids);
        Assert.Contains("MD501", ids);
        Assert.Contains("MD502", ids);
        Assert.Contains("MD503", ids);
    }

    [Fact]
    public void IconsFindsMissingAdaptiveAndMarketingSize()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-ico-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFrameworks>net10.0-android;net10.0-ios</TargetFrameworks>
                <UseMaui>true</UseMaui>
              </PropertyGroup>
              <ItemGroup>
                <MauiIcon Include="Resources/AppIcon/appicon.svg" />
              </ItemGroup>
            </Project>
            """);
        files.AddFile(Path.Combine(root, "Platforms", "Android", "Resources", "mipmap-anydpi-v26", "ic_launcher.xml"), "<adaptive-icon><foreground android:drawable=\"@mipmap/fg\" /></adaptive-icon>");
        files.AddFile(Path.Combine(root, "Platforms", "iOS", "Assets.xcassets", "AppIcon.appiconset", "Contents.json"), """{"images":[{"size":"60x60","idiom":"iphone"}]}""");

        var report = new IconsEngine().Run(TestContextFactory.Create(root, files, new FakeProcessRunner()));
        var ids = report.Results[0].Diagnostics.Select(item => item.Id).ToArray();
        Assert.Contains("MD211", ids);
        Assert.Contains("MD212", ids);
        Assert.Contains("MD213", ids);
    }

    [Fact]
    public void IgnoreFiltersDiagnosticIds()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-ign-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, "App.csproj"), TestContextFactory.GoodCsproj);
        files.AddFile(Path.Combine(root, "MainPage.xaml.cs"), "var x = Android.App.Application.Context;");
        var context = TestContextFactory.Create(root, files, new FakeProcessRunner());
        context = new CheckContext
        {
            RootPath = context.RootPath,
            Files = context.Files,
            Process = context.Process,
            Host = context.Host,
            Timeout = context.Timeout,
            IgnoreIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MD052" }
        };

        var report = new PlatformEngine().Run(context);
        Assert.DoesNotContain(report.Results[0].Diagnostics, item => item.Id == "MD052");
    }

    static string Packable(string version) =>
        $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <IsPackable>true</IsPackable>
            <Version>{version}</Version>
          </PropertyGroup>
        </Project>
        """;
}
