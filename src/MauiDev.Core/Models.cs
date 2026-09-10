namespace MauiDev;

public enum CheckCategory
{
    Machine,
    Project,
    Resources,
    Packaging
}

public enum CheckStatus
{
    Pass,
    Warn,
    Fail,
    Skip
}

public enum ReportFormat
{
    Human,
    Json,
    Sarif
}

public static class ExitCodes
{
    public const int Success = 0;
    public const int Issues = 1;
    public const int Usage = 2;
}

public sealed class DiagnosticFinding
{
    public required string Id { get; init; }
    public required string Message { get; init; }
    public CheckStatus Severity { get; init; } = CheckStatus.Warn;
    public string? File { get; init; }
    public int? Line { get; init; }
    public string? Why { get; init; }
    public string? NextStep { get; init; }
}

public sealed class CheckResult
{
    public required string CheckId { get; init; }
    public required string Title { get; init; }
    public required CheckCategory Category { get; init; }
    public required CheckStatus Status { get; init; }
    public string? Detail { get; init; }
    public string? Recommendation { get; init; }
    public IReadOnlyList<DiagnosticFinding> Diagnostics { get; init; } = [];
    public bool CanFix { get; init; }

    public static CheckResult Pass(ICheck check, string? detail = null) =>
        new()
        {
            CheckId = check.Id,
            Title = check.Title,
            Category = check.Category,
            Status = CheckStatus.Pass,
            Detail = detail
        };

    public static CheckResult Warn(ICheck check, string detail, string? recommendation = null, IReadOnlyList<DiagnosticFinding>? diagnostics = null, bool canFix = false) =>
        new()
        {
            CheckId = check.Id,
            Title = check.Title,
            Category = check.Category,
            Status = CheckStatus.Warn,
            Detail = detail,
            Recommendation = recommendation,
            Diagnostics = diagnostics ?? [],
            CanFix = canFix
        };

    public static CheckResult Fail(ICheck check, string detail, string? recommendation = null, IReadOnlyList<DiagnosticFinding>? diagnostics = null, bool canFix = false) =>
        new()
        {
            CheckId = check.Id,
            Title = check.Title,
            Category = check.Category,
            Status = CheckStatus.Fail,
            Detail = detail,
            Recommendation = recommendation,
            Diagnostics = diagnostics ?? [],
            CanFix = canFix
        };

    public static CheckResult Skip(ICheck check, string reason) =>
        new()
        {
            CheckId = check.Id,
            Title = check.Title,
            Category = check.Category,
            Status = CheckStatus.Skip,
            Detail = reason
        };
}

public sealed class FixResult
{
    public required bool Applied { get; init; }
    public required string Message { get; init; }
    public IReadOnlyList<string> Changes { get; init; } = [];
}

public sealed class DoctorReport
{
    public required string Command { get; init; }
    public IReadOnlyList<CheckResult> Results { get; init; } = [];
    public IReadOnlyList<string> Recommendations { get; init; } = [];
    public IReadOnlyList<FixResult> Fixes { get; init; } = [];

    public CheckStatus WorstStatus
    {
        get
        {
            if (Results.Any(result => result.Status == CheckStatus.Fail))
            {
                return CheckStatus.Fail;
            }

            if (Results.Any(result => result.Status == CheckStatus.Warn))
            {
                return CheckStatus.Warn;
            }

            return CheckStatus.Pass;
        }
    }

    public int ExitCode(bool warnAsError)
    {
        return WorstStatus switch
        {
            CheckStatus.Fail => ExitCodes.Issues,
            CheckStatus.Warn when warnAsError => ExitCodes.Issues,
            _ => ExitCodes.Success
        };
    }
}

public interface ICheck
{
    string Id { get; }
    CheckCategory Category { get; }
    string Title { get; }
    bool CanFix { get; }
    Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken);
    Task<FixResult> FixAsync(CheckContext context, CheckResult result, CancellationToken cancellationToken);
}

public abstract class CheckBase : ICheck
{
    public abstract string Id { get; }
    public abstract CheckCategory Category { get; }
    public abstract string Title { get; }
    public virtual bool CanFix => false;

    public abstract Task<CheckResult> RunAsync(CheckContext context, CancellationToken cancellationToken);

    public virtual Task<FixResult> FixAsync(CheckContext context, CheckResult result, CancellationToken cancellationToken)
    {
        return Task.FromResult(new FixResult
        {
            Applied = false,
            Message = $"{Title} does not support --fix."
        });
    }

    protected CheckResult Pass(string? detail = null) => CheckResult.Pass(this, detail);

    protected CheckResult Warn(string detail, string? recommendation = null, IReadOnlyList<DiagnosticFinding>? diagnostics = null, bool canFix = false) =>
        CheckResult.Warn(this, detail, recommendation, diagnostics, canFix);

    protected CheckResult Fail(string detail, string? recommendation = null, IReadOnlyList<DiagnosticFinding>? diagnostics = null, bool canFix = false) =>
        CheckResult.Fail(this, detail, recommendation, diagnostics, canFix);

    protected CheckResult Skip(string reason) => CheckResult.Skip(this, reason);
}

public interface IReporter
{
    ReportFormat Format { get; }
    void Write(DoctorReport report, TextWriter writer);
}
