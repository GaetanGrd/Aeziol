using Aeziol.App.Settings;

namespace Aeziol.Tests.App;

public sealed class GracePeriodOptionsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(30)]
    public void SupportedValuesArePreserved(int seconds)
    {
        Assert.Equal(seconds, GracePeriodOptions.Normalize(seconds));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(31)]
    [InlineData(100)]
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
