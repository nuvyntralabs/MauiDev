using MauiDev;

namespace MauiDev.Tests.Fakes;

public sealed class FakeFileSystem : IFileSystem
{
    readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    public void AddFile(string path, string contents)
    {
        var full = Path.GetFullPath(path);
        _files[full] = contents;
        var directory = Path.GetDirectoryName(full);
        while (!string.IsNullOrEmpty(directory))
        {
            _directories.Add(directory);
            directory = Path.GetDirectoryName(directory);
        }
    }

    public void AddDirectory(string path) => _directories.Add(Path.GetFullPath(path));

    public bool FileExists(string path) => _files.ContainsKey(Path.GetFullPath(path));

    public bool DirectoryExists(string path) => _directories.Contains(Path.GetFullPath(path));

    public string ReadAllText(string path) => _files[Path.GetFullPath(path)];

    public void WriteAllText(string path, string contents) => AddFile(path, contents);

    public IReadOnlyList<string> GetFiles(string directory, string pattern, bool recursive)
    {
        var root = Path.GetFullPath(directory);
        var extension = pattern.StartsWith("*.", StringComparison.Ordinal) ? pattern[1..] : null;
        return _files.Keys
            .Where(path => path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            .Where(path => recursive || string.Equals(Path.GetDirectoryName(path), root, StringComparison.OrdinalIgnoreCase))
            .Where(path => extension is null
                ? Path.GetFileName(path).Equals(pattern, StringComparison.OrdinalIgnoreCase)
                : path.EndsWith(extension, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(path).Equals(pattern, StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    public IReadOnlyList<string> GetDirectories(string directory)
    {
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
        return _directories
            .Where(path =>
            {
                var parent = Path.GetDirectoryName(path);
                return parent is not null && string.Equals(Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar), root, StringComparison.OrdinalIgnoreCase);
            })
            .ToArray();
    }

    public void DeleteDirectory(string path)
    {
        var root = Path.GetFullPath(path);
        foreach (var file in _files.Keys.Where(key => key.StartsWith(root, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            _files.Remove(file);
        }

        _directories.RemoveWhere(item => item.StartsWith(root, StringComparison.OrdinalIgnoreCase));
    }

    public void CreateDirectory(string path) => AddDirectory(path);
}
