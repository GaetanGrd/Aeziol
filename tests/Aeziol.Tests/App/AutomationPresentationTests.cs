using Aeziol.App.Appearance;

namespace Aeziol.Tests.App;

public sealed class AutomationPresentationTests
{
    [Fact]
    public void EnabledAutomationOffersAGoldDisableAction()
    {
        var presentation = AutomationPresentation.For(enabled: true);

        Assert.Equal("automation-disable", presentation.ActionLocalizationKey);
        Assert.Equal("AeziolGold", presentation.AccentBrushKey);
        Assert.Equal(1, presentation.ContentOpacity);
        Assert.True(presentation.ContentIsEnabled);
    }

    [Fact]
    public void DisabledAutomationDarkensContentAndOffersGoldEnableAction()
    {
        var presentation = AutomationPresentation.For(enabled: false);

        Assert.Equal("automation-enable", presentation.ActionLocalizationKey);
        Assert.Equal("AeziolGold", presentation.AccentBrushKey);
        Assert.Equal(0.32, presentation.ContentOpacity);
        Assert.False(presentation.ContentIsEnabled);
    }
}
