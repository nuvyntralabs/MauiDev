using MauiDev;

namespace MauiDev.Tests.Fakes;

public sealed class FakeProcessRunner : IProcessRunner
{
    readonly Dictionary<string, ProcessResult> _responses = new(StringComparer.OrdinalIgnoreCase);

    public void On(string fileName, ProcessResult result) => _responses[fileName] = result;

    public void On(string fileName, string argumentsContain, ProcessResult result) =>
        _responses[fileName + " " + argumentsContain] = result;

    public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var joined = string.Join(' ', arguments);
        foreach (var pair in _responses)
        {
            if (pair.Key.Contains(' ', StringComparison.Ordinal) &&
                pair.Key.StartsWith(fileName, StringComparison.OrdinalIgnoreCase) &&
                joined.Contains(pair.Key[fileName.Length..].Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(pair.Value);
            }
        }

        if (_responses.TryGetValue(fileName, out var result))
        {
            return Task.FromResult(result);
        }

        return Task.FromResult(new ProcessResult
        {
            ExitCode = 127,
            StandardOutput = string.Empty,
            StandardError = $"No fake for {fileName} {joined}"
        });
    }
}
