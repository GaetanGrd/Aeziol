using Aeziol.App.Appearance;

namespace Aeziol.Tests.App;

public sealed class AutomationPresentationTests
{
    [Fact]
    public void EnabledAutomationOffersAVisibleDisableAction()
    {
        var presentation = AutomationPresentation.For(enabled: true);

        Assert.Equal("automation-disable", presentation.ActionLocalizationKey);
        Assert.Equal("M 2,1 L 2,11 M 8,1 L 8,11", presentation.IconGeometry);
        Assert.Equal("AeziolWarningOrange", presentation.AccentBrushKey);
        Assert.Equal("AeziolRaised", presentation.BackgroundBrushKey);
        Assert.Equal(1, presentation.ContentOpacity);
        Assert.True(presentation.ContentIsEnabled);
    }

    [Fact]
    public void DisabledAutomationGreysContentAndOffersGreenEnableAction()
    {
        var presentation = AutomationPresentation.For(enabled: false);

        Assert.Equal("automation-enable", presentation.ActionLocalizationKey);
        Assert.Equal("M 1,1 L 10,6 L 1,11 Z", presentation.IconGeometry);
        Assert.Equal("AeziolSuccess", presentation.AccentBrushKey);
        Assert.Equal("AeziolRaised", presentation.BackgroundBrushKey);
        Assert.Equal(0.32, presentation.ContentOpacity);
        Assert.False(presentation.ContentIsEnabled);
    }
}
