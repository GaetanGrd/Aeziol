namespace Aeziol.App.Settings;

public enum WritingRegister
{
    Standard,
}

public enum CloseBehavior
{
    Ask,
    MinimizeToTray,
    Quit,
}

public enum AeziolTheme
{
    Elgo,
    Elna,
    Ilyors,
    Cherry,
    Yuna,
    Lilith,
    Chaos,
}

public enum UpdateChannel
{
    Stable,
    Beta,
}

public sealed record AppSettings
{
    public const int CurrentSchemaVersion = 4;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    public bool FirstRunCompleted { get; init; }

    public bool AutomationEnabled { get; init; } = true;

    public string Language { get; init; } = "en";

    public AeziolTheme Theme { get; init; } = AeziolTheme.Elgo;

    public bool EnhanceContrast { get; init; }

    public bool ReduceAnimations { get; init; }

    public bool AmbientMusicEnabled { get; init; }

    public int AmbientMusicVolumePercent { get; init; } = 8;

    public bool PauseAmbientMusicWhenUnfocused { get; init; } = true;

    public bool KeepAmbientMusicPlayingWhenHidden { get; init; }

    public bool UseHardwareAcceleration { get; init; } = true;

    public UpdateChannel UpdateChannel { get; init; } = UpdateChannel.Stable;

    public string DiscordClientId { get; init; } = "1538505326641414154";

    public string DiscordRedirectUri { get; init; } = "http://127.0.0.1/aeziol-discord-oauth";

    public string? DiscordExecutablePath { get; init; }

    public bool DiscordExecutableSearchCompleted { get; init; }

    public string? TargetEndpointId { get; init; }

    public HashSet<string> ExcludedEndpointIds { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public bool StartWithWindows { get; init; }

    public bool OpenHiddenAtWindowsStartup { get; init; }

    public CloseBehavior CloseBehavior { get; init; } = CloseBehavior.Ask;

    public int ExitGracePeriodSeconds { get; init; } = 1;

}
