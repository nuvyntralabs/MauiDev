namespace MauiDev;

public sealed class ProcessResult
{
    public required int ExitCode { get; init; }
    public required string StandardOutput { get; init; }
    public required string StandardError { get; init; }
}

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, string? workingDirectory, TimeSpan timeout, CancellationToken cancellationToken);
}

public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    string ReadAllText(string path);
    void WriteAllText(string path, string contents);
    IReadOnlyList<string> GetFiles(string directory, string pattern, bool recursive);
    IReadOnlyList<string> GetDirectories(string directory);
    void DeleteDirectory(string path);
    void CreateDirectory(string path);
}

public interface IHostEnvironment
{
    bool IsWindows { get; }
    bool IsMacOs { get; }
    bool IsLinux { get; }
    string? GetVariable(string name);
    string UserProfile { get; }
    IReadOnlyList<string> PathEntries { get; }
}
