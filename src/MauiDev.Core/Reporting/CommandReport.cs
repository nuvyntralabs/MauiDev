namespace MauiDev;

public static class CommandReport
{
    public static DoctorReport Create(
        string command,
        string checkId,
        string title,
        CheckCategory category,
        IReadOnlyList<DiagnosticFinding> diagnostics,
        string passDetail,
        string? skipReason = null,
        bool canFix = false,
        IReadOnlyList<FixResult>? fixes = null)
    {
        if (skipReason is not null && diagnostics.Count == 0)
        {
            return new DoctorReport
            {
                Command = command,
                Results =
                [
                    new CheckResult
                    {
                        CheckId = checkId,
                        Title = title,
                        Category = category,
                        Status = CheckStatus.Skip,
                        Detail = skipReason
                    }
                ]
            };
        }

        var actionable = diagnostics.Where(item => item.Severity is CheckStatus.Fail or CheckStatus.Warn).ToArray();
        var status = actionable.Any(item => item.Severity == CheckStatus.Fail)
            ? CheckStatus.Fail
            : actionable.Length > 0 ? CheckStatus.Warn : CheckStatus.Pass;
        var recommendation = actionable.Select(item => item.NextStep).FirstOrDefault(step => !string.IsNullOrWhiteSpace(step));
        return new DoctorReport
        {
            Command = command,
            Results =
            [
                new CheckResult
                {
                    CheckId = checkId,
                    Title = title,
                    Category = category,
                    Status = status,
                    Detail = status == CheckStatus.Pass ? passDetail : actionable.Length + " issue(s)",
                    Recommendation = recommendation,
                    Diagnostics = diagnostics,
                    CanFix = canFix
                }
            ],
            Recommendations = recommendation is null ? [] : [recommendation],
            Fixes = fixes ?? []
        };
    }
}
