using MauiDev;
using MauiDev.Tests.Fakes;

namespace MauiDev.Tests;

public sealed class AnalyzeAndResourceTests
{
    [Fact]
    public void AnalyzeFindsHttpClientAndEventRetention()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-analyze-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, "App.csproj"), TestContextFactory.GoodCsproj);
        files.AddFile(Path.Combine(root, "MainPage.xaml.cs"),
            """
            public partial class MainPage
            {
                public MainPage()
                {
                    Clicked += OnClicked;
                    _ = Task.Run(() => new HttpClient());
                    MainThread.BeginInvokeOnMainThread(() => { });
                    MainThread.BeginInvokeOnMainThread(() => { });
                    MainThread.BeginInvokeOnMainThread(() => { });
                    var x = Android.App.Application.Context;
                }
            }
            """);

        var report = new AnalyzeEngine().Run(TestContextFactory.Create(root, files, new FakeProcessRunner()));
        var ids = report.Results[0].Diagnostics.Select(item => item.Id).ToArray();
        Assert.Contains("MD100", ids);
        Assert.Contains("MD101", ids);
        Assert.Contains("MD102", ids);
        Assert.Contains("MD103", ids);
        Assert.Contains("MD105", ids);
        Assert.Equal("analyze", report.Command);
    }

    [Fact]
    public void ResourcesDetectsMissingAndUnused()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-res-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, "App.csproj"), TestContextFactory.GoodCsproj);
        files.AddFile(Path.Combine(root, "MainPage.xaml"), "<ContentPage></ContentPage>");

        var report = new ResourceEngine().Run(TestContextFactory.Create(root, files, new FakeProcessRunner()));
        Assert.Contains(report.Results[0].Diagnostics, item => item.Id == "MD201");
        Assert.Contains(report.Results[0].Diagnostics, item => item.Id == "MD202");
        Assert.Equal(CheckStatus.Fail, report.WorstStatus);
    }

    [Fact]
    public void CleanDeletesBinAndObj()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-clean-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, "src", "bin", "Debug", "a.dll"), "bin");
        files.AddFile(Path.Combine(root, "src", "obj", "Debug", "a.cache"), "obj");
        files.AddDirectory(Path.Combine(root, "src", "bin"));
        files.AddDirectory(Path.Combine(root, "src", "obj"));

        var report = new CleanEngine().Run(TestContextFactory.Create(root, files, new FakeProcessRunner()), new CleanRequest());
        Assert.False(files.DirectoryExists(Path.Combine(root, "src", "bin")));
        Assert.False(files.DirectoryExists(Path.Combine(root, "src", "obj")));
        Assert.Equal("clean", report.Command);
    }

    [Fact]
    public void PackageValidateRequiresVersionAlignment()
    {
        var root = Path.Combine(Path.GetTempPath(), "maui-dev-pack-" + Guid.NewGuid().ToString("N"));
        var files = new FakeFileSystem();
        files.AddFile(Path.Combine(root, "src", "Lib", "Lib.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <IsPackable>true</IsPackable>
                <Version>1.0.0</Version>
                <PackageReadmeFile>README.md</PackageReadmeFile>
                <PackageIcon>icon.png</PackageIcon>
                <PackageLicenseExpression>MIT</PackageLicenseExpression>
              </PropertyGroup>
            </Project>
            """);
        files.AddFile(Path.Combine(root, "src", "Cli", "Cli.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <PackAsTool>true</PackAsTool>
                <ToolCommandName>maui-dev</ToolCommandName>
                <IsPackable>true</IsPackable>
                <Version>1.0.1</Version>
                <PackageReadmeFile>README.md</PackageReadmeFile>
                <PackageIcon>icon.png</PackageIcon>
                <PackageLicenseExpression>MIT</PackageLicenseExpression>
              </PropertyGroup>
            </Project>
            """);

        var report = new PackageValidator().Validate(TestContextFactory.Create(root, files, new FakeProcessRunner()));
        Assert.Contains(report.Results[0].Diagnostics, item => item.Id == "MD401");
        Assert.Equal(CheckStatus.Fail, report.WorstStatus);
    }

    [Fact]
    public void JsonReporterIncludesCamelCaseSchema()
    {
        var report = new DoctorReport
        {
            Command = "doctor",
            Results =
            [
                CheckResult.Pass(new MauiDev.Checks.DotNetSdkCheck(), "10.0.102")
            ]
        };
        using var writer = new StringWriter();
        new JsonReporter().Write(report, writer);
        var json = writer.ToString();
        Assert.Contains("\"command\": \"doctor\"", json, StringComparison.Ordinal);
        Assert.Contains("\"status\": \"pass\"", json, StringComparison.Ordinal);
        Assert.Contains("\"id\": \"dotnet-sdk\"", json, StringComparison.Ordinal);
    }
}
