using Aeziol.App.Appearance;

namespace Aeziol.Tests.App;

public sealed class RouteSummaryPresentationTests
{
    [Fact]
    public void PendingRestoration_ReplacesTheCurrentOutputSelectorWithRestoreAction()
    {
        var presentation = RouteSummaryPresentation.For(
            currentEndpointId: "speakers",
            targetEndpointId: "speakers",
            hasPendingRestoration: true);

        Assert.Equal("restore-output", presentation.OutputLabelLocalizationKey);
        Assert.False(presentation.ShowCurrentOutputSelector);
        Assert.True(presentation.ShowRestoreOutput);
        Assert.True(presentation.ShowForceRestore);
        Assert.False(presentation.ShowIdenticalOutputWarning);
    }

    [Fact]
    public void IdleRoute_ShowsTheSelectorAndWarnsWhenOutputsAreIdentical()
    {
        var presentation = RouteSummaryPresentation.For(
            currentEndpointId: "speakers",
            targetEndpointId: "SPEAKERS",
            hasPendingRestoration: false);

        Assert.Equal("current-output", presentation.OutputLabelLocalizationKey);
        Assert.True(presentation.ShowCurrentOutputSelector);
        Assert.False(presentation.ShowRestoreOutput);
        Assert.False(presentation.ShowForceRestore);
        Assert.True(presentation.ShowIdenticalOutputWarning);
    }
}
