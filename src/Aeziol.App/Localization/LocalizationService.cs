using System.Globalization;
using Aeziol.App.Settings;
using Linguini.Bundle;
using Linguini.Bundle.Builder;

namespace Aeziol.App.Localization;

public sealed record LanguageChoice(string Code, string NativeName);

public sealed class LocalizationService
{
    private const string FormatHeader = "# aeziol-language-format: 1";
    private readonly string _builtInDirectory;
    private readonly string _externalDirectory;
    private FluentBundle _current;
    private FluentBundle _english;

    public LocalizationService(string builtInDirectory, string externalDirectory, string language)
    {
        _builtInDirectory = Path.GetFullPath(builtInDirectory);
        _externalDirectory = Path.GetFullPath(externalDirectory);
        _english = LoadBundle("en");
        var resolvedLanguage = ResolveLanguage(language);
        _current = string.Equals(resolvedLanguage, "en", StringComparison.OrdinalIgnoreCase)
            ? _english
            : LoadBundle(resolvedLanguage);
        CurrentLanguage = resolvedLanguage;
        AvailableLanguages = DiscoverLanguages();
    }

    public string CurrentLanguage { get; private set; }

    public IReadOnlyList<LanguageChoice> AvailableLanguages { get; }

    public bool IsRightToLeft => CultureInfo.GetCultureInfo(CurrentLanguage).TextInfo.IsRightToLeft;

    public void ChangeLanguage(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        var resolvedLanguage = ResolveLanguage(language);
        _current = string.Equals(resolvedLanguage, "en", StringComparison.OrdinalIgnoreCase)
            ? _english
            : LoadBundle(resolvedLanguage);
        CurrentLanguage = resolvedLanguage;
    }

    public string Get(string semanticKey, WritingRegister register)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(semanticKey);
        var standard = semanticKey + "-standard";
        _ = register;
        return TryGet(_current, standard)
            ?? TryGet(_english, standard)
            ?? semanticKey;
    }

    private static string? TryGet(FluentBundle bundle, string key) =>
        bundle.TryGetAstMessage(key, out _) ? bundle.GetMessage(key) : null;

    private FluentBundle LoadBundle(string language)
    {
        var builtInPath = Path.Combine(_builtInDirectory, language + ".ftl");
        var externalPath = Path.Combine(_externalDirectory, language + ".ftl");
        var path = File.Exists(externalPath) ? externalPath : builtInPath;
        var resource = File.ReadAllText(path);
        if (!resource.StartsWith(FormatHeader, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Language file '{path}' has no supported format header.");
        }

        return LinguiniBuilder.Builder()
            .CultureInfo(CultureInfo.GetCultureInfo(language))
            .AddResource(resource)
            .UncheckedBuild();
    }

    private string ResolveLanguage(string language)
    {
        var normalized = NormalizeLanguageCode(language);
        if (normalized is null)
        {
            return "en";
        }

        return File.Exists(Path.Combine(_externalDirectory, normalized + ".ftl"))
            || File.Exists(Path.Combine(_builtInDirectory, normalized + ".ftl"))
                ? normalized
                : "en";
    }

    private LanguageChoice[] DiscoverLanguages()
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddLanguageFiles(_builtInDirectory, codes);
        AddLanguageFiles(_externalDirectory, codes);
        return codes
            .Select(code => new LanguageChoice(code, CultureInfo.GetCultureInfo(code).NativeName))
            .OrderBy(choice => choice.NativeName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static void AddLanguageFiles(string directory, HashSet<string> codes)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.ftl", SearchOption.TopDirectoryOnly))
        {
            var code = NormalizeLanguageCode(Path.GetFileNameWithoutExtension(path));
            if (code is not null)
            {
                codes.Add(code);
            }
        }
    }

    private static string? NormalizeLanguageCode(string language)
    {
        if (string.IsNullOrWhiteSpace(language)
            || language.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0
            || !string.Equals(Path.GetFileName(language), language, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            return CultureInfo.GetCultureInfo(language).Name;
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}
