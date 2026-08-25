using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Aeziol.App.DiscordSettingsV4;

namespace Aeziol.Tests.App;

public sealed class DiscordSettingsV4Concept2StructureTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void ConceptContainsTheFourSettingsNotOwnedByTheOriginalConnectionCard()
    {
        var document = LoadConcept();
        var settings = document.Descendants()
            .Where(element => element.Attribute("Tag")?.Value.StartsWith("DiscordSetting:", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Equal(4, settings.Length);
        Assert.Equal(
            ["DiscordSetting:2", "DiscordSetting:3", "DiscordSetting:8", "DiscordSetting:9"],
            settings.Select(element => element.Attribute("Tag")!.Value).Order(StringComparer.Ordinal));

        Assert.Equal("DiscordSetting:2", FindNamedElement(document, "RestoreDelaySetting").Attribute("Tag")?.Value);
        Assert.Equal("DiscordSetting:3", FindNamedElement(document, "ExcludedOutputsSetting").Attribute("Tag")?.Value);
        Assert.Equal("DiscordSetting:8", FindNamedElement(document, "DiscordFallbackHost").Attribute("Tag")?.Value);
        Assert.Equal("DiscordSetting:9", FindNamedElement(document, "WindowsNotificationSetting").Attribute("Tag")?.Value);
    }

    [Fact]
    public void ConceptDoesNotRepeatMainDiscordControls()
    {
        var document = LoadConcept();
        var source = File.ReadAllText(FindConceptPath());
        var forbiddenNames = new[]
        {
            "AuthorizeDiscord",
            "DiscordRoute",
            "ModuleActivation",
            "AutomationActivation",
            "Trigger",
            "Condition",
            "Priority",
            "Destination",
            "CurrentOutput",
            "ForceRestore",
            "SourcePanel",
            "TargetPanel",
            "StateDot",
        };
        var names = document.Descendants()
            .Select(element => element.Attribute(Xaml + "Name")?.Value)
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();

        foreach (var forbiddenName in forbiddenNames)
        {
            Assert.DoesNotContain(names, name => name.Contains(forbiddenName, StringComparison.OrdinalIgnoreCase));
        }

        var forbiddenCopy = new[]
        {
            "Autoriser Discord",
            "Activer le module",
            "Déclencheur",
            "Condition",
            "Priorité",
            "Choisir une sortie",
            "Sortie actuelle",
            "Restaurer maintenant",
            "Forcer la restauration",
            "Connexion Discord",
        };
        var visibleCopy = document.Descendants()
            .SelectMany(element => new[] { element.Attribute("Text")?.Value, element.Attribute("Content")?.Value })
            .Where(value => value is not null)
            .Cast<string>()
            .ToArray();
        foreach (var copy in forbiddenCopy)
        {
            Assert.DoesNotContain(visibleCopy, value => value.Contains(copy, StringComparison.OrdinalIgnoreCase));
        }

        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "Image");
        Assert.DoesNotContain("DiscordSymbolGeometry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AeziolCicadaDrawing", source, StringComparison.Ordinal);
        var modalScrollViewer = Assert.Single(
            document.Descendants(),
            element => element.Name.LocalName == "ScrollViewer");
        Assert.Contains(modalScrollViewer.Ancestors(), element =>
            element.Attribute(Xaml + "Name")?.Value == "SettingsModalLayer");
        Assert.DoesNotContain(document.Descendants(), element =>
            string.Equals(element.Attribute("Text")?.Value, "Réglages Discord", StringComparison.Ordinal));
    }

    [Fact]
    public void ConceptUsesThreeCompactLaunchersAndModalSections()
    {
        var document = LoadConcept();
        var launcherStyle = document.Descendants().Single(element =>
            element.Name.LocalName == "Style"
            && element.Attribute(Xaml + "Key")?.Value == "ConceptLauncherButton");
        var launcherSurface = launcherStyle.Descendants().Single(element =>
            element.Attribute(Xaml + "Name")?.Value == "LauncherSurface");

        Assert.Null(launcherStyle.Attribute("BasedOn"));
        Assert.Contains(launcherStyle.Elements(), element =>
            element.Name.LocalName == "Setter"
            && element.Attribute("Property")?.Value == "MinHeight"
            && element.Attribute("Value")?.Value == "168");
        Assert.Equal("16", launcherSurface.Attribute("CornerRadius")?.Value);
        Assert.Equal("0", FindNamedElement(document, "OpenGlobalSettingsButton").Attribute("Grid.Column")?.Value);
        Assert.Equal("2", FindNamedElement(document, "OpenOutputDevicesButton").Attribute("Grid.Column")?.Value);
        Assert.Equal("4", FindNamedElement(document, "OpenFallbackSettingsButton").Attribute("Grid.Column")?.Value);
        Assert.Equal("Collapsed", FindNamedElement(document, "SettingsModalLayer").Attribute("Visibility")?.Value);
        Assert.Null(FindNamedElement(document, "SettingsModalLayer").Attribute("MinHeight"));
        Assert.Equal("520", FindNamedElement(document, "SettingsModalSurface").Attribute("Width")?.Value);
        Assert.Equal("Right", FindNamedElement(document, "SettingsModalSurface").Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Stretch", FindNamedElement(document, "SettingsModalSurface").Attribute("VerticalAlignment")?.Value);
        Assert.NotNull(FindNamedElement(document, "SettingsModalHeaderIcon"));
        Assert.Null(FindNamedElement(document, "SettingsModalScrollViewer").Attribute("MaxHeight"));
        Assert.Equal("1", FindNamedElement(document, "RestoreDelayComboBox").Attribute("Grid.Column")?.Value);
        Assert.Equal("32", FindNamedElement(document, "RestoreDelayComboBox").Attribute("MinHeight")?.Value);
        Assert.Equal("300", FindNamedElement(document, "ExcludedOutputsHost").Attribute("MinHeight")?.Value);
        foreach (var panelName in new[] { "GlobalSettingsPanel", "ExcludedOutputsSetting", "FallbackSettingsPanel" })
        {
            Assert.Contains(FindNamedElement(document, panelName).Ancestors(), element =>
                element.Attribute(Xaml + "Name")?.Value == "SettingsModalLayer");
        }
    }

    private static XDocument LoadConcept() => XDocument.Load(FindConceptPath());

    private static XElement FindNamedElement(XDocument document, string name) =>
        document.Descendants().Single(element => element.Attribute(Xaml + "Name")?.Value == name);

    private static string FindConceptPath() =>
        FindSourceFile("src", "Aeziol.App", "DiscordSettingsV4", "DiscordSettingsV4Concept2.xaml");

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

[Collection(WpfUiTestGroup.Name)]
public sealed class DiscordSettingsV4Concept2WpfTests
{
    [Fact]
    public void ConceptInstantiatesWithTheOriginalConnectionAndFallbackHosts()
    {
        WpfTestHost.Run(() =>
        {
            var concept = new DiscordSettingsV4Concept2();
            concept.ApplyTemplate();

            Assert.IsType<Border>(concept.FindName("RestoreDelaySetting"));
            Assert.IsType<Border>(concept.FindName("ExcludedOutputsSetting"));
            Assert.IsType<Border>(concept.FindName("WindowsNotificationSetting"));
            Assert.IsType<ContentControl>(concept.FindName("DiscordConnectionHost"));
            Assert.IsType<StackPanel>(concept.FindName("DiscordFallbackHost"));
            Assert.IsType<ContentControl>(concept.FindName("ExcludedOutputsHost"));

            var outputsButton = Assert.IsType<System.Windows.Controls.Button>(
                concept.FindName("OpenOutputDevicesButton"));
            outputsButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));

            Assert.Equal(Visibility.Visible, Assert.IsType<Grid>(concept.FindName("SettingsModalLayer")).Visibility);
            Assert.Equal(Visibility.Visible, Assert.IsType<Border>(concept.FindName("ExcludedOutputsSetting")).Visibility);
            Assert.Equal(
                "Périphériques de sortie",
                Assert.IsType<TextBlock>(concept.FindName("SettingsModalTitleText")).Text);
        });
    }
}
