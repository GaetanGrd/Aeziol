using System.Text.RegularExpressions;
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
    public void RouteCicadaIsDiscoverableAndHasAwakeAndSleepingStates()
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
        var awakeCicada = FindNamedElement(windowDocument, "AutomationAwakeCicadaImage");
        var sleepingVisual = FindNamedElement(windowDocument, "AutomationSleepingCicadaVisual");
        var navigationBrand = FindNamedElement(windowDocument, "NavigationBrandCicada");
        var sleepingDrawing = appDocument.Descendants()
            .Single(element => element.Name.LocalName == "DrawingImage"
                && element.Attribute(Xaml + "Key")?.Value == "AeziolSleepingCicadaDrawing");

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
        Assert.Equal("{DynamicResource AeziolCicadaDrawing}", awakeCicada.Attribute("Source")?.Value);
        Assert.Equal("0", sleepingVisual.Attribute("Opacity")?.Value);
        Assert.Contains(sleepingDrawing.Descendants(), element => element.Name.LocalName == "GeometryDrawing");
        Assert.DoesNotContain(windowDocument.Descendants(), element => element.Attribute(Xaml + "Name")?.Value == "AutomationStateDot");
        Assert.Contains("AutomationAwakeWingScale.BeginAnimation", windowSource, StringComparison.Ordinal);
        Assert.Contains("AutomationSleepingCicadaScale.BeginAnimation", windowSource, StringComparison.Ordinal);
        Assert.Contains("AutomationSleepingCicadaRotation.BeginAnimation", windowSource, StringComparison.Ordinal);
        Assert.Contains("var targetAwakeScaleX = enabled ? 1 : 0.62;", windowSource, StringComparison.Ordinal);
        Assert.Contains("var targetSleepingOpacity = enabled ? 0 : 1;", windowSource, StringComparison.Ordinal);
        Assert.Contains("animate && !_runtime.Settings.ReduceAnimations", windowSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SleepingCicadaSvgKeepsTheBrandLanguageAndOwnsItsRestingPosture()
    {
        var activeDocument = XDocument.Load(FindSourceFile("src", "Aeziol.App", "Assets", "Brand", "aeziol-cicada.svg"));
        var sleepingDocument = XDocument.Load(FindSourceFile("src", "Aeziol.App", "Assets", "Brand", "aeziol-cicada-sleeping.svg"));
        var appDocument = XDocument.Load(FindSourceFile("src", "Aeziol.App", "App.xaml"));
        var activeRoot = activeDocument.Root ?? throw new InvalidDataException("The active cicada SVG has no root element.");
        var sleepingRoot = sleepingDocument.Root ?? throw new InvalidDataException("The sleeping cicada SVG has no root element.");
        var activePaths = activeDocument.Descendants().Where(element => element.Name.LocalName == "path").ToArray();
        var sleepingPaths = sleepingDocument.Descendants().Where(element => element.Name.LocalName == "path").ToArray();
        var sleepingDrawingPaths = appDocument.Descendants()
            .Single(element => element.Name.LocalName == "DrawingImage"
                && element.Attribute(Xaml + "Key")?.Value == "AeziolSleepingCicadaDrawing")
            .Descendants()
            .Where(element => element.Name.LocalName == "GeometryDrawing")
            .Select(element => element.Attribute("Geometry")?.Value ?? string.Empty)
            .ToArray();

        Assert.Equal(activeRoot.Attribute("viewBox")?.Value, sleepingRoot.Attribute("viewBox")?.Value);
        Assert.Equal(activeRoot.Attribute("width")?.Value, sleepingRoot.Attribute("width")?.Value);
        Assert.Equal(activeRoot.Attribute("height")?.Value, sleepingRoot.Attribute("height")?.Value);
        Assert.Equal(activePaths.Length, sleepingPaths.Length);
        Assert.Equal(8, sleepingPaths.Length);
        Assert.All(
            activePaths.Zip(sleepingPaths),
            pair => Assert.NotEqual(pair.First.Attribute("d")?.Value, pair.Second.Attribute("d")?.Value));
        Assert.DoesNotContain("zzz", sleepingDocument.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("moon", sleepingDocument.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            sleepingPaths.Select(path => ExtractGeometryNumbers(path.Attribute("d")?.Value ?? string.Empty)),
            sleepingDrawingPaths.Select(ExtractGeometryNumbers));
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

    private static string ExtractGeometryNumbers(string geometry) =>
        string.Join(',', Regex.Matches(geometry, @"-?\d+(?:\.\d+)?").Select(match => match.Value));

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
