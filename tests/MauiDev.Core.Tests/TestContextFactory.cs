using MauiDev;
using MauiDev.Tests.Fakes;

namespace MauiDev.Tests;

public static class TestContextFactory
{
    public static CheckContext Create(
        string root,
        FakeFileSystem files,
        FakeProcessRunner process,
        FakeHostEnvironment? host = null,
        bool fix = false,
        bool dryRun = false,
        bool warnAsError = false)
    {
        return new CheckContext
        {
            RootPath = root,
            Files = files,
            Process = process,
            Host = host ?? new FakeHostEnvironment(),
            Fix = fix,
            DryRun = dryRun,
            WarnAsError = warnAsError,
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    public static void SeedHealthyMachine(FakeProcessRunner process, FakeHostEnvironment host, FakeFileSystem files, string androidHome)
    {
        process.On("dotnet", "--list-sdks", new ProcessResult { ExitCode = 0, StandardOutput = "10.0.102 [/.dotnet/sdk]\n", StandardError = string.Empty });
        process.On("dotnet", "workload list", new ProcessResult { ExitCode = 0, StandardOutput = "maui-android     10.0.100\n", StandardError = string.Empty });
        var java = Path.Combine("/opt/java21", "bin", "java");
        process.On("java", new ProcessResult { ExitCode = 0, StandardOutput = string.Empty, StandardError = "openjdk version \"21.0.8\" 2025-01-01\n" });
        process.On(java, new ProcessResult { ExitCode = 0, StandardOutput = string.Empty, StandardError = "openjdk version \"21.0.8\" 2025-01-01\n" });
        host.Set("ANDROID_HOME", androidHome);
        host.Set("JAVA_HOME", "/opt/java21");
        files.AddDirectory(androidHome);
        files.AddDirectory(Path.Combine(androidHome, "platforms"));
        files.AddDirectory(Path.Combine(androidHome, "platforms", "android-36"));
        files.AddFile(java, string.Empty);
    }

    public const string GoodCsproj =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFrameworks>net10.0-android;net10.0-ios</TargetFrameworks>
            <UseMaui>true</UseMaui>
            <SupportedOSPlatformVersion>24</SupportedOSPlatformVersion>
            <ApplicationId>com.example.app</ApplicationId>
          </PropertyGroup>
          <ItemGroup>
            <MauiImage Include="Resources/Images/logo.png" />
            <MauiSplashScreen Include="Resources/Splash/splash.svg" />
          </ItemGroup>
        </Project>
        """;

    public const string BadCsproj =
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFrameworks>net10.0-android;net10.0-ios</TargetFrameworks>
            <SupportedOSPlatformVersion>21</SupportedOSPlatformVersion>
          </PropertyGroup>
          <ItemGroup>
            <MauiSplashScreen Include="Resources/Splash/splash.svg" />
            <MauiSplashScreen Include="Resources/Splash/splash.svg" />
            <MauiImage Include="Resources/Images/logo.png" />
            <UsesPermission Include="android.permission.CAMERA" />
          </ItemGroup>
        </Project>
        """;
}
