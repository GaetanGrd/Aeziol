using System.Windows.Media;
using Aeziol.App.Settings;

namespace Aeziol.App.Services;

public sealed class AmbientMusicService : IDisposable
{
    private readonly MediaPlayer _player = new();
    private readonly Uri _trackUri;
    private bool _opened;
    private bool _enabled;
    private bool _keepPlayingWhenHidden;
    private bool _pauseWhenUnfocused;
    private bool _applicationVisible;
    private bool _applicationFocused;

    public AmbientMusicService(string trackPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackPath);
        _trackUri = new Uri(Path.GetFullPath(trackPath), UriKind.Absolute);
        _player.MediaEnded += OnMediaEnded;
        _player.MediaFailed += OnMediaFailed;
    }

    public bool IsAvailable => File.Exists(_trackUri.LocalPath);

    public void Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _enabled = settings.AmbientMusicEnabled && IsAvailable;
        _keepPlayingWhenHidden = settings.KeepAmbientMusicPlayingWhenHidden;
        _pauseWhenUnfocused = settings.PauseAmbientMusicWhenUnfocused;
        _player.Volume = Math.Clamp(settings.AmbientMusicVolumePercent, 0, 100) / 100d;
        _player.IsMuted = false;
        UpdatePlayback();
    }

    public void SetApplicationVisible(bool isVisible)
    {
        if (_applicationVisible == isVisible)
        {
            return;
        }

        _applicationVisible = isVisible;
        UpdatePlayback();
    }

    public void SetApplicationFocused(bool isFocused)
    {
        if (_applicationFocused == isFocused)
        {
            return;
        }

        _applicationFocused = isFocused;
        UpdatePlayback();
    }

    private void UpdatePlayback()
    {
        if (!ShouldPlay(
                _enabled,
                _keepPlayingWhenHidden,
                _pauseWhenUnfocused,
                _applicationVisible,
                _applicationFocused))
        {
            _player.Pause();
            return;
        }

        if (!_opened)
        {
            _player.Open(_trackUri);
            _opened = true;
        }

        _player.Play();
    }

    public void Dispose()
    {
        _enabled = false;
        _player.MediaEnded -= OnMediaEnded;
        _player.MediaFailed -= OnMediaFailed;
        _player.Close();
        GC.SuppressFinalize(this);
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        if (!ShouldPlay(
                _enabled,
                _keepPlayingWhenHidden,
                _pauseWhenUnfocused,
                _applicationVisible,
                _applicationFocused))
        {
            return;
        }

        _player.Position = TimeSpan.Zero;
        _player.Play();
    }

    private void OnMediaFailed(object? sender, ExceptionEventArgs e)
    {
        _enabled = false;
        _player.Stop();
    }

    internal static bool ShouldPlay(
        bool enabled,
        bool keepPlayingWhenHidden,
        bool pauseWhenUnfocused,
        bool applicationVisible,
        bool applicationFocused) =>
        enabled
        && (!pauseWhenUnfocused || applicationFocused)
        && (applicationVisible || keepPlayingWhenHidden);
}
