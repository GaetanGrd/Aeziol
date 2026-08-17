using System.Windows;
using System.Windows.Controls;
using Aeziol.App.Localization;
using Aeziol.App.Settings;
using WpfButton = System.Windows.Controls.Button;
using WpfCheckBox = System.Windows.Controls.CheckBox;

namespace Aeziol.Tests.App;

[Collection(WpfUiTestGroup.Name)]
public sealed class FirstRunWindowTests
{
    [Fact]
    public void MusicChoice_IsExplicitAndPrecedesThePalette()
    {
        WpfTestHost.Run(() =>
        {
            var musicPreviewStates = new List<bool>();
            var localization = new LocalizationService(
                Path.Combine(AppContext.BaseDirectory, "Localization"),
                Path.Combine(Path.GetTempPath(), "Aeziol.Tests", "Languages"),
                "en");
            var window = new Aeziol.App.FirstRunWindow(
                localization,
                "en",
                AeziolTheme.Elgo,
                enhanceContrast: false,
                ambientMusicEnabledChanged: musicPreviewStates.Add);
            try
            {
                var essentials = Assert.IsType<Grid>(window.FindName("EssentialsStep"));
                var music = Assert.IsType<Grid>(window.FindName("MusicStep"));
                var palette = Assert.IsType<Grid>(window.FindName("PaletteStep"));
                var musicToggle = Assert.IsType<WpfCheckBox>(window.FindName("MusicEnabledCheck"));

                Assert.Equal(Visibility.Visible, essentials.Visibility);
                Assert.Equal(Visibility.Collapsed, music.Visibility);
                Assert.False(window.AmbientMusicEnabled);

                Assert.IsType<WpfButton>(window.FindName("ContinueButton"))
                    .RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.Equal(Visibility.Collapsed, essentials.Visibility);
                Assert.Equal(Visibility.Visible, music.Visibility);
                Assert.Equal(Visibility.Collapsed, palette.Visibility);

                musicToggle.IsChecked = true;
                musicToggle.RaiseEvent(new RoutedEventArgs(
                    System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.True(window.AmbientMusicEnabled);
                Assert.Equal([true], musicPreviewStates);

                musicToggle.IsChecked = false;
                musicToggle.RaiseEvent(new RoutedEventArgs(
                    System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.Equal([true, false], musicPreviewStates);

                musicToggle.IsChecked = true;
                musicToggle.RaiseEvent(new RoutedEventArgs(
                    System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.IsType<WpfButton>(window.FindName("MusicContinueButton"))
                    .RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Assert.Equal(Visibility.Collapsed, music.Visibility);
                Assert.Equal(Visibility.Visible, palette.Visibility);
            }
            finally
            {
                window.Close();
            }
        });
    }
}
