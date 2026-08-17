using Aeziol.App.Settings;

namespace Aeziol.Tests.App;

public sealed class ApplicationSettingsDefaultsTests
{
    [Fact]
    public void Reset_RestoresApplicationPreferencesAndPreservesDiscordAndRouting()
    {
        var original = new AppSettings
        {
            FirstRunCompleted = true,
            AutomationEnabled = false,
            Language = "ar",
            Theme = AeziolTheme.Chaos,
            EnhanceContrast = true,
            ReduceAnimations = true,
            AmbientMusicEnabled = true,
            AmbientMusicVolumePercent = 73,
            PauseAmbientMusicWhenUnfocused = false,
            UseHardwareAcceleration = false,
            DiscordClientId = "discord-client",
            DiscordRedirectUri = "http://127.0.0.1/custom",
            DiscordExecutablePath = @"C:\Discord\Discord.exe",
            DiscordExecutableSearchCompleted = true,
            TargetEndpointId = "headset",
            ExcludedEndpointIds = new HashSet<string>(["speakers"], StringComparer.OrdinalIgnoreCase),
            StartWithWindows = true,
            CloseBehavior = CloseBehavior.Quit,
            ExitGracePeriodSeconds = 10,
        };

        var reset = ApplicationSettingsDefaults.Reset(original);
        var defaults = new AppSettings();

        Assert.Equal(defaults.Language, reset.Language);
        Assert.Equal(defaults.Theme, reset.Theme);
        Assert.Equal(defaults.EnhanceContrast, reset.EnhanceContrast);
        Assert.Equal(defaults.ReduceAnimations, reset.ReduceAnimations);
        Assert.Equal(defaults.AmbientMusicEnabled, reset.AmbientMusicEnabled);
        Assert.Equal(defaults.AmbientMusicVolumePercent, reset.AmbientMusicVolumePercent);
        Assert.Equal(defaults.PauseAmbientMusicWhenUnfocused, reset.PauseAmbientMusicWhenUnfocused);
        Assert.Equal(defaults.UseHardwareAcceleration, reset.UseHardwareAcceleration);
        Assert.Equal(defaults.StartWithWindows, reset.StartWithWindows);
        Assert.Equal(defaults.CloseBehavior, reset.CloseBehavior);
        Assert.Equal(defaults.ExitGracePeriodSeconds, reset.ExitGracePeriodSeconds);

        Assert.True(reset.FirstRunCompleted);
        Assert.False(reset.AutomationEnabled);
        Assert.Equal(original.DiscordClientId, reset.DiscordClientId);
        Assert.Equal(original.DiscordRedirectUri, reset.DiscordRedirectUri);
        Assert.Equal(original.DiscordExecutablePath, reset.DiscordExecutablePath);
        Assert.True(reset.DiscordExecutableSearchCompleted);
        Assert.Equal(original.TargetEndpointId, reset.TargetEndpointId);
        Assert.Contains("speakers", reset.ExcludedEndpointIds);
    }

    [Fact]
    public void Reset_RejectsNullSettings()
    {
        Assert.Throws<ArgumentNullException>(() => ApplicationSettingsDefaults.Reset(null!));
    }
}
