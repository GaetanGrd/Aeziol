using Aeziol.App.Services;
using Aeziol.App.Settings;
using System.Windows.Media;

namespace Aeziol.Tests.App;

public sealed class AmbientMusicServiceTests
{
    private readonly string _trackPath = typeof(AmbientMusicServiceTests).Assembly.Location;

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

    [Fact]
    public void DisabledMusic_ClosesPlayerAndResumesAtPreviousPosition()
    {
        var player = new FakeAmbientMediaPlayer();
        using var service = new AmbientMusicService(_trackPath, player);
        service.SetApplicationVisible(true);
        service.SetApplicationFocused(true);

        service.Apply(CreateSettings(enabled: true));

        Assert.Equal(1, player.OpenCount);
        Assert.Equal(1, player.PlayCount);

        player.Position = TimeSpan.FromSeconds(37);
        service.Apply(CreateSettings(enabled: false));

        Assert.Equal(1, player.CloseCount);

        service.Apply(CreateSettings(enabled: true));

        Assert.Equal(2, player.OpenCount);
        Assert.Equal(1, player.PlayCount);

        player.RaiseMediaOpened();

        Assert.Equal(TimeSpan.FromSeconds(37), player.Position);
        Assert.Equal(2, player.PlayCount);
    }

    [Fact]
    public void UnfocusedMusic_PausesWithoutClosingPlayer()
    {
        var player = new FakeAmbientMediaPlayer();
        using var service = new AmbientMusicService(_trackPath, player);
        service.SetApplicationVisible(true);
        service.SetApplicationFocused(true);
        service.Apply(CreateSettings(enabled: true));

        service.SetApplicationFocused(false);

        Assert.Equal(1, player.PauseCount);
        Assert.Equal(0, player.CloseCount);
    }

    private static AppSettings CreateSettings(bool enabled) => new()
    {
        AmbientMusicEnabled = enabled,
    };

    private sealed class FakeAmbientMediaPlayer : IAmbientMediaPlayer
    {
        public event EventHandler? MediaOpened;

        public event EventHandler? MediaEnded
        {
            add { }
            remove { }
        }

        public event EventHandler<ExceptionEventArgs>? MediaFailed
        {
            add { }
            remove { }
        }

        public double Volume { get; set; }

        public bool IsMuted { get; set; }

        public TimeSpan Position { get; set; }

        public int OpenCount { get; private set; }

        public int PlayCount { get; private set; }

        public int PauseCount { get; private set; }

        public int CloseCount { get; private set; }

        public void Open(Uri source) => OpenCount++;

        public void Play() => PlayCount++;

        public void Pause() => PauseCount++;

        public void Close() => CloseCount++;

        public void RaiseMediaOpened() => MediaOpened?.Invoke(this, EventArgs.Empty);
    }
}
