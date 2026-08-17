using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aeziol.App.Settings;

public sealed class UnsupportedSettingsSchemaException(int schemaVersion)
    : IOException($"Unsupported Aeziol settings schema: {schemaVersion}.")
{
    public int SchemaVersion { get; } = schemaVersion;
}

public sealed class JsonAppSettingsStore(string path)
{
    private const long MaximumSettingsBytes = 256 * 1024;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
    private readonly string _path = Path.GetFullPath(path);
    private readonly string _backupPath = Path.GetFullPath(path) + ".backup";

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return new AppSettings();
        }

        LoadResult result;
        try
        {
            result = await LoadFileAsync(_path, cancellationToken).ConfigureAwait(false);
        }
        catch (UnsupportedSettingsSchemaException)
        {
            throw;
        }
        catch (Exception primaryException) when (primaryException is not OperationCanceledException)
        {
            if (!File.Exists(_backupPath))
            {
                throw;
            }

            try
            {
                result = await LoadFileAsync(_backupPath, cancellationToken).ConfigureAwait(false);
                RestorePrimaryFromBackup();
            }
            catch (UnsupportedSettingsSchemaException)
            {
                throw;
            }
            catch (Exception backupException) when (backupException is not OperationCanceledException)
            {
                throw new InvalidDataException(
                    "Aeziol could not load either the settings file or its backup.",
                    new AggregateException(primaryException, backupException));
            }
        }

        if (result.RequiresSave)
        {
            await SaveAsync(result.Settings, cancellationToken).ConfigureAwait(false);
        }

        return result.Settings;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var normalized = settings.SchemaVersion == AppSettings.CurrentSchemaVersion
            ? settings
            : settings with { SchemaVersion = AppSettings.CurrentSchemaVersion };
        var directory = Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("The settings path must have a parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, normalized, Options, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }

            if (File.Exists(_path))
            {
                File.Replace(temporaryPath, _path, _backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporaryPath, _path);
            }
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static async Task<LoadResult> LoadFileAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var file = new FileInfo(path);
        if (file.Length is <= 0 or > MaximumSettingsBytes)
        {
            throw new InvalidDataException("The Aeziol settings file has an invalid size.");
        }

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        var schemaVersion = document.RootElement.TryGetProperty("schemaVersion", out var schemaElement)
            && schemaElement.TryGetInt32(out var parsedSchema)
                ? parsedSchema
                : 0;
        if (schemaVersion < 0)
        {
            throw new InvalidDataException($"Invalid Aeziol settings schema: {schemaVersion}.");
        }

        if (schemaVersion > AppSettings.CurrentSchemaVersion)
        {
            throw new UnsupportedSettingsSchemaException(schemaVersion);
        }

        var settings = document.RootElement.Deserialize<AppSettings>(Options)
            ?? throw new InvalidDataException("The Aeziol settings file is empty.");
        var requiresSave = schemaVersion < AppSettings.CurrentSchemaVersion;
        if (requiresSave)
        {
            settings = settings with { SchemaVersion = AppSettings.CurrentSchemaVersion };
        }

        if (document.RootElement.TryGetProperty("ambientMusicMuted", out var legacyMuted)
            && legacyMuted.ValueKind is JsonValueKind.True)
        {
            settings = settings with { AmbientMusicEnabled = false };
            requiresSave = true;
        }

        return new LoadResult(settings, requiresSave);
    }

    private void RestorePrimaryFromBackup()
    {
        var corruptPath = _path + ".corrupt-" + DateTimeOffset.UtcNow.ToString(
            "yyyyMMddHHmmssfff",
            System.Globalization.CultureInfo.InvariantCulture);
        var primaryWasMoved = false;
        try
        {
            if (File.Exists(_path))
            {
                File.Move(_path, corruptPath);
                primaryWasMoved = true;
            }

            File.Copy(_backupPath, _path, overwrite: false);
        }
        catch
        {
            if (primaryWasMoved && !File.Exists(_path) && File.Exists(corruptPath))
            {
                File.Move(corruptPath, _path);
            }

            throw;
        }
    }

    private sealed record LoadResult(AppSettings Settings, bool RequiresSave);
}
