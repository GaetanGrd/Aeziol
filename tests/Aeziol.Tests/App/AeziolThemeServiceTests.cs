using Aeziol.App.Appearance;
using Aeziol.App.Settings;
using System.Windows.Media;

namespace Aeziol.Tests.App;

public sealed class AeziolThemeServiceTests
{
    public static TheoryData<AeziolTheme> Themes => new()
    {
        AeziolTheme.Elgo,
        AeziolTheme.Elna,
        AeziolTheme.Ilyors,
        AeziolTheme.Cherry,
        AeziolTheme.Yuna,
        AeziolTheme.Lilith,
        AeziolTheme.Chaos,
    };

    [Theory]
    [MemberData(nameof(Themes))]
    public void ButtonText_AlwaysUsesBlack(AeziolTheme theme)
    {
        var palette = AeziolThemeService.GetPalette(theme);

        foreach (var enhanced in new[] { false, true })
        {
            var appearance = AeziolThemeService.GetAppearancePalette(enhanced);
            var accent = AeziolThemeService.SelectUiAccent(palette, appearance, enhanced);
            var text = AeziolThemeService.GetContrastingText(accent);

            Assert.Equal(Colors.Black, text);
        }
    }

    [Theory]
    [MemberData(nameof(Themes))]
    public void AppearanceAndEnhancedContrast_NeverChangeTheAccentColor(AeziolTheme theme)
    {
        var palette = AeziolThemeService.GetPalette(theme);

        var darkAppearance = AeziolThemeService.GetAppearancePalette(enhancedContrast: false);
        var oledAppearance = AeziolThemeService.GetAppearancePalette(enhancedContrast: true);
        var normal = AeziolThemeService.SelectUiAccent(palette, darkAppearance, enhanceContrast: false);
        var enhanced = AeziolThemeService.SelectUiAccent(palette, oledAppearance, enhanceContrast: true);

        Assert.Equal(palette.Primary, normal);
        Assert.Equal(normal, enhanced);
    }

    [Fact]
    public void OledAppearance_UsesTrueBlackForTheWindowAndRail()
    {
        var oled = AeziolThemeService.GetAppearancePalette(enhancedContrast: true);

        Assert.Equal(Colors.Black, oled.Canvas);
        Assert.Equal(Colors.Black, oled.Rail);
    }

    [Fact]
    public void StandardAppearance_UsesDarkCanvas()
    {
        var dark = AeziolThemeService.GetAppearancePalette(enhancedContrast: false);

        Assert.Equal(System.Windows.Media.Color.FromRgb(0x07, 0x07, 0x09), dark.Canvas);
    }

}
