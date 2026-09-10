using MauiDev.Checks;

namespace MauiDev;

public sealed class WorkloadEngine
{
    public async Task<DoctorReport> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        var workload = await new MauiWorkloadCheck().RunAsync(context, cancellationToken).ConfigureAwait(false);
        var cli = await new MauiCliCheck().RunAsync(context, cancellationToken).ConfigureAwait(false);
        var results = new[] { workload, cli };
        var recommendations = results
            .Select(result => result.Recommendation)
            .OfType<string>()
            .Where(text => text.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (workload.Status == CheckStatus.Fail && recommendations.Length == 0)
        {
            recommendations = ["dotnet workload install maui"];
        }

        return new DoctorReport
        {
            Command = "workload",
            Results = results,
            Recommendations = recommendations
        };
    }
}
