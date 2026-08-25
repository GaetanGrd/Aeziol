using System.Windows;
using System.Xml.Linq;
using Aeziol.App.DiscordSettingsV4;
using WpfSize = System.Windows.Size;

namespace Aeziol.Tests.App;

[Collection(WpfUiTestGroup.Name)]
public sealed class DiscordSettingsV4Concept4Tests
{
    private static readonly string[] TagsInDocumentOrder = ["2", "3", "8", "9", "7"];
    private static readonly string[] RequestedTags = ["2", "3", "7", "8", "9"];
    private static readonly string[] NavigationItems = ["Audio", "Discord", "Sécurité"];

    [Fact]
    public void PrototypeContainsExactlyTheRequestedSettings()
    {
        var document = XDocument.Load(FindSourceFile(
            "src", "Aeziol.App", "DiscordSettingsV4", "DiscordSettingsV4Concept4.xaml"));
        var tags = document.Descendants()
            .Select(element => element.Attribute("Tag")?.Value)
            .Where(value => value is not null)
            .ToArray();

        Assert.Equal(TagsInDocumentOrder, tags);
        Assert.Equal(RequestedTags, tags.Order(StringComparer.Ordinal));
        Assert.Equal(tags.Length, tags.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void PrototypeDoesNotRepeatMainInterfaceControlsOrFuturistConcepts()
    {
        var source = File.ReadAllText(FindSourceFile(
            "src", "Aeziol.App", "DiscordSettingsV4", "DiscordSettingsV4Concept4.xaml"));
        var forbiddenPhrases = new[]
        {
            "état discord",
            "autorisation initiale",
            "activer le module",
            "activation du module",
            "déclencheur",
            "priorité",
            "choix de sortie",
            "sortie actuelle",
            "restauration forcée",
            "discord -> aeziol",
            "cockpit",
            "score",
            "orbite",
            "intelligence artificielle",
            "prédiction",
        };

        Assert.All(forbiddenPhrases, phrase =>
            Assert.DoesNotContain(phrase, source, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain('\u2014', source);
    }

    [Fact]
    public void PrototypeUsesACompactLocalNavigationAndAeziolControls()
    {
        var document = XDocument.Load(FindSourceFile(
            "src", "Aeziol.App", "DiscordSettingsV4", "DiscordSettingsV4Concept4.xaml"));
        var navigationItems = document.Descendants()
            .Where(element => element.Name.LocalName == "RadioButton")
            .Select(element => element.Attribute("Content")?.Value)
            .ToArray();

        Assert.Equal(NavigationItems, navigationItems);
        Assert.Contains(document.Descendants(), element =>
            element.Name.LocalName == "ComboBox"
            && element.Attribute("Style")?.Value == "{StaticResource CompactComboBox}");
        Assert.Equal(2, document.Descendants().Count(element =>
            element.Name.LocalName == "CheckBox"
            && element.Attribute("Style")?.Value == "{StaticResource DeviceCheck}"));
        Assert.Contains(document.Descendants(), element =>
            element.Name.LocalName == "CheckBox"
            && element.Attribute("Style")?.Value == "{StaticResource ToggleSwitch}");
        Assert.Contains(document.Descendants(), element =>
            element.Name.LocalName == "Button"
            && element.Attribute("Style")?.Value == "{StaticResource DangerButton}"
            && element.Attribute("Content")?.Value == "Révoquer l’autorisation Discord");
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "ScrollViewer");
    }

    [Fact]
    public void PrototypeInstantiatesOnTheWpfDispatcher()
    {
        WpfTestHost.Run(() =>
        {
            var control = new DiscordSettingsV4Concept4();
            var root = Assert.IsType<System.Windows.Controls.Border>(control.Content);
            Assert.NotNull(root.Child);
            root.Measure(new WpfSize(992, 712));
            root.Arrange(new Rect(0, 0, 992, 712));
            root.UpdateLayout();
            Assert.True(root.DesiredSize.Width > 0);
            Assert.True(root.DesiredSize.Height > 0);
        });
    }

    private static string FindSourceFile(params string[] relativeSegments)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. relativeSegments]);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not locate {Path.Combine(relativeSegments)} from the test output directory.");
    }
}
