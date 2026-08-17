using Aeziol.App.Settings;
using Aeziol.App.Services;

namespace Aeziol.Tests.App;

public sealed class JsonAppSettingsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Aeziol.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Defaults_UseElgoWithDarkAppearance()
    {
        var settings = new AppSettings();

        Assert.Equal(AeziolTheme.Elgo, settings.Theme);
        Assert.Equal("en", settings.Language);
        Assert.False(settings.EnhanceContrast);
        Assert.False(settings.ReduceAnimations);
        Assert.False(settings.AmbientMusicEnabled);
        Assert.Equal(8, settings.AmbientMusicVolumePercent);
        Assert.True(settings.PauseAmbientMusicWhenUnfocused);
        Assert.True(settings.UseHardwareAcceleration);
        Assert.Equal(UpdateChannel.Stable, settings.UpdateChannel);
    }

    [Fact]
    public void DefaultPaths_KeepSettingsOutsideTheInstallationDirectory()
    {
        var paths = AppPaths.CreateDefault();
        var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        Assert.StartsWith(localApplicationData, paths.SettingsFile, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("Aeziol", "settings.json"), paths.SettingsFile, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(paths.SettingsFile + ".backup", paths.SettingsBackupFile);
        Assert.False(paths.SettingsFile.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SaveAndLoadAsync_RoundTripsPreferences()
    {
        var path = Path.Combine(_root, "settings.json");
        var store = new JsonAppSettingsStore(path);
        var expected = new AppSettings
        {
            FirstRunCompleted = true,
            Language = "ar",
            Theme = AeziolTheme.Yuna,
            EnhanceContrast = true,
            ReduceAnimations = true,
            AmbientMusicEnabled = false,
            AmbientMusicVolumePercent = 12,
            PauseAmbientMusicWhenUnfocused = false,
            UseHardwareAcceleration = false,
            UpdateChannel = UpdateChannel.Beta,
            DiscordExecutablePath = @"C:\Tools\Discord\Discord.exe",
            DiscordExecutableSearchCompleted = true,
            TargetEndpointId = "endpoint-1",
            ExcludedEndpointIds = new HashSet<string>(["endpoint-2"], StringComparer.OrdinalIgnoreCase),
        };

        await store.SaveAsync(expected, TestContext.Current.CancellationToken);
        var actual = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(expected.Language, actual.Language);
        Assert.Equal(expected.Theme, actual.Theme);
        Assert.Equal(expected.EnhanceContrast, actual.EnhanceContrast);
        Assert.Equal(expected.ReduceAnimations, actual.ReduceAnimations);
        Assert.Equal(expected.AmbientMusicEnabled, actual.AmbientMusicEnabled);
        Assert.Equal(expected.AmbientMusicVolumePercent, actual.AmbientMusicVolumePercent);
        Assert.Equal(expected.PauseAmbientMusicWhenUnfocused, actual.PauseAmbientMusicWhenUnfocused);
        Assert.Equal(expected.UseHardwareAcceleration, actual.UseHardwareAcceleration);
        Assert.Equal(expected.UpdateChannel, actual.UpdateChannel);
        Assert.Equal(expected.DiscordExecutablePath, actual.DiscordExecutablePath);
        Assert.True(actual.DiscordExecutableSearchCompleted);
        Assert.Equal(expected.TargetEndpointId, actual.TargetEndpointId);
        Assert.Contains("endpoint-2", actual.ExcludedEndpointIds);
    }

    [Fact]
    public async Task LoadAsync_IgnoresLegacyAppearanceAndRegister()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "settings.json");
        await File.WriteAllTextAsync(
            path,
            """
            {
              "schemaVersion": 1,
              "theme": "elgo",
              "themeMode": "light",
              "register": "fantasy",
              "enhanceContrast": false
            }
            """,
            TestContext.Current.CancellationToken);

        var settings = await new JsonAppSettingsStore(path).LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AeziolTheme.Elgo, settings.Theme);
        Assert.False(settings.EnhanceContrast);
    }

    [Fact]
    public async Task LoadAsync_MigratesTheLegacyMutedStateToDisabled()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "settings.json");
        await File.WriteAllTextAsync(
            path,
            """
            {
              "schemaVersion": 1,
              "ambientMusicEnabled": true,
              "ambientMusicMuted": true,
              "ambientMusicVolumePercent": 37
            }
            """,
            TestContext.Current.CancellationToken);

        var settings = await new JsonAppSettingsStore(path).LoadAsync(TestContext.Current.CancellationToken);

        Assert.False(settings.AmbientMusicEnabled);
        Assert.Equal(37, settings.AmbientMusicVolumePercent);
    }

    [Fact]
    public async Task SaveAsync_KeepsThePreviousSettingsGenerationAsBackup()
    {
        var path = Path.Combine(_root, "settings.json");
        var store = new JsonAppSettingsStore(path);
        await store.SaveAsync(
            new AppSettings { Language = "fr", Theme = AeziolTheme.Cherry },
            TestContext.Current.CancellationToken);
        await store.SaveAsync(
            new AppSettings { Language = "ar", Theme = AeziolTheme.Yuna },
            TestContext.Current.CancellationToken);

        var current = await store.LoadAsync(TestContext.Current.CancellationToken);
        var backup = await new JsonAppSettingsStore(path + ".backup")
            .LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal("ar", current.Language);
        Assert.Equal(AeziolTheme.Yuna, current.Theme);
        Assert.Equal("fr", backup.Language);
        Assert.Equal(AeziolTheme.Cherry, backup.Theme);
    }

    [Fact]
    public async Task LoadAsync_RecoversTheBackupWithoutDeletingTheCorruptFile()
    {
        var path = Path.Combine(_root, "settings.json");
        var store = new JsonAppSettingsStore(path);
        await store.SaveAsync(
            new AppSettings { Language = "fr", TargetEndpointId = "speakers" },
            TestContext.Current.CancellationToken);
        await store.SaveAsync(
            new AppSettings { Language = "ar", TargetEndpointId = "headset" },
            TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(path, "{ definitely-not-json", TestContext.Current.CancellationToken);

        var recovered = await store.LoadAsync(TestContext.Current.CancellationToken);
        var reloaded = await store.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal("fr", recovered.Language);
        Assert.Equal("speakers", recovered.TargetEndpointId);
        Assert.Equal(recovered.Language, reloaded.Language);
        Assert.Equal(recovered.TargetEndpointId, reloaded.TargetEndpointId);
        Assert.Equal(recovered.Theme, reloaded.Theme);
        Assert.Single(Directory.GetFiles(_root, "settings.json.corrupt-*"));
    }

    [Fact]
    public async Task LoadAsync_MigratesAnOlderSchemaAndPreservesKnownValues()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "settings.json");
        await File.WriteAllTextAsync(
            path,
            """
            {
              "schemaVersion": 0,
              "firstRunCompleted": true,
              "language": "fr",
              "theme": "lilith",
              "targetEndpointId": "headset"
            }
            """,
            TestContext.Current.CancellationToken);

        var settings = await new JsonAppSettingsStore(path).LoadAsync(TestContext.Current.CancellationToken);
        var persisted = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
        Assert.True(settings.FirstRunCompleted);
        Assert.Equal("fr", settings.Language);
        Assert.Equal(AeziolTheme.Lilith, settings.Theme);
        Assert.Equal("headset", settings.TargetEndpointId);
        Assert.Contains($"\"schemaVersion\": {AppSettings.CurrentSchemaVersion}", persisted, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsync_RejectsANewerSchemaWithoutOverwritingIt()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "settings.json");
        var futureJson = $$"""
            {
              "schemaVersion": {{AppSettings.CurrentSchemaVersion + 1}},
              "language": "fr",
              "unknownFutureSetting": true
            }
            """;
        await File.WriteAllTextAsync(path, futureJson, TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<UnsupportedSettingsSchemaException>(() =>
            new JsonAppSettingsStore(path).LoadAsync(TestContext.Current.CancellationToken));
        var preserved = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);

        Assert.Equal(AppSettings.CurrentSchemaVersion + 1, exception.SchemaVersion);
        Assert.Equal(futureJson, preserved);
        Assert.False(File.Exists(path + ".backup"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }
}
