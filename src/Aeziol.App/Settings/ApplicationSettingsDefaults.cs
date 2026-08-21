namespace Aeziol.App.Settings;

internal static class ApplicationSettingsDefaults
{
    public static AppSettings Reset(AppSettings current)
    {
        ArgumentNullException.ThrowIfNull(current);
        var defaults = new AppSettings();
        return current with
        {
            Language = defaults.Language,
            Theme = defaults.Theme,
            EnhanceContrast = defaults.EnhanceContrast,
            ReduceAnimations = defaults.ReduceAnimations,
            AmbientMusicEnabled = defaults.AmbientMusicEnabled,
            AmbientMusicVolumePercent = defaults.AmbientMusicVolumePercent,
            PauseAmbientMusicWhenUnfocused = defaults.PauseAmbientMusicWhenUnfocused,
            KeepAmbientMusicPlayingWhenHidden = defaults.KeepAmbientMusicPlayingWhenHidden,
            UseHardwareAcceleration = defaults.UseHardwareAcceleration,
            UpdateChannel = defaults.UpdateChannel,
            StartWithWindows = defaults.StartWithWindows,
            OpenHiddenAtWindowsStartup = defaults.OpenHiddenAtWindowsStartup,
            CloseBehavior = defaults.CloseBehavior,
            ExitGracePeriodSeconds = defaults.ExitGracePeriodSeconds,
        };
    }
}
