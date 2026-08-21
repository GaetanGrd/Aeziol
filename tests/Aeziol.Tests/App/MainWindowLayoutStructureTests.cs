using System.Xml.Linq;

namespace Aeziol.Tests.App;

public sealed class MainWindowLayoutStructureTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void PassageNavigationUsesAHeaderAndAnInactiveComingSoonEntry()
    {
        var document = XDocument.Load(FindSourceFile("src", "Aeziol.App", "MainWindow.xaml"));

        var passageHeader = FindNamedElement(document, "PassageCategoryHeader");
        var comingSoon = FindNamedElement(document, "ComingSoonNav");
        var settingsSection = FindNamedElement(document, "SettingsNavSection");

        Assert.Equal("Border", passageHeader.Name.LocalName);
        Assert.Equal("False", passageHeader.Attribute("IsHitTestVisible")?.Value);
        Assert.Equal("RadioButton", comingSoon.Name.LocalName);
        Assert.Equal("False", comingSoon.Attribute("IsEnabled")?.Value);
        Assert.Equal("False", comingSoon.Attribute("IsTabStop")?.Value);
        Assert.Contains(
            settingsSection.Elements(),
            element => element.Name.LocalName == "Border" && element.Attribute("BorderThickness")?.Value == "0,1,0,0");
    }

    [Fact]
    public void DiscordRuleOwnsConnectionSettingsAndNoLongerDuplicatesTheDestination()
    {
        var xamlPath = FindSourceFile("src", "Aeziol.App", "MainWindow.xaml");
        var codePath = FindSourceFile("src", "Aeziol.App", "MainWindow.xaml.cs");
        var document = XDocument.Load(xamlPath);
        var source = File.ReadAllText(codePath);

        var rulesView = FindNamedElement(document, "RulesView");
        var settingsHost = FindNamedElement(document, "DiscordSettingsHost");
        var automationAction = FindNamedElement(document, "AutomationActionButton");

        Assert.Contains(settingsHost, rulesView.Descendants());
        Assert.Equal("DiscordHeading", automationAction.Ancestors().First(element => element.Attribute(Xaml + "Name") is not null).Attribute(Xaml + "Name")?.Value);
        Assert.DoesNotContain(document.Descendants(), element => element.Attribute(Xaml + "Name")?.Value == "RuleDestinationCombo");
        Assert.DoesNotContain(document.Descendants(), element => element.Attribute(Xaml + "Name")?.Value == "SettingsDiscordTab");
        Assert.Contains("DiscordSettingsHost.Content = DiscordSettingsCard;", source, StringComparison.Ordinal);
    }

    private static XElement FindNamedElement(XDocument document, string name) =>
        document.Descendants().Single(element => element.Attribute(Xaml + "Name")?.Value == name);

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
