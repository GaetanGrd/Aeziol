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
            UseHardwareAcceleration = defaults.UseHardwareAcceleration,
            UpdateChannel = defaults.UpdateChannel,
            StartWithWindows = defaults.StartWithWindows,
            CloseBehavior = defaults.CloseBehavior,
            ExitGracePeriodSeconds = defaults.ExitGracePeriodSeconds,
        };
    }
}
