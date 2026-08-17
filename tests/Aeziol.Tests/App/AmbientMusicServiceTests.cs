using Aeziol.App.Services;

namespace Aeziol.Tests.App;

public sealed class AmbientMusicServiceTests
{
    [Theory]
    [InlineData(false, true, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, false, true)]
    public void ShouldPlay_RespectsEnabledAndForegroundPreferences(
        bool enabled,
        bool pauseWhenUnfocused,
        bool applicationActive,
        bool expected)
    {
        Assert.Equal(
            expected,
            AmbientMusicService.ShouldPlay(enabled, pauseWhenUnfocused, applicationActive));
    }
}
