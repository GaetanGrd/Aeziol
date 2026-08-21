using Aeziol.App.Services;

namespace Aeziol.Tests.App;

public sealed class AmbientMusicServiceTests
{
    [Theory]
    [InlineData(false, false, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    public void ShouldPlay_RespectsEnabledVisibilityAndHiddenPlaybackPreference(
        bool enabled,
        bool keepPlayingWhenHidden,
        bool applicationVisible,
        bool expected)
    {
        Assert.Equal(
            expected,
            AmbientMusicService.ShouldPlay(enabled, keepPlayingWhenHidden, applicationVisible));
    }
}
