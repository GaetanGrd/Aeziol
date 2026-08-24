using Aeziol.App.Localization;
using Aeziol.App.Settings;

namespace Aeziol.Tests.App;

public sealed class LocalizationServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Aeziol.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void StandardCopy_IsUsedInCurrentLanguage()
    {
        var builtIn = Path.Combine(_root, "built-in");
        var external = Path.Combine(_root, "external");
        Directory.CreateDirectory(builtIn);
        File.WriteAllText(Path.Combine(builtIn, "en.ftl"), "# aeziol-language-format: 1\nlabel-standard = Technical\n");
        File.WriteAllText(Path.Combine(builtIn, "fr.ftl"), "# aeziol-language-format: 1\nlabel-standard = Technique\n");

        var localization = new LocalizationService(builtIn, external, "fr");

        Assert.Equal("Technique", localization.Get("label", WritingRegister.Standard));
    }

    [Fact]
    public void InvalidExternalLanguageFormat_IsRejected()
    {
        var builtIn = Path.Combine(_root, "built-in");
        var external = Path.Combine(_root, "external");
        Directory.CreateDirectory(builtIn);
        Directory.CreateDirectory(external);
        File.WriteAllText(Path.Combine(builtIn, "en.ftl"), "# aeziol-language-format: 1\nlabel-standard = Technical\n");
        File.WriteAllText(Path.Combine(external, "fr.ftl"), "label-standard = Non validé\n");

        Assert.Throws<InvalidDataException>(() =>
        {
            _ = new LocalizationService(builtIn, external, "fr");
        });
    }

    [Fact]
    public void ExternalLanguage_IsDiscoveredWithoutRecompiling()
    {
        var builtIn = Path.Combine(_root, "built-in");
        var external = Path.Combine(_root, "external");
        Directory.CreateDirectory(builtIn);
        Directory.CreateDirectory(external);
        File.WriteAllText(Path.Combine(builtIn, "en.ftl"), "# aeziol-language-format: 1\nlabel-standard = Technical\n");
        File.WriteAllText(Path.Combine(external, "es.ftl"), "# aeziol-language-format: 1\nlabel-standard = Técnico\n");

        var localization = new LocalizationService(builtIn, external, "es");

        Assert.Contains(localization.AvailableLanguages, choice => choice.Code == "es");
        Assert.Equal("Técnico", localization.Get("label", WritingRegister.Standard));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("ar")]
    public void BuiltInLanguages_ContainTheNewInteractionCopy(string language)
    {
        var localization = new LocalizationService(
            Path.Combine(AppContext.BaseDirectory, "Localization"),
            Path.Combine(_root, "external"),
            language);
        var requiredKeys = new[]
        {
            "close-choice-hide-compact",
            "close-choice-quit-compact",
            "close-remember-settings-note",
            "force-restore",
            "nav-discord",
            "nav-passage",
            "nav-coming-soon",
            "nav-coming-soon-help",
            "page-discord-title",
            "discord-section-overview",
            "discord-section-rule",
            "page-rules-title",
            "page-rules-subtitle",
            "rule-route-hint",
            "reset-application",
            "reset-application-title",
            "reset-application-message",
            "reset-application-confirm",
            "ambient-music-enabled",
            "settings-section-general",
            "settings-section-discord",
            "first-run-music-enable",
            "ambient-music-keep-playing-hidden",
            "autostart-open-hidden",
            "update-channel",
            "update-check",
            "update-download",
        };

        foreach (var key in requiredKeys)
        {
            var value = localization.Get(key, WritingRegister.Standard);
            Assert.False(string.IsNullOrWhiteSpace(value));
            Assert.DoesNotContain(key, value, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void InvalidLanguagePath_FallsBackToEnglishWithoutLeavingLanguageDirectories()
    {
        var builtIn = Path.Combine(_root, "built-in");
        var external = Path.Combine(_root, "external");
        Directory.CreateDirectory(builtIn);
        Directory.CreateDirectory(external);
        File.WriteAllText(Path.Combine(builtIn, "en.ftl"), "# aeziol-language-format: 1\nlabel-standard = Technical\n");
        File.WriteAllText(Path.Combine(_root, "outside.ftl"), "# aeziol-language-format: 1\nlabel-standard = Outside\n");

        var localization = new LocalizationService(builtIn, external, "../outside");

        Assert.Equal("en", localization.CurrentLanguage);
        Assert.Equal("Technical", localization.Get("label", WritingRegister.Standard));
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
