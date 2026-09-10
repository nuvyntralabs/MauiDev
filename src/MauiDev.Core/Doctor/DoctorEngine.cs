namespace MauiDev;

public sealed class DoctorEngine
{
    readonly IReadOnlyList<ICheck> _checks;

    public DoctorEngine(IReadOnlyList<ICheck>? checks = null)
    {
        _checks = checks ?? CheckCatalog.DoctorChecks;
    }

    public async Task<DoctorReport> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        var results = new List<CheckResult>();
        var fixes = new List<FixResult>();
        foreach (var check in _checks)
        {
            if (context.IgnoreIds.Contains(check.Id))
            {
                results.Add(CheckResult.Skip(check, "Ignored by .maui-dev.json"));
                continue;
            }

            var result = await check.RunAsync(context, cancellationToken).ConfigureAwait(false);
            if (result.Diagnostics.Count > 0)
            {
                var filtered = context.Filter(result.Diagnostics);
                if (filtered.Count != result.Diagnostics.Count)
                {
                    result = new CheckResult
                    {
                        CheckId = result.CheckId,
                        Title = result.Title,
                        Category = result.Category,
                        Status = filtered.Any(item => item.Severity == CheckStatus.Fail)
                            ? CheckStatus.Fail
                            : filtered.Any(item => item.Severity == CheckStatus.Warn)
                                ? CheckStatus.Warn
                                : result.Status == CheckStatus.Skip ? CheckStatus.Skip : CheckStatus.Pass,
                        Detail = result.Detail,
                        Recommendation = result.Recommendation,
                        Diagnostics = filtered,
                        CanFix = result.CanFix
                    };
                }
            }

            results.Add(result);

            if (context.Fix && result.CanFix && check.CanFix && result.Status is CheckStatus.Fail or CheckStatus.Warn)
            {
                fixes.Add(await check.FixAsync(context, result, cancellationToken).ConfigureAwait(false));
            }
        }

        var recommendations = results
            .Where(result => result.Status is CheckStatus.Fail or CheckStatus.Warn)
            .Select(result => result.Recommendation ?? result.Detail ?? result.Title)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new DoctorReport
        {
            Command = "doctor",
            Results = results,
            Recommendations = recommendations,
            Fixes = fixes
        };
    }
}
