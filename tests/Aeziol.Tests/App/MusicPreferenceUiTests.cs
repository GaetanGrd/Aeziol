using System.Xml.Linq;

namespace Aeziol.Tests.App;

public sealed class MusicPreferenceUiTests
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void SettingsMusicEditor_MakesHiddenPlaybackADependentPreference()
    {
        var document = XDocument.Load(Path.Combine(AppContext.BaseDirectory, "Fixtures", "MainWindow.xaml"));
        var keepUnfocusedToggle = FindNamedElement(document, "KeepAmbientMusicPlayingWhenUnfocusedToggle");
        var keepHiddenRow = FindNamedElement(document, "KeepAmbientMusicPlayingWhenHiddenRow");
        var keepHiddenToggle = FindNamedElement(document, "KeepAmbientMusicPlayingWhenHiddenToggle");
        var combinationText = FindNamedElement(document, "AmbientMusicFocusHiddenCombinationText");
        var keepHiddenReset = keepHiddenRow.Descendants().Single(element =>
            element.Name.LocalName == "Button"
            && (string?)element.Attribute("Tag") == "KeepAmbientMusicPlayingWhenHidden");

        Assert.Equal(
            "OnKeepAmbientMusicPlayingWhenUnfocusedChanged",
            keepUnfocusedToggle.Attribute("Click")?.Value);
        Assert.Equal("OnKeepAmbientMusicPlayingWhenHiddenChanged", keepHiddenToggle.Attribute("Click")?.Value);
        Assert.Equal("18,8,0,0", keepHiddenRow.Attribute("Margin")?.Value);
        Assert.Equal(
            "{Binding IsChecked, ElementName=KeepAmbientMusicPlayingWhenUnfocusedToggle}",
            keepHiddenRow.Attribute("IsEnabled")?.Value);
        Assert.Contains(keepHiddenRow, keepHiddenToggle.Ancestors());
        Assert.Contains(keepHiddenRow, keepHiddenReset.Ancestors());
        Assert.True(XNode.DocumentOrderComparer.Compare(keepUnfocusedToggle, keepHiddenToggle) < 0);
        Assert.True(XNode.DocumentOrderComparer.Compare(keepHiddenToggle, combinationText) < 0);
    }

    private static XElement FindNamedElement(XDocument document, string name) =>
        document.Descendants().Single(element => (string?)element.Attribute(XamlNamespace + "Name") == name);
}
