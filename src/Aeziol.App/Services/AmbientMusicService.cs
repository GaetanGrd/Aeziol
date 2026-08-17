using System.Windows.Media;
using Aeziol.App.Settings;

namespace Aeziol.App.Services;

public sealed class AmbientMusicService : IDisposable
{
    private readonly MediaPlayer _player = new();
    private readonly Uri _trackUri;
    private bool _opened;
    private bool _enabled;
    private bool _pauseWhenUnfocused;
    private bool _applicationActive;

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
        _pauseWhenUnfocused = settings.PauseAmbientMusicWhenUnfocused;
        _player.Volume = Math.Clamp(settings.AmbientMusicVolumePercent, 0, 100) / 100d;
        _player.IsMuted = false;
        UpdatePlayback();
    }

    public void SetApplicationActive(bool isActive)
    {
        if (_applicationActive == isActive)
        {
            return;
        }

        _applicationActive = isActive;
        UpdatePlayback();
    }

    private void UpdatePlayback()
    {
        if (!ShouldPlay(_enabled, _pauseWhenUnfocused, _applicationActive))
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
        if (!ShouldPlay(_enabled, _pauseWhenUnfocused, _applicationActive))
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

    internal static bool ShouldPlay(bool enabled, bool pauseWhenUnfocused, bool applicationActive) =>
        enabled && (!pauseWhenUnfocused || applicationActive);
}
