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
        foreach (var copy in forbiddenCopy)
        {
            Assert.DoesNotContain(copy, source, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "Image");
        Assert.DoesNotContain("DiscordSymbolGeometry", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AeziolCicadaDrawing", source, StringComparison.Ordinal);
        Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "ScrollViewer");
        Assert.DoesNotContain("Réglages Discord", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ConceptUsesTwoBalancedColumnsAndDefaultsToAutomaticDetection()
    {
        var document = LoadConcept();
        var audioColumn = FindNamedElement(document, "AudioBehaviorColumn");
        var installationColumn = FindNamedElement(document, "DiscordInstallationColumn");
        var columns = audioColumn.Parent!.Elements()
            .Single(element => element.Name.LocalName == "Grid.ColumnDefinitions");

        Assert.Equal("0", audioColumn.Attribute("Grid.Column")?.Value);
        Assert.Equal("2", installationColumn.Attribute("Grid.Column")?.Value);
        Assert.Equal(["*", "14", "*"], columns.Elements().Select(element => element.Attribute("Width")?.Value));
        Assert.Equal("150", FindNamedElement(document, "RestoreDelayComboBox").Attribute("Width")?.Value);
        Assert.Equal("Left", FindNamedElement(document, "RestoreDelayComboBox").Attribute("HorizontalAlignment")?.Value);
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
        });
    }
}
