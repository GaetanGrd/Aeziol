using Aeziol.App.Localization;

namespace Aeziol.Tests.App;

public sealed class LanguageCardOptionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "Aeziol.Tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void BuiltInLanguagesUseTheirExpectedFlagAssets()
    {
        var localization = CreateLocalization("fr");

        var choices = LanguageCardOptionFactory.Create(localization);

        Assert.EndsWith("/en.png", choices.Single(choice => choice.Code == "en").FlagSource.ToString(), StringComparison.Ordinal);
        Assert.EndsWith("/fr.png", choices.Single(choice => choice.Code == "fr").FlagSource.ToString(), StringComparison.Ordinal);
        Assert.EndsWith("/ar.png", choices.Single(choice => choice.Code == "ar").FlagSource.ToString(), StringComparison.Ordinal);
        Assert.Equal("Anglais", choices.Single(choice => choice.Code == "en").DisplayName);
    }

    [Fact]
    public void ExternalLanguageWithoutBundledFlagUsesNeutralWorldAsset()
    {
        var builtIn = Path.Combine(_root, "built-in");
        var external = Path.Combine(_root, "external");
        Directory.CreateDirectory(builtIn);
        Directory.CreateDirectory(external);
        CopyBuiltInLanguages(builtIn);
        File.WriteAllText(Path.Combine(external, "es.ftl"), "# aeziol-language-format: 1\nlabel-standard = Español\n");
        var localization = new LocalizationService(builtIn, external, "en");

        var spanish = LanguageCardOptionFactory.Create(localization).Single(choice => choice.Code == "es");

        Assert.EndsWith("/ar.png", spanish.FlagSource.ToString(), StringComparison.Ordinal);
        Assert.Contains("Spanish", spanish.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private LocalizationService CreateLocalization(string language)
    {
        var builtIn = Path.Combine(_root, "built-in");
        var external = Path.Combine(_root, "external");
        Directory.CreateDirectory(builtIn);
        Directory.CreateDirectory(external);
        CopyBuiltInLanguages(builtIn);
        return new LocalizationService(builtIn, external, language);
    }

    private static void CopyBuiltInLanguages(string destination)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Localization");
        foreach (var file in Directory.EnumerateFiles(source, "*.ftl"))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }
    }
}
