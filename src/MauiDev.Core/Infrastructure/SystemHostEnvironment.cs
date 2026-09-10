namespace MauiDev;

public sealed class SystemHostEnvironment : IHostEnvironment
{
    public bool IsWindows => OperatingSystem.IsWindows();
    public bool IsMacOs => OperatingSystem.IsMacOS();
    public bool IsLinux => OperatingSystem.IsLinux();

    public string UserProfile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public IReadOnlyList<string> PathEntries =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public string? GetVariable(string name) => Environment.GetEnvironmentVariable(name);
}
