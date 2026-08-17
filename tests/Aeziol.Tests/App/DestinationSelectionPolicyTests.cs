using Aeziol.App.Settings;

namespace Aeziol.Tests.App;

public sealed class DestinationSelectionPolicyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TransientEmptySelectionNeverClearsConfiguredDestination(string? transientSelection)
    {
        Assert.False(DestinationSelectionPolicy.ShouldPersist(transientSelection, "headset"));
    }

    [Fact]
    public void SelectingSameDestinationIsIgnoredCaseInsensitively()
    {
        Assert.False(DestinationSelectionPolicy.ShouldPersist("HEADSET", "headset"));
    }

    [Fact]
    public void SelectingDifferentDestinationIsPersisted()
    {
        Assert.True(DestinationSelectionPolicy.ShouldPersist("hdmi", "headset"));
    }
}
