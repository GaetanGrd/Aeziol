using System.Windows.Media;
using Aeziol.App.Settings;

namespace Aeziol.App.Services;

public sealed class AmbientMusicService : IDisposable
{
    private readonly IAmbientMediaPlayer _player;
    private readonly Uri _trackUri;
    private bool _opened;
    private bool _resumeAfterOpen;
    private TimeSpan _resumePosition;
    private bool _enabled;
    private bool _keepPlayingWhenHidden;
    private bool _keepPlayingWhenUnfocused;
    private bool _applicationVisible;
    private bool _applicationFocused;

    public AmbientMusicService(string trackPath)
        : this(trackPath, new WpfAmbientMediaPlayer())
    {
    }

    internal AmbientMusicService(string trackPath, IAmbientMediaPlayer player)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(trackPath);
        ArgumentNullException.ThrowIfNull(player);
        _player = player;
        _trackUri = new Uri(Path.GetFullPath(trackPath), UriKind.Absolute);
        _player.MediaOpened += OnMediaOpened;
        _player.MediaEnded += OnMediaEnded;
        _player.MediaFailed += OnMediaFailed;
    }

    public bool IsAvailable => File.Exists(_trackUri.LocalPath);

    public void Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _enabled = settings.AmbientMusicEnabled && IsAvailable;
        _keepPlayingWhenHidden = settings.KeepAmbientMusicPlayingWhenHidden;
        _keepPlayingWhenUnfocused = settings.KeepAmbientMusicPlayingWhenUnfocused;
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
                _keepPlayingWhenUnfocused,
                _applicationVisible,
                _applicationFocused))
        {
            if (_enabled)
            {
                _player.Pause();
            }
            else
            {
                ClosePlayer(preservePosition: true);
            }

            return;
        }

        if (!_opened)
        {
            _resumeAfterOpen = _resumePosition > TimeSpan.Zero;
            _player.Open(_trackUri);
            _opened = true;
            if (_resumeAfterOpen)
            {
                return;
            }
        }

        _player.Play();
    }

    public void Dispose()
    {
        _enabled = false;
        _player.MediaOpened -= OnMediaOpened;
        _player.MediaEnded -= OnMediaEnded;
        _player.MediaFailed -= OnMediaFailed;
        ClosePlayer(preservePosition: false);
        GC.SuppressFinalize(this);
    }

    private void OnMediaOpened(object? sender, EventArgs e)
    {
        if (!_opened)
        {
            return;
        }

        if (_resumeAfterOpen)
        {
            _player.Position = _resumePosition;
            _resumeAfterOpen = false;
        }

        if (ShouldPlay(
                _enabled,
                _keepPlayingWhenHidden,
                _keepPlayingWhenUnfocused,
                _applicationVisible,
                _applicationFocused))
        {
            _player.Play();
        }
    }

    private void OnMediaEnded(object? sender, EventArgs e)
    {
        if (!ShouldPlay(
                _enabled,
                _keepPlayingWhenHidden,
                _keepPlayingWhenUnfocused,
                _applicationVisible,
                _applicationFocused))
        {
            return;
        }

        _resumePosition = TimeSpan.Zero;
        _player.Position = TimeSpan.Zero;
        _player.Play();
    }

    private void OnMediaFailed(object? sender, ExceptionEventArgs e)
    {
        _enabled = false;
        ClosePlayer(preservePosition: false);
    }

    private void ClosePlayer(bool preservePosition)
    {
        if (!_opened)
        {
            if (!preservePosition)
            {
                _resumePosition = TimeSpan.Zero;
            }

            return;
        }

        _resumePosition = preservePosition ? _player.Position : TimeSpan.Zero;
        _resumeAfterOpen = false;
        _player.Close();
        _opened = false;
    }

    internal static bool ShouldPlay(
        bool enabled,
        bool keepPlayingWhenHidden,
        bool keepPlayingWhenUnfocused,
        bool applicationVisible,
        bool applicationFocused) =>
        enabled
        && (applicationFocused || keepPlayingWhenUnfocused)
        && (applicationVisible || keepPlayingWhenHidden);
}

internal interface IAmbientMediaPlayer
{
    event EventHandler? MediaOpened;

    event EventHandler? MediaEnded;

    event EventHandler<ExceptionEventArgs>? MediaFailed;

    double Volume { get; set; }

    bool IsMuted { get; set; }

    TimeSpan Position { get; set; }

    void Open(Uri source);

    void Play();

    void Pause();

    void Close();
}

internal sealed class WpfAmbientMediaPlayer : IAmbientMediaPlayer
{
    private readonly MediaPlayer _player = new();

    public event EventHandler? MediaOpened
    {
        add => _player.MediaOpened += value;
        remove => _player.MediaOpened -= value;
    }

    public event EventHandler? MediaEnded
    {
        add => _player.MediaEnded += value;
        remove => _player.MediaEnded -= value;
    }

    public event EventHandler<ExceptionEventArgs>? MediaFailed
    {
        add => _player.MediaFailed += value;
        remove => _player.MediaFailed -= value;
    }

    public double Volume
    {
        get => _player.Volume;
        set => _player.Volume = value;
    }

    public bool IsMuted
    {
        get => _player.IsMuted;
        set => _player.IsMuted = value;
    }

    public TimeSpan Position
    {
        get => _player.Position;
        set => _player.Position = value;
    }

    public void Open(Uri source) => _player.Open(source);

    public void Play() => _player.Play();

    public void Pause() => _player.Pause();

    public void Close() => _player.Close();
}
