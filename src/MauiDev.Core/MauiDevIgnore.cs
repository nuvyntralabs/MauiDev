using System.Text.Json;

namespace MauiDev;

public static class MauiDevIgnore
{
    public const string FileName = ".maui-dev.json";

    public static IReadOnlySet<string> Load(IFileSystem files, string rootPath)
    {
        var path = Path.Combine(rootPath, FileName);
        if (!files.FileExists(path))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var document = JsonDocument.Parse(files.ReadAllText(path));
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (document.RootElement.TryGetProperty("ignore", out var ignore) && ignore.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in ignore.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } id)
                    {
                        ids.Add(id);
                    }
                }
            }

            return ids;
        }
        catch (JsonException)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static void Write(IFileSystem files, string rootPath, IEnumerable<string> ignoreIds)
    {
        var path = Path.Combine(rootPath, FileName);
        var payload = JsonSerializer.Serialize(
            new { ignore = ignoreIds.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(id => id, StringComparer.OrdinalIgnoreCase).ToArray() },
            new JsonSerializerOptions { WriteIndented = true });
        files.WriteAllText(path, payload + Environment.NewLine);
    }
}
