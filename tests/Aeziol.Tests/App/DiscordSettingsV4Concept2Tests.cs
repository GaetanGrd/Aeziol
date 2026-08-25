using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Aeziol.App.Controls;
using Aeziol.App.DiscordSettingsV4;

namespace Aeziol.Tests.App;

public sealed class DiscordSettingsV4Concept2StructureTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly string[] LauncherNames =
        ["OpenGlobalSettingsButton", "OpenOutputDevicesButton", "OpenFallbackSettingsButton"];

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
        var component = XDocument.Load(FindSourceFile(
            "src", "Aeziol.App", "Controls", "SettingsSectionCard.xaml"));
        var launcherSurface = component.Descendants().Single(element =>
            element.Attribute(Xaml + "Name")?.Value == "CardSurface");
        var journeyTrace = component.Descendants().Single(element =>
            element.Attribute(Xaml + "Name")?.Value == "CardJourneyTrace");
        var iconSurface = FindNamedElement(component, "SectionIconSurface");

        Assert.Equal("168", component.Root?.Attribute("MinHeight")?.Value);
        Assert.Equal("16", launcherSurface.Attribute("CornerRadius")?.Value);
        Assert.Equal("JourneyTrace", journeyTrace.Name.LocalName);
        Assert.Equal("0.9", journeyTrace.Attribute("BaseStrokeA")?.Value);
        Assert.Equal("0.75", journeyTrace.Attribute("BaseStrokeB")?.Value);
        Assert.Equal("52", iconSurface.Attribute("Width")?.Value);
        Assert.Equal("52", iconSurface.Attribute("Height")?.Value);
        Assert.Equal("OnCardMouseEnter", FindNamedElement(component, "SectionButton").Attribute("MouseEnter")?.Value);
        Assert.Equal("OnCardMouseLeave", FindNamedElement(component, "SectionButton").Attribute("MouseLeave")?.Value);
        Assert.All(
            LauncherNames,
            name => Assert.Equal("SettingsSectionCard", FindNamedElement(document, name).Name.LocalName));
        Assert.Equal("0", FindNamedElement(document, "OpenGlobalSettingsButton").Attribute("Grid.Column")?.Value);
        Assert.Equal("2", FindNamedElement(document, "OpenOutputDevicesButton").Attribute("Grid.Column")?.Value);
        Assert.Equal("4", FindNamedElement(document, "OpenFallbackSettingsButton").Attribute("Grid.Column")?.Value);
        Assert.Equal("Collapsed", FindNamedElement(document, "SettingsModalLayer").Attribute("Visibility")?.Value);
        Assert.Null(FindNamedElement(document, "SettingsModalLayer").Attribute("MinHeight"));
        Assert.Null(FindNamedElement(document, "SettingsModalSurface").Attribute("Width"));
        Assert.Equal("480", FindNamedElement(document, "SettingsModalSurface").Attribute("MinWidth")?.Value);
        Assert.Equal("720", FindNamedElement(document, "SettingsModalSurface").Attribute("MaxWidth")?.Value);
        Assert.Equal("520", FindNamedElement(document, "SettingsModalSurface").Attribute("MaxHeight")?.Value);
        Assert.Equal("Center", FindNamedElement(document, "SettingsModalSurface").Attribute("HorizontalAlignment")?.Value);
        Assert.Equal("Center", FindNamedElement(document, "SettingsModalSurface").Attribute("VerticalAlignment")?.Value);
        Assert.NotNull(FindNamedElement(document, "SettingsModalHeaderIcon"));
        Assert.Null(FindNamedElement(document, "SettingsModalScrollViewer").Attribute("MaxHeight"));
        Assert.Equal("1", FindNamedElement(document, "RestoreDelayComboBox").Attribute("Grid.Column")?.Value);
        Assert.Equal("32", FindNamedElement(document, "RestoreDelayComboBox").Attribute("MinHeight")?.Value);
        Assert.Equal(
            ["Immédiatement", "1 seconde", "2 secondes", "3 secondes", "Personnaliser"],
            FindNamedElement(document, "RestoreDelayComboBox").Elements()
                .Select(element => element.Attribute("Content")?.Value));
        Assert.Equal(
            ["0", "1", "2", "3", "Custom"],
            FindNamedElement(document, "RestoreDelayComboBox").Elements()
                .Select(element => element.Attribute("Tag")?.Value));
        Assert.Equal("Collapsed", FindNamedElement(document, "CustomRestoreDelayPanel").Attribute("Visibility")?.Value);
        Assert.Equal("2", FindNamedElement(document, "CustomRestoreDelayTextBox").Attribute("MaxLength")?.Value);
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

            var outputsButton = Assert.IsType<SettingsSectionCard>(
                concept.FindName("OpenOutputDevicesButton"));
            outputsButton.RaiseEvent(new RoutedEventArgs(SettingsSectionCard.ClickEvent));

            Assert.Equal(Visibility.Visible, Assert.IsType<Grid>(concept.FindName("SettingsModalLayer")).Visibility);
            Assert.Equal(Visibility.Visible, Assert.IsType<Border>(concept.FindName("ExcludedOutputsSetting")).Visibility);
            Assert.Equal(
                "Périphériques de sortie",
                Assert.IsType<TextBlock>(concept.FindName("SettingsModalTitleText")).Text);
        });
    }

    [Fact]
    public void ReusableSectionCardForwardsClicksFromItsInternalButton()
    {
        WpfTestHost.Run(() =>
        {
            var card = new SettingsSectionCard
            {
                Title = "Section",
                Description = "Description",
                IconData = System.Windows.Media.Geometry.Parse("M 0,0 L 10,10"),
            };
            var clickCount = 0;
            card.Click += (_, _) => clickCount++;
            card.ApplyTemplate();

            var button = Assert.IsType<System.Windows.Controls.Button>(card.FindName("SectionButton"));
            button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));

            Assert.Equal(1, clickCount);
            Assert.IsType<JourneyTrace>(card.FindName("CardJourneyTrace"));
        });
    }

    [Fact]
    public void CustomRestoreDelayIsOnlyShownForTheCustomChoice()
    {
        WpfTestHost.Run(() =>
        {
            var concept = new DiscordSettingsV4Concept2();
            var comboBox = Assert.IsType<System.Windows.Controls.ComboBox>(
                concept.FindName("RestoreDelayComboBox"));
            var customPanel = Assert.IsType<StackPanel>(concept.FindName("CustomRestoreDelayPanel"));
            var customTextBox = Assert.IsType<System.Windows.Controls.TextBox>(
                concept.FindName("CustomRestoreDelayTextBox"));

            concept.SetRestoreDelaySeconds(12);

            Assert.Equal(4, comboBox.SelectedIndex);
            Assert.Equal(Visibility.Visible, customPanel.Visibility);
            Assert.Equal("12", customTextBox.Text);
            Assert.Equal(12, concept.RestoreDelaySeconds);

            comboBox.SelectedIndex = 2;

            Assert.Equal(Visibility.Collapsed, customPanel.Visibility);
            Assert.Equal(2, concept.RestoreDelaySeconds);
        });
    }
}
