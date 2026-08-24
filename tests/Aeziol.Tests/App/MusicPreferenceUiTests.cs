using System.Xml.Linq;

namespace Aeziol.Tests.App;

public sealed class MusicPreferenceUiTests
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void SettingsMusicEditor_ContainsIndependentFocusAndHiddenPreferences()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainWindow.xaml"));
        var keepUnfocusedToggle = FindNamedElement(document, "KeepAmbientMusicPlayingWhenUnfocusedToggle");
        var keepHiddenToggle = FindNamedElement(document, "KeepAmbientMusicPlayingWhenHiddenToggle");
        var combinationText = FindNamedElement(document, "AmbientMusicFocusHiddenCombinationText");

        Assert.Equal(
            "OnKeepAmbientMusicPlayingWhenUnfocusedChanged",
            keepUnfocusedToggle.Attribute("Click")?.Value);
        Assert.Equal("OnKeepAmbientMusicPlayingWhenHiddenChanged", keepHiddenToggle.Attribute("Click")?.Value);
        Assert.True(XNode.DocumentOrderComparer.Compare(keepUnfocusedToggle, keepHiddenToggle) < 0);
        Assert.True(XNode.DocumentOrderComparer.Compare(keepHiddenToggle, combinationText) < 0);
    }

    private static XElement FindNamedElement(XDocument document, string name) =>
        document.Descendants().Single(element => (string?)element.Attribute(XamlNamespace + "Name") == name);
}
