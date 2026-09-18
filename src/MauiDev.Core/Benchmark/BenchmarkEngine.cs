namespace MauiDev;

public sealed class BenchmarkRequest
{
    public IReadOnlyList<string> Arguments { get; init; } = [];
}

public sealed class BenchmarkEngine
{
    public async Task<DoctorReport> RunAsync(CheckContext context, BenchmarkRequest request, CancellationToken cancellationToken)
    {
        var tool = ToolResolver.Find(context, "maui-perf");
        if (tool is null)
        {
            var missing = new DiagnosticFinding
            {
                Id = "MD900",
                Message = "maui-perf was not found on PATH or ~/.dotnet/tools.",
                Severity = CheckStatus.Fail,
                Why = "benchmark shells to Plugin.Maui.Performance.Cli. It does not reimplement maui profile.",
                NextStep = "dotnet tool install -g Plugin.Maui.Performance.Cli --source https://api.nuget.org/v3/index.json"
            };
            return CommandReport.Create(
                "benchmark",
                "benchmark",
                "maui-perf",
                CheckCategory.Machine,
                context.Filter([missing]),
                "OK");
        }

        var args = request.Arguments.Count > 0 ? request.Arguments : ["--help"];
        var result = await context.Process.RunAsync(tool, args, context.RootPath, context.Timeout, cancellationToken).ConfigureAwait(false);
        var failed = result.ExitCode != 0;
        var diagnostic = new DiagnosticFinding
        {
            Id = "MD900",
            Message = failed
                ? "maui-perf exited " + result.ExitCode
                : "maui-perf " + string.Join(' ', args),
            Severity = failed ? CheckStatus.Fail : CheckStatus.Pass,
            Why = "Android and iOS simulator only — the same constraint as maui-perf.",
            NextStep = failed ? (string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput.Trim() : result.StandardError.Trim()) : null
        };

        return CommandReport.Create(
            "benchmark",
            "benchmark",
            "maui-perf",
            CheckCategory.Machine,
            context.Filter([diagnostic]),
            (result.StandardOutput.Trim().Length > 0 ? result.StandardOutput.Trim() : "maui-perf ran") + " (not a maui-dev profile)");
    }
}
