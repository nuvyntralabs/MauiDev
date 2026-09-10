using System.Text.Json;
using System.Text.Json.Serialization;

namespace MauiDev;

public sealed class JsonReporter : IReporter
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public ReportFormat Format => ReportFormat.Json;

    public void Write(DoctorReport report, TextWriter writer)
    {
        var payload = new ReportDto
        {
            Command = report.Command,
            ExitCode = report.ExitCode(warnAsError: false),
            Results = report.Results.Select(result => new ResultDto
            {
                Id = result.CheckId,
                Title = result.Title,
                Category = result.Category,
                Status = result.Status,
                Detail = result.Detail,
                Recommendation = result.Recommendation,
                CanFix = result.CanFix,
                Diagnostics = result.Diagnostics.Select(item => new DiagnosticDto
                {
                    Id = item.Id,
                    Message = item.Message,
                    File = item.File,
                    Line = item.Line,
                    Severity = item.Severity,
                    Why = item.Why,
                    NextStep = item.NextStep
                }).ToArray()
            }).ToArray(),
            Recommendations = report.Recommendations.ToArray(),
            Fixes = report.Fixes.Select(fix => new FixDto
            {
                Applied = fix.Applied,
                Message = fix.Message,
                Changes = fix.Changes.ToArray()
            }).ToArray()
        };

        writer.WriteLine(JsonSerializer.Serialize(payload, Options));
    }

    public sealed class ReportDto
    {
        public required string Command { get; init; }
        public int ExitCode { get; init; }
        public ResultDto[] Results { get; init; } = [];
        public string[] Recommendations { get; init; } = [];
        public FixDto[] Fixes { get; init; } = [];
    }

    public sealed class ResultDto
    {
        public required string Id { get; init; }
        public required string Title { get; init; }
        public CheckCategory Category { get; init; }
        public CheckStatus Status { get; init; }
        public string? Detail { get; init; }
        public string? Recommendation { get; init; }
        public bool CanFix { get; init; }
        public DiagnosticDto[] Diagnostics { get; init; } = [];
    }

    public sealed class DiagnosticDto
    {
        public required string Id { get; init; }
        public required string Message { get; init; }
        public string? File { get; init; }
        public int? Line { get; init; }
        public CheckStatus Severity { get; init; }
        public string? Why { get; init; }
        public string? NextStep { get; init; }
    }

    public sealed class FixDto
    {
        public bool Applied { get; init; }
        public string? Message { get; init; }
        public string[] Changes { get; init; } = [];
    }
}
