using Aeziol.App.Services;

namespace Aeziol.Tests.App;

public sealed class AmbientMusicServiceTests
{
    [Theory]
    [InlineData(false, false, false, true, true, false)]
    [InlineData(true, false, false, false, true, false)]
    [InlineData(true, false, false, true, false, false)]
    [InlineData(true, false, true, true, false, true)]
    [InlineData(true, true, false, false, false, false)]
    [InlineData(true, true, true, false, false, true)]
    public void ShouldPlay_RequiresBothPositiveOptionsWhenHiddenAndUnfocused(
        bool enabled,
        bool keepPlayingWhenHidden,
        bool keepPlayingWhenUnfocused,
        bool applicationVisible,
        bool applicationFocused,
        bool expected)
    {
        Assert.Equal(
            expected,
            AmbientMusicService.ShouldPlay(
                enabled,
                keepPlayingWhenHidden,
                keepPlayingWhenUnfocused,
                applicationVisible,
                applicationFocused));
    }
}
