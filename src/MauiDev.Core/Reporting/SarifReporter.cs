using System.Text.Json;

namespace MauiDev;

public sealed class SarifReporter : IReporter
{
    public ReportFormat Format => ReportFormat.Sarif;

    public void Write(DoctorReport report, TextWriter writer)
    {
        var results = report.Results
            .SelectMany(result => result.Diagnostics.DefaultIfEmpty(new DiagnosticFinding
            {
                Id = result.CheckId,
                Message = result.Detail ?? result.Title,
                Severity = result.Status,
                File = null
            }))
            .Where(item => item.Severity is CheckStatus.Warn or CheckStatus.Fail)
            .Select(item => new
            {
                ruleId = item.Id,
                level = item.Severity == CheckStatus.Fail ? "error" : "warning",
                message = new { text = item.Message },
                locations = item.File is null
                    ? Array.Empty<object>()
                    : new object[]
                    {
                        new
                        {
                            physicalLocation = new
                            {
                                artifactLocation = new { uri = item.File },
                                region = new { startLine = item.Line ?? 1 }
                            }
                        }
                    }
            });

        var sarif = new
        {
            version = "2.1.0",
            schema = "https://json.schemastore.org/sarif-2.1.0.json",
            runs = new[]
            {
                new
                {
                    tool = new { driver = new { name = "maui-dev", informationUri = "https://github.com/nuvyntralabs/MauiDev" } },
                    results
                }
            }
        };

        writer.WriteLine(JsonSerializer.Serialize(sarif, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
    }
}
