using System.Text.Json;

namespace Aeziol.App.Services;

public sealed class AppLogger : IDisposable
{
    private const long MaximumLogBytes = 1024 * 1024;
    private const int RetainedFiles = 5;
    private readonly string _directory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AppLogger(string directory)
    {
        _directory = Path.GetFullPath(directory);
        ScrubExistingLogs();
    }

    public string CurrentLogPath => Path.Combine(_directory, "aeziol.log.jsonl");

    public async Task WriteAsync(
        string level,
        string eventName,
        object? properties = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_directory);
            RotateIfNeeded();
            var entry = JsonSerializer.Serialize(new
            {
                timestamp = DateTimeOffset.UtcNow,
                level = LogSanitizer.SanitizeText(level),
                eventName = LogSanitizer.SanitizeText(eventName),
                properties = LogSanitizer.Sanitize(properties),
            });
            await File.AppendAllTextAsync(CurrentLogPath, entry + Environment.NewLine, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void ScrubExistingLogs()
    {
        if (!Directory.Exists(_directory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(_directory, "aeziol*.log.jsonl", SearchOption.TopDirectoryOnly))
        {
            var temporaryPath = path + ".sanitizing";
            try
            {
                var sanitizedLines = File.ReadLines(path).Select(LogSanitizer.SanitizeJsonLine).ToArray();
                File.WriteAllLines(temporaryPath, sanitizedLines);
                File.Move(temporaryPath, path, overwrite: true);
            }
            catch (IOException)
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
            catch (UnauthorizedAccessException)
            {
                TryDeleteTemporaryFile(temporaryPath);
            }
        }
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void RotateIfNeeded()
    {
        var file = new FileInfo(CurrentLogPath);
        if (!file.Exists || file.Length < MaximumLogBytes)
        {
            return;
        }

        File.Delete(Path.Combine(_directory, $"aeziol.{RetainedFiles}.log.jsonl"));
        for (var index = RetainedFiles - 1; index >= 1; index--)
        {
            var source = Path.Combine(_directory, $"aeziol.{index}.log.jsonl");
            var destination = Path.Combine(_directory, $"aeziol.{index + 1}.log.jsonl");
            if (File.Exists(source))
            {
                File.Move(source, destination, overwrite: true);
            }
        }

        File.Move(CurrentLogPath, Path.Combine(_directory, "aeziol.1.log.jsonl"), overwrite: true);
    }

    public void Dispose()
    {
        _gate.Dispose();
        GC.SuppressFinalize(this);
    }
}
