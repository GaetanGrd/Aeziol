using Aeziol.App.Appearance;

namespace Aeziol.Tests.App;

public sealed class AutomationPresentationTests
{
    [Fact]
    public void EnabledAutomationOffersAVisibleDisableAction()
    {
        var presentation = AutomationPresentation.For(enabled: true);

        Assert.Equal("automation-disable", presentation.ActionLocalizationKey);
        Assert.Equal("AeziolDanger", presentation.AccentBrushKey);
        Assert.Equal(1, presentation.ContentOpacity);
        Assert.True(presentation.ContentIsEnabled);
    }

    [Fact]
    public void DisabledAutomationGreysContentAndOffersGreenEnableAction()
    {
        var presentation = AutomationPresentation.For(enabled: false);

        Assert.Equal("automation-enable", presentation.ActionLocalizationKey);
        Assert.Equal("AeziolSuccess", presentation.AccentBrushKey);
        Assert.Equal(0.62, presentation.ContentOpacity);
        Assert.False(presentation.ContentIsEnabled);
    }
}
