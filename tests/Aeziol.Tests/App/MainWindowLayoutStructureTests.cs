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
    public void AutomationControlCollapsesToAnExplicitActivationButton()
    {
        var windowDocument = XDocument.Load(FindSourceFile("src", "Aeziol.App", "MainWindow.xaml"));
        var windowSource = File.ReadAllText(FindSourceFile("src", "Aeziol.App", "MainWindow.xaml.cs"));
        var actionButton = FindNamedElement(windowDocument, "AutomationActionButton");
        var controlSurface = FindNamedElement(windowDocument, "AutomationControlSurface");
        var cicada = FindNamedElement(windowDocument, "AutomationCicadaImage");
        var cicadaRotation = FindNamedElement(windowDocument, "AutomationCicadaRotation");
        var activeStopGlyph = FindNamedElement(windowDocument, "AutomationActiveStopGlyph");
        var activationIcon = FindNamedElement(windowDocument, "AutomationInactiveActivationIcon");
        var journeyTraceView = FindNamedElement(windowDocument, "PassageJourneyTraceView");
        var passageJourneyTrace = FindNamedElement(windowDocument, "PassageJourneyTrace");
        var settingsJourneyTrace = FindNamedElement(windowDocument, "SettingsJourneyTrace");
        var exclusionsJourneyTrace = FindNamedElement(windowDocument, "ExclusionsJourneyTrace");
        var navigationBrand = FindNamedElement(windowDocument, "NavigationBrandCicada");
        var surfaceStateTriggers = controlSurface.Descendants()
            .Where(element => element.Name.LocalName == "DataTrigger")
            .ToArray();

        Assert.Equal("Button", actionButton.Name.LocalName);
        Assert.Equal("82", actionButton.Attribute("Width")?.Value);
        Assert.Equal("82", actionButton.Attribute("Height")?.Value);
        Assert.Equal("False", actionButton.Attribute("Focusable")?.Value);
        Assert.Equal("False", actionButton.Attribute("IsTabStop")?.Value);
        Assert.Equal("OnAutomationAction", actionButton.Attribute("Click")?.Value);
        Assert.Equal("{StaticResource AutomationCicadaButton}", actionButton.Attribute("Style")?.Value);
        Assert.Equal("AutomationRouteControlHost", actionButton.Ancestors().First(element => element.Attribute(Xaml + "Name") is not null).Attribute(Xaml + "Name")?.Value);
        Assert.Equal("82", controlSurface.Attribute("Width")?.Value);
        Assert.Equal("82", controlSurface.Attribute("Height")?.Value);
        Assert.Equal("0", controlSurface.Attribute("Margin")?.Value);
        Assert.Equal("23", controlSurface.Attribute("CornerRadius")?.Value);
        Assert.Equal("18", activationIcon.Attribute("Width")?.Value);
        Assert.Equal("18", activationIcon.Attribute("Height")?.Value);
        Assert.Contains(activationIcon.Descendants(), element =>
            element.Name.LocalName == "Path"
            && element.Attribute("Stroke")?.Value == "{DynamicResource AeziolGold}");
        Assert.Contains(activeStopGlyph.Descendants(), element =>
            element.Name.LocalName == "Path"
            && element.Attribute("Stroke")?.Value == "{DynamicResource AeziolGold}");
        var stopGlyphTriggers = activeStopGlyph.Descendants()
            .Where(element => element.Name.LocalName == "DataTrigger")
            .ToArray();
        Assert.Contains(activeStopGlyph.Descendants(), element =>
            element.Name.LocalName == "Setter"
            && element.Attribute("Property")?.Value == "Opacity"
            && element.Attribute("Value")?.Value == "0.20");
        Assert.Contains(stopGlyphTriggers, trigger =>
            trigger.Attribute("Binding")?.Value.Contains("IsMouseOver", StringComparison.Ordinal) == true
            && trigger.Descendants().Any(setter =>
                setter.Attribute("Property")?.Value == "Opacity"
                && setter.Attribute("Value")?.Value == "0.94"));
        Assert.DoesNotContain(stopGlyphTriggers, trigger =>
            trigger.Attribute("Binding")?.Value.Contains("IsKeyboardFocused", StringComparison.Ordinal) == true);
        Assert.Contains(surfaceStateTriggers, trigger =>
            trigger.Attribute("Binding")?.Value.Contains("IsMouseOver", StringComparison.Ordinal) == true
            && trigger.Descendants().Any(setter =>
                setter.Name.LocalName == "Setter"
                && setter.Attribute("Property")?.Value == "Opacity"
                && setter.Attribute("Value")?.Value == "0.14"));
        Assert.Contains(surfaceStateTriggers, trigger =>
            trigger.Attribute("Binding")?.Value.Contains("IsPressed", StringComparison.Ordinal) == true
            && trigger.Descendants().Any(setter =>
                setter.Name.LocalName == "Setter"
                && setter.Attribute("Property")?.Value == "Opacity"
                && setter.Attribute("Value")?.Value == "0.24"));
        Assert.DoesNotContain(surfaceStateTriggers, trigger =>
            trigger.Attribute("Binding")?.Value.Contains("IsKeyboardFocused", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(journeyTraceView.Descendants(), element => element.Name.LocalName == "ScaleTransform");
        Assert.Equal("SymmetricFromCenter", passageJourneyTrace.Attribute("ProgressMode")?.Value);
        Assert.Null(settingsJourneyTrace.Attribute("ProgressMode"));
        Assert.Null(exclusionsJourneyTrace.Attribute("ProgressMode"));
        Assert.Equal("RotateTransform", cicadaRotation.Name.LocalName);
        Assert.Equal("0", cicadaRotation.Attribute("Angle")?.Value);
        Assert.Equal("False", navigationBrand.Attribute("IsHitTestVisible")?.Value);
        Assert.Null(navigationBrand.Attribute("Click"));
        Assert.Equal("{DynamicResource AeziolCicadaDrawing}", cicada.Attribute("Source")?.Value);
        Assert.DoesNotContain(windowDocument.Descendants(), element => element.Attribute(Xaml + "Name")?.Value == "AutomationActionText");
        Assert.DoesNotContain(windowDocument.Descendants(), element => element.Attribute(Xaml + "Name")?.Value == "AutomationStateDot");
        Assert.Contains("AutomationActionButton.ToolTip = actionText;", windowSource, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(AutomationActionButton, actionText);", windowSource, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetHelpText(AutomationActionButton, actionText);", windowSource, StringComparison.Ordinal);
        Assert.Contains("AutomationActiveStopGlyph.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;", windowSource, StringComparison.Ordinal);
    }

    [Fact]
    public void JourneyTraceProgressLayerOwnsItsCurvesAndParticles()
    {
        var document = XDocument.Load(FindSourceFile("src", "Aeziol.App", "Controls", "JourneyTrace.xaml"));
        var progressLayer = FindNamedElement(document, "ProgressLayer");
        var baseTraceA = FindNamedElement(document, "BaseTraceA");
        var baseTraceB = FindNamedElement(document, "BaseTraceB");
        var particles = FindNamedElement(document, "BaseParticleLayer");
        var highlights = FindNamedElement(document, "HighlightHost");

        Assert.Contains(baseTraceA, progressLayer.Descendants());
        Assert.Contains(baseTraceB, progressLayer.Descendants());
        Assert.Contains(particles, progressLayer.Descendants());
        Assert.Contains(highlights, progressLayer.Descendants());
    }

    [Fact]
    public void VoicePresenceIntegratesWithRuntimeAsItsSinglePresentationSource()
    {
        var document = XDocument.Load(FindSourceFile("src", "Aeziol.App", "MainWindow.xaml"));
        var source = File.ReadAllText(FindSourceFile("src", "Aeziol.App", "MainWindow.xaml.cs"));
        var sourcePanel = FindNamedElement(document, "PassageSourcePanel");
        var sourceStateText = FindNamedElement(document, "DiscordSourceStateText");
        var presenceIcon = FindNamedElement(document, "DiscordPresenceIcon");
        var discordNavigation = FindNamedElement(document, "DiscordNav");
        var runtimeUpdateStart = source.IndexOf("private void UpdateRuntimeVoiceState(", StringComparison.Ordinal);
        var runtimeUpdateEnd = source.IndexOf(
            "private void UpdatePassageAuthorizationBreak(",
            runtimeUpdateStart,
            StringComparison.Ordinal);
        var runtimeUpdate = source[runtimeUpdateStart..runtimeUpdateEnd];

        Assert.DoesNotContain(document.Descendants(), element =>
            element.Attribute(Xaml + "Name")?.Value is "VoicePill" or "VoicePillDot" or "VoicePillText");
        Assert.Contains(sourceStateText, sourcePanel.Descendants());
        Assert.Equal("VoicePresenceIcon", presenceIcon.Name.LocalName);
        Assert.Equal("39", presenceIcon.Attribute("Width")?.Value);
        Assert.Equal("29", presenceIcon.Attribute("Height")?.Value);
        Assert.Contains(discordNavigation.Descendants(), element =>
            element.Name.LocalName == "Path"
            && element.Attribute("Data")?.Value == "{StaticResource DiscordSymbolGeometry}");
        Assert.Contains("_latestRuntimeVoicePresenceState = state;", runtimeUpdate, StringComparison.Ordinal);
        Assert.Contains("UpdateVoiceState(state);", runtimeUpdate, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthorizationPresenceAnimatesOnlyTheLeadingTraceHalf()
    {
        var document = XDocument.Load(FindSourceFile("src", "Aeziol.App", "MainWindow.xaml"));
        var journeyDocument = XDocument.Load(FindSourceFile("src", "Aeziol.App", "Controls", "JourneyTrace.xaml"));
        var source = File.ReadAllText(FindSourceFile("src", "Aeziol.App", "MainWindow.xaml.cs"));
        var journeyTrace = FindNamedElement(document, "PassageJourneyTrace");
        var ruptureDust = FindNamedElement(journeyDocument, "LeadingBreakDustLayer");
        var referenceDust = FindNamedElement(document, "DiscordRuptureGlints");
        var particles = ruptureDust.Descendants()
            .Where(element => element.Name.LocalName == "Ellipse")
            .ToArray();
        var referenceParticles = referenceDust.Descendants()
            .Where(element => element.Name.LocalName == "Ellipse")
            .ToArray();
        var glow = Assert.Single(ruptureDust.Descendants(), element => element.Name.LocalName == "DropShadowEffect");
        var referenceGlow = Assert.Single(referenceDust.Descendants(), element => element.Name.LocalName == "DropShadowEffect");

        Assert.Equal("JourneyTrace", journeyTrace.Name.LocalName);
        Assert.NotEmpty(particles);
        Assert.All(particles, element =>
        {
            Assert.True(double.Parse(
                element.Attribute("Canvas.Left")?.Value ?? "101",
                System.Globalization.CultureInfo.InvariantCulture) < 100);
            Assert.True(element.Attribute("Fill")?.Value is
                "{DynamicResource AeziolPrimary}" or "{DynamicResource AeziolSecondary}");
            Assert.Contains(referenceParticles, reference =>
                reference.Attribute("Width")?.Value == element.Attribute("Width")?.Value
                && reference.Attribute("Height")?.Value == element.Attribute("Height")?.Value
                && reference.Attribute("Fill")?.Value == element.Attribute("Fill")?.Value
                && reference.Attribute("Opacity")?.Value == element.Attribute("Opacity")?.Value);
        });
        Assert.DoesNotContain(ruptureDust.Descendants(), element => element.Name.LocalName == "Path");
        Assert.Equal(referenceGlow.Attribute("BlurRadius")?.Value, glow.Attribute("BlurRadius")?.Value);
        Assert.Equal(referenceGlow.Attribute("ShadowDepth")?.Value, glow.Attribute("ShadowDepth")?.Value);
        Assert.Equal(referenceGlow.Attribute("Color")?.Value, glow.Attribute("Color")?.Value);
        Assert.Equal(referenceGlow.Attribute("Opacity")?.Value, glow.Attribute("Opacity")?.Value);
        Assert.Contains("state == VoicePresenceState.AuthorizationRequired", source, StringComparison.Ordinal);
        Assert.Contains("JourneyTrace.LeadingBreakProgressProperty", source, StringComparison.Ordinal);
        Assert.Contains("DiscordAuthorizationBreakTransitionDurationMilliseconds", source, StringComparison.Ordinal);
        Assert.Contains("new DoubleAnimation(currentBreak, target, duration)", source, StringComparison.Ordinal);
        Assert.Equal("0", ruptureDust.Attribute("Opacity")?.Value);
        Assert.DoesNotContain("PassageAuthorizationRuptureGlints", source, StringComparison.Ordinal);
        Assert.Contains("MotionAssist.GetIsReduced(this)", source, StringComparison.Ordinal);
        Assert.Contains("DiscordPresenceIcon.State = state;", source, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetName(DiscordPresenceIcon", source, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.SetHelpText(DiscordPresenceIcon", source, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthorizationPresentationSuppressesOnlyTheDiscordSourceHoverAndRestoresItAfterward()
    {
        var source = File.ReadAllText(FindSourceFile("src", "Aeziol.App", "MainWindow.xaml.cs"));
        var sourceEnterStart = source.IndexOf("private void OnPassageJourneySourceEnter(", StringComparison.Ordinal);
        var targetEnterStart = source.IndexOf("private void OnPassageJourneyTargetEnter(", sourceEnterStart, StringComparison.Ordinal);
        var leaveStart = source.IndexOf("private void OnPassageJourneyLeave(", targetEnterStart, StringComparison.Ordinal);
        var sourceEnter = source[sourceEnterStart..targetEnterStart];
        var availabilityStart = source.IndexOf(
            "private void UpdatePassageSourceHighlightAvailability(",
            StringComparison.Ordinal);
        var highlightStart = source.IndexOf(
            "private void ShowPassageJourneyHighlight(",
            availabilityStart,
            StringComparison.Ordinal);
        var availability = source[availabilityStart..highlightStart];

        Assert.Contains("_presentedVoicePresenceState = state;", source, StringComparison.Ordinal);
        Assert.Contains(
            "_presentedVoicePresenceState == VoicePresenceState.AuthorizationRequired",
            source,
            StringComparison.Ordinal);
        Assert.Contains("if (IsPassageSourceHighlightSuppressed)", sourceEnter, StringComparison.Ordinal);
        Assert.Contains("PassageJourneyTrace.HideHighlight(sender", sourceEnter, StringComparison.Ordinal);
        Assert.DoesNotContain("IsPassageSourceHighlightSuppressed", source[targetEnterStart..leaveStart], StringComparison.Ordinal);
        Assert.Contains("PassageJourneyTrace.HideHighlight(PassageSourcePanel", availability, StringComparison.Ordinal);
        Assert.Contains("wasAuthorizationRequired && PassageSourcePanel.IsMouseOver", availability, StringComparison.Ordinal);
        Assert.Contains("ShowPassageJourneyHighlight(PassageSourcePanel, 0, 112);", availability, StringComparison.Ordinal);
    }

    [Fact]
    public void AutomationTransitionsCancelCleanlyAndRespectReducedMotion()
    {
        var source = File.ReadAllText(FindSourceFile("src", "Aeziol.App", "MainWindow.xaml.cs"));

        Assert.Contains("var generation = ++_automationVisualGeneration;", source, StringComparison.Ordinal);
        Assert.Contains("ClearAutomationVisualAnimations();", source, StringComparison.Ordinal);
        Assert.Contains("generation != _automationVisualGeneration", source, StringComparison.Ordinal);
        Assert.Contains("AutomationActionButton.Width = enabled ? 82 : 36;", source, StringComparison.Ordinal);
        Assert.Contains("AutomationActionButton.Height = enabled ? 82 : 36;", source, StringComparison.Ordinal);
        Assert.Contains("AutomationControlSurface.Width = enabled ? 82 : 36;", source, StringComparison.Ordinal);
        Assert.Contains("AutomationControlSurface.Height = enabled ? 82 : 36;", source, StringComparison.Ordinal);
        Assert.Contains("AutomationControlSurface.CornerRadius = enabled ? new CornerRadius(23) : new CornerRadius(18);", source, StringComparison.Ordinal);
        Assert.Contains("AutomationControlSurface.Margin = new Thickness(0);", source, StringComparison.Ordinal);
        Assert.Contains("BeginAutomationDoubleTransition(AutomationActionButton, WidthProperty, buttonWidth, 82, delay", source, StringComparison.Ordinal);
        Assert.Contains("BeginAutomationDoubleTransition(AutomationActionButton, HeightProperty, buttonHeight, 82, delay", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AutomationActionText", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateAutomationThicknessTransition", source, StringComparison.Ordinal);
        Assert.Contains("PassageJourneyTrace.Progress = enabled ? 1 : 0;", source, StringComparison.Ordinal);
        Assert.Contains("Aeziol.App.Controls.JourneyTrace.ProgressProperty", source, StringComparison.Ordinal);
        Assert.Contains("var cicadaX = AutomationCicadaTranslation.X;", source, StringComparison.Ordinal);
        Assert.Contains("var cicadaAngle = AutomationCicadaRotation.Angle;", source, StringComparison.Ordinal);
        Assert.Contains("TranslateTransform.XProperty", source, StringComparison.Ordinal);
        Assert.Contains("TranslateTransform.YProperty", source, StringComparison.Ordinal);
        Assert.Contains("RotateTransform.AngleProperty", source, StringComparison.Ordinal);
        Assert.Contains("private const double AutomationCicadaExitX = -16;", source, StringComparison.Ordinal);
        Assert.Contains("private const double AutomationCicadaExitY = -16;", source, StringComparison.Ordinal);
        Assert.Contains("private const double AutomationCicadaExitAngle = -8;", source, StringComparison.Ordinal);
        Assert.Contains("private const double AutomationCicadaReturnX = -14;", source, StringComparison.Ordinal);
        Assert.Contains("private const double AutomationCicadaReturnY = 12;", source, StringComparison.Ordinal);
        Assert.Contains("private const double AutomationCicadaReturnAngle = 7;", source, StringComparison.Ordinal);
        Assert.Contains("private const int AutomationResizeDurationMilliseconds = 280;", source, StringComparison.Ordinal);
        Assert.Contains("private const int AutomationCicadaExitDurationMilliseconds = 220;", source, StringComparison.Ordinal);
        Assert.Contains("private const int AutomationTraceExitDurationMilliseconds = 280;", source, StringComparison.Ordinal);
        Assert.Contains("private const int AutomationCicadaArrivalDurationMilliseconds = 340;", source, StringComparison.Ordinal);
        Assert.Contains("if (!animate || MotionAssist.GetIsReduced(this))", source, StringComparison.Ordinal);
        Assert.Contains("UpdateAutomationControlVisual(_runtime.Settings.AutomationEnabled, animate: false);", source, StringComparison.Ordinal);
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
        Assert.Contains("_discordSettings = new DiscordSettingsV4.DiscordSettingsV4Concept2();", source, StringComparison.Ordinal);
        Assert.Contains("_discordSettings.DiscordConnectionHost.Content = DiscordSettingsCard;", source, StringComparison.Ordinal);
        Assert.Contains("_discordSettings.DiscordFallbackHost.Children.Add(DiscordFallbackToggle);", source, StringComparison.Ordinal);
        Assert.Contains("_discordSettings.DiscordFallbackHost.Children.Add(DiscordFallbackPanel);", source, StringComparison.Ordinal);
        Assert.Contains("DiscordFallbackToggle.IsChecked = true;", source, StringComparison.Ordinal);
        Assert.Contains("DiscordFallbackToggle.Visibility = Visibility.Collapsed;", source, StringComparison.Ordinal);
        Assert.Contains("_discordSettings.ConceptRoot.Children.Remove(_discordSettings.SettingsModalLayer);", source, StringComparison.Ordinal);
        Assert.Contains("RulesView.Children.Add(_discordSettings.SettingsModalLayer);", source, StringComparison.Ordinal);
        Assert.Contains("System.Windows.Controls.Panel.SetZIndex(_discordSettings.SettingsModalLayer, 20);", source, StringComparison.Ordinal);
        Assert.Contains("DiscordSettingsHost.Content = _discordSettings;", source, StringComparison.Ordinal);

        var connectionCard = FindNamedElement(document, "DiscordSettingsCard");
        var connectionRoute = FindNamedElement(document, "DiscordConnectionRouteGrid");
        var discordEndpointIcon = FindNamedElement(document, "DiscordConnectionEndpointIcon");
        var aeziolEndpointIcon = FindNamedElement(document, "AeziolConnectionEndpointIcon");
        var connectedTrail = FindNamedElement(document, "DiscordConnectedTrailCanvas");
        var connectedGlow = FindNamedElement(document, "DiscordConnectedTrailGlowLayer");
        Assert.Equal("16", connectionCard.Attribute("Padding")?.Value);
        Assert.Equal("88", connectionRoute.Attribute("Height")?.Value);
        Assert.Equal("52", discordEndpointIcon.Attribute("Width")?.Value);
        Assert.Equal("52", discordEndpointIcon.Attribute("Height")?.Value);
        Assert.Equal("52", aeziolEndpointIcon.Attribute("Width")?.Value);
        Assert.Equal("52", aeziolEndpointIcon.Attribute("Height")?.Value);
        Assert.Equal(["0.9", "0.75"], connectedTrail.Elements()
            .Where(element => element.Name.LocalName == "Path")
            .Select(element => element.Attribute("StrokeThickness")?.Value));
        Assert.Equal("0.82", connectedGlow.Attribute("Opacity")?.Value);
        Assert.Equal(2, connectedGlow.Elements().Count(element => element.Name.LocalName == "Path"));
        Assert.Contains("DiscordConnectedTrailGlowLayer.Opacity = isAuthorized ? 0.82 : 0;", source, StringComparison.Ordinal);
        Assert.Contains("authorizationGlow", source, StringComparison.Ordinal);
        Assert.Contains(connectionCard.Descendants(), element => element.Attribute(Xaml + "Name")?.Value == "DiscordConnectionTrail");
        Assert.Contains(connectionCard.Descendants(), element => element.Attribute(Xaml + "Name")?.Value == "RevokeDiscordButton");
        Assert.DoesNotContain(document.Descendants(), element => element.Attribute(Xaml + "Name")?.Value == "RulesTitleText");
        Assert.DoesNotContain(document.Descendants(), element => element.Attribute(Xaml + "Name")?.Value == "RulesSubtitleText");
        Assert.Single(rulesView.Elements().Single(element => element.Name.LocalName == "Grid.RowDefinitions").Elements());
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
