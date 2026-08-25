using System.Windows;
using System.Windows.Controls;
using System.Xml.Linq;
using Aeziol.App.DiscordSettingsV4;

namespace Aeziol.Tests.App;

public sealed class DiscordSettingsV4Concept2StructureTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void DiscordSettingsKeepOnlyTheFiveSelectedResponsibilities()
    {
        var concept = LoadConcept();
        var window = XDocument.Load(FindSourceFile("src", "Aeziol.App", "MainWindow.xaml"));
        var tags = concept.Descendants()
            .Concat(window.Descendants())
            .Select(element => element.Attribute("Tag")?.Value)
            .Where(value => value?.StartsWith("DiscordSetting:", StringComparison.Ordinal) == true)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            ["DiscordSetting:2", "DiscordSetting:3", "DiscordSetting:7", "DiscordSetting:8", "DiscordSetting:9"],
            tags);
        Assert.Equal("DiscordSetting:2", FindNamedElement(concept, "RestoreDelaySetting").Attribute("Tag")?.Value);
        Assert.Equal("DiscordSetting:3", FindNamedElement(concept, "ExcludedOutputsSetting").Attribute("Tag")?.Value);
        Assert.Equal("DiscordSetting:9", FindNamedElement(concept, "WindowsNotificationSetting").Attribute("Tag")?.Value);
        Assert.Equal("DiscordSetting:7", FindNamedElement(window, "RevokeDiscordButton").Attribute("Tag")?.Value);
        Assert.Equal("DiscordSetting:8", FindNamedElement(window, "DiscordFallbackToggle").Attribute("Tag")?.Value);
    }

    [Fact]
    public void MainSettingsUseTwoPermanentColumnsWithoutLaunchersOrModals()
    {
        var document = LoadConcept();
        var columns = FindNamedElement(document, "DiscordSettingsColumns");
        var globalCard = FindNamedElement(document, "GlobalSettingsCard");
        var outputCard = FindNamedElement(document, "OutputSettingsCard");
        var globalSettingsIcon = FindNamedElement(document, "GlobalSettingsIcon");

        Assert.Equal("*", columns.Descendants().First(element =>
            element.Name.LocalName == "ColumnDefinition").Attribute("Width")?.Value);
        Assert.Equal("0", globalCard.Attribute("Grid.Column")?.Value);
        Assert.Equal("2", outputCard.Attribute("Grid.Column")?.Value);
        var globalSettingsIconData = globalSettingsIcon.Attribute("Data")?.Value ?? string.Empty;
        Assert.Contains("M 3,5 L 17,5", globalSettingsIconData, StringComparison.Ordinal);
        Assert.DoesNotContain("SettingsRingsGeometry", globalSettingsIconData, StringComparison.Ordinal);
        Assert.Equal("Stretch", FindNamedElement(document, "ExcludedOutputsHost")
            .Attribute("VerticalContentAlignment")?.Value);
        Assert.Null(FindNamedElement(document, "ExcludedOutputsHost").Attribute("MinHeight"));
        Assert.DoesNotContain(document.Descendants(), element =>
            element.Name.LocalName == "SettingsSectionCard");
        Assert.DoesNotContain(document.Descendants(), element =>
            element.Attribute(Xaml + "Name")?.Value?.Contains("Modal", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(document.Descendants(), element =>
            element.Attribute(Xaml + "Name")?.Value?.StartsWith("Open", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void RestoreDelayOffersTheRequestedPresetsAndCustomValue()
    {
        var document = LoadConcept();
        var comboBox = FindNamedElement(document, "RestoreDelayComboBox");

        Assert.Equal("1", comboBox.Attribute("Grid.Column")?.Value);
        Assert.Equal(
            ["Immédiatement", "1 seconde", "2 secondes", "3 secondes", "Personnaliser"],
            comboBox.Elements().Select(element => element.Attribute("Content")?.Value));
        Assert.Equal(
            ["0", "1", "2", "3", "Custom"],
            comboBox.Elements().Select(element => element.Attribute("Tag")?.Value));
        Assert.Equal("132", comboBox.Attribute("Width")?.Value);
        Assert.Equal("36", comboBox.Attribute("Height")?.Value);
        Assert.Equal("Center", comboBox.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("Collapsed", FindNamedElement(document, "CustomRestoreDelayPanel")
            .Attribute("Visibility")?.Value);
        Assert.Equal("2", FindNamedElement(document, "CustomRestoreDelayTextBox")
            .Attribute("MaxLength")?.Value);
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

        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "Image");
        Assert.DoesNotContain("DiscordSymbolGeometry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AeziolCicadaDrawing", source, StringComparison.Ordinal);
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
    public void ConceptInstantiatesWithPermanentGlobalAndOutputColumns()
    {
        WpfTestHost.Run(() =>
        {
            var concept = new DiscordSettingsV4Concept2();
            concept.ApplyTemplate();

            Assert.IsType<Border>(concept.FindName("GlobalSettingsCard"));
            Assert.IsType<Border>(concept.FindName("RestoreDelaySetting"));
            Assert.IsType<Border>(concept.FindName("OutputSettingsCard"));
            Assert.IsType<Border>(concept.FindName("ExcludedOutputsSetting"));
            Assert.IsType<Border>(concept.FindName("WindowsNotificationSetting"));
            Assert.IsType<ContentControl>(concept.FindName("DiscordConnectionHost"));
            Assert.IsType<ContentControl>(concept.FindName("ExcludedOutputsHost"));
            Assert.Null(concept.FindName("SettingsModalLayer"));
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
