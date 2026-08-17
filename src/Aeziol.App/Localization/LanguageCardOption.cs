using System.Globalization;
using Aeziol.App.Settings;

namespace Aeziol.App.Localization;

public sealed record LanguageCardOption(
    string Code,
    string NativeName,
    string DisplayName,
    Uri FlagSource);

internal static class LanguageCardOptionFactory
{
    public static IReadOnlyList<LanguageCardOption> Create(LocalizationService localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        return localization.AvailableLanguages
            .Select(choice => Create(choice, localization))
            .ToArray();
    }

    private static LanguageCardOption Create(LanguageChoice choice, LocalizationService localization)
    {
        var baseCode = choice.Code.Split('-', StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
        var asset = baseCode switch
        {
            "en" => "en.png",
            "fr" => "fr.png",
            "ar" => "ar.png",
            _ => "ar.png",
        };
        var key = "language-name-" + baseCode;
        var localizedName = localization.Get(key, WritingRegister.Standard);
        if (string.Equals(localizedName, key, StringComparison.Ordinal))
        {
            localizedName = CultureInfo.GetCultureInfo(choice.Code).EnglishName;
        }

        return new LanguageCardOption(
            choice.Code,
            choice.NativeName,
            localizedName,
            new Uri($"/Aeziol.App;component/Assets/Flags/{asset}", UriKind.Relative));
    }
}
