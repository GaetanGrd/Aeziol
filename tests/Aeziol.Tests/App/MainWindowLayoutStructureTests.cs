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
        var passageHeaderText = FindNamedElement(document, "PassageHeaderText");
        var comingSoon = FindNamedElement(document, "ComingSoonNav");
        var comingSoonText = FindNamedElement(document, "ComingSoonNavText");
        var settingsSection = FindNamedElement(document, "SettingsNavSection");

        Assert.Equal("Border", passageHeader.Name.LocalName);
        Assert.Equal("False", passageHeader.Attribute("IsHitTestVisible")?.Value);
        Assert.Equal("10", passageHeaderText.Attribute("FontSize")?.Value);
        Assert.Equal("{DynamicResource AeziolMuted}", passageHeaderText.Attribute("Foreground")?.Value);
        Assert.Equal("RadioButton", comingSoon.Name.LocalName);
        Assert.Equal("False", comingSoon.Attribute("IsEnabled")?.Value);
        Assert.Equal("False", comingSoon.Attribute("IsTabStop")?.Value);
        Assert.Equal("10", comingSoonText.Attribute("FontSize")?.Value);
        Assert.Equal("SemiBold", comingSoonText.Attribute("FontWeight")?.Value);
        Assert.Contains(
            settingsSection.Elements(),
            element => element.Name.LocalName == "Border" && element.Attribute("BorderThickness")?.Value == "0,1,0,0");
    }

    [Fact]
    public void NavigationNamesAreLocalizedForAssistiveTechnology()
    {
        var source = File.ReadAllText(FindSourceFile("src", "Aeziol.App", "MainWindow.xaml.cs"));

        Assert.Contains("AutomationProperties.SetName(DiscordNav, discordLabel);", source, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(ComingSoonNav, comingSoonLabel);", source, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(SettingsNav, settingsLabel);", source, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetHelpText(", source, StringComparison.Ordinal);
        Assert.Contains("ComingSoonNav,", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainNavigationEntriesShareAGroupAcrossTheirDifferentContainers()
    {
        var document = XDocument.Load(FindSourceFile("src", "Aeziol.App", "MainWindow.xaml"));
        var discordNavigation = FindNamedElement(document, "DiscordNav");
        var settingsNavigation = FindNamedElement(document, "SettingsNav");

        Assert.Equal("MainNavigation", discordNavigation.Attribute("GroupName")?.Value);
        Assert.Equal(
            discordNavigation.Attribute("GroupName")?.Value,
            settingsNavigation.Attribute("GroupName")?.Value);
        Assert.NotEqual(discordNavigation.Parent, settingsNavigation.Parent);
    }

    [Fact]
    public void RouteCicadaIsDiscoverableAndUsesOneBrandIconForBothStates()
    {
        var appDocument = XDocument.Load(FindSourceFile("src", "Aeziol.App", "App.xaml"));
        var windowDocument = XDocument.Load(FindSourceFile("src", "Aeziol.App", "MainWindow.xaml"));
        var windowSource = File.ReadAllText(FindSourceFile("src", "Aeziol.App", "MainWindow.xaml.cs"));
        var style = appDocument.Descendants()
            .Single(element => element.Name.LocalName == "Style" && element.Attribute(Xaml + "Key")?.Value == "AutomationCicadaButton");
        var hoverTrigger = style.Descendants()
            .Single(element => element.Name.LocalName == "Trigger"
                && element.Attribute("Property")?.Value == "IsMouseOver"
                && element.Attribute("Value")?.Value == "True");
        var actionButton = FindNamedElement(windowDocument, "AutomationActionButton");
        var actionText = FindNamedElement(windowDocument, "AutomationActionText");
        var cicada = FindNamedElement(windowDocument, "AutomationCicadaImage");
        var navigationBrand = FindNamedElement(windowDocument, "NavigationBrandCicada");

        Assert.Contains(
            hoverTrigger.Elements(),
            element => element.Name.LocalName == "Setter"
                && element.Attribute("TargetName")?.Value == "HoverWash"
                && element.Attribute("Property")?.Value == "Opacity"
                && element.Attribute("Value")?.Value == "0.16");
        Assert.Contains(
            hoverTrigger.Elements(),
            element => element.Name.LocalName == "Setter"
                && element.Attribute("TargetName")?.Value == "HoverOutline"
                && element.Attribute("Property")?.Value == "Opacity");
        Assert.Equal("{StaticResource AutomationCicadaButton}", actionButton.Attribute("Style")?.Value);
        Assert.Equal("AutomationRouteControlHost", actionButton.Ancestors().First(element => element.Attribute(Xaml + "Name") is not null).Attribute(Xaml + "Name")?.Value);
        Assert.Equal("False", navigationBrand.Attribute("IsHitTestVisible")?.Value);
        Assert.Null(navigationBrand.Attribute("Click"));
        Assert.Equal("9", actionText.Attribute("FontSize")?.Value);
        Assert.Equal("SemiBold", actionText.Attribute("FontWeight")?.Value);
        Assert.Equal("{DynamicResource AeziolCicadaDrawing}", cicada.Attribute("Source")?.Value);
        Assert.DoesNotContain(windowDocument.Descendants(), element => element.Attribute(Xaml + "Name")?.Value == "AutomationStateDot");
        Assert.DoesNotContain("SleepingCicada", appDocument.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("SleepingCicada", windowDocument.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("SleepingCicada", windowSource, StringComparison.Ordinal);
        Assert.DoesNotContain("AwakeCicada", windowSource, StringComparison.Ordinal);
        Assert.Contains("animate && !_runtime.Settings.ReduceAnimations", windowSource, StringComparison.Ordinal);
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
        Assert.Equal("AutomationRouteControlHost", automationAction.Ancestors().First(element => element.Attribute(Xaml + "Name") is not null).Attribute(Xaml + "Name")?.Value);
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
