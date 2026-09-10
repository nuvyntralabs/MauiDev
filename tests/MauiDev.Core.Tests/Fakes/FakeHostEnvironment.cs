using MauiDev;

namespace MauiDev.Tests.Fakes;

public sealed class FakeHostEnvironment : IHostEnvironment
{
    readonly Dictionary<string, string> _variables = new(StringComparer.OrdinalIgnoreCase);

    public bool IsWindows { get; init; }
    public bool IsMacOs { get; init; }
    public bool IsLinux { get; init; } = true;
    public string UserProfile { get; init; } = "/tmp/maui-dev-home";
    public IReadOnlyList<string> PathEntries { get; init; } = ["/usr/bin"];

    public void Set(string name, string value) => _variables[name] = value;

    public string? GetVariable(string name) => _variables.TryGetValue(name, out var value) ? value : null;
}
