using Aeziol.App.Settings;

namespace Aeziol.Tests.App;

public sealed class GracePeriodOptionsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SupportedValuesArePreserved(int seconds)
    {
        Assert.Equal(seconds, GracePeriodOptions.Normalize(seconds));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(6)]
    [InlineData(10)]
    [InlineData(30)]
    public void LegacyOrInvalidValuesMigrateToRecommendedDelay(int seconds)
    {
        Assert.Equal(1, GracePeriodOptions.Normalize(seconds));
    }

    [Fact]
    public void NewSettingsUseRecommendedDelay()
    {
        Assert.Equal(1, new AppSettings().ExitGracePeriodSeconds);
    }
}
