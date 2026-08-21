using Aeziol.App.Services;

namespace Aeziol.Tests.App;

public sealed class AmbientMusicServiceTests
{
    [Theory]
    [InlineData(false, false, false, true, true, false)]
    [InlineData(true, false, false, false, true, false)]
    [InlineData(true, false, false, true, false, true)]
    [InlineData(true, false, true, true, false, false)]
    [InlineData(true, true, false, false, false, true)]
    [InlineData(true, true, true, false, false, false)]
    public void ShouldPlay_GivesFocusPausePriorityOverHiddenPlayback(
        bool enabled,
        bool keepPlayingWhenHidden,
        bool pauseWhenUnfocused,
        bool applicationVisible,
        bool applicationFocused,
        bool expected)
    {
        Assert.Equal(
            expected,
            AmbientMusicService.ShouldPlay(
                enabled,
                keepPlayingWhenHidden,
                pauseWhenUnfocused,
                applicationVisible,
                applicationFocused));
    }
}
