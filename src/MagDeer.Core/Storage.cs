using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MagDeer.Core;

public static class JsonStorage
{
    public static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
    public static T Read<T>(string path) where T : new() => File.Exists(path)
        ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) ?? throw new InvalidDataException("Prázdný JSON dokument.") : new();
    public static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Options);
        using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        { file.Write(bytes); file.Flush(true); }
        File.Move(temporary, path, true);
    }
}

public sealed class HistoryStore(string directory, int retentionDays)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateOnly? cleaned;
    public async Task AppendAsync(string endpoint, QueryResult result, ServerStatus status, Recovery? recovery, CancellationToken ct)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(directory);
            var today = DateOnly.FromDateTime(result.At.UtcDateTime);
            if (cleaned != today)
            {
                foreach (var path in Directory.EnumerateFiles(directory, "query-????-??-??.jsonl"))
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    if (DateOnly.TryParseExact(name[6..], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day) &&
                        day < today.AddDays(-retentionDays)) File.Delete(path);
                }
                cleaned = today;
            }
            var row = JsonSerializer.Serialize(new { result.At, Endpoint = endpoint, Status = status.ToString(),
                result.Info, result.RttMs, Failure = result.Failure.ToString(), result.Detail, Recovery = recovery, result.TimeSource, result.SystemAt, result.RequestedAt });
            var file = Path.Combine(directory, $"query-{today:yyyy-MM-dd}.jsonl");
            // Bound disk usage even when a user manually refreshes very frequently.
            if (!File.Exists(file) || new FileInfo(file).Length < 16 * 1024 * 1024)
                await File.AppendAllTextAsync(file, row + Environment.NewLine, Encoding.UTF8, ct).ConfigureAwait(false);
        }
        finally { gate.Release(); }
    }
}
