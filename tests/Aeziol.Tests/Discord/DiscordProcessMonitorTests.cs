using Aeziol.Infrastructure.Discord.Processes;

namespace Aeziol.Tests.Discord;

public sealed class DiscordProcessMonitorTests
{
    [Fact]
    public void ConfiguredExecutableName_IsRecognizedAsDiscord()
    {
        using var monitor = new DiscordProcessMonitor(@"C:\Portable\Discord\MyDiscord.exe");

        var recognized = monitor.TryGetEdition("MyDiscord.exe", out var edition);

        Assert.True(recognized);
        Assert.Equal(DiscordEdition.Stable, edition);
    }

    [Fact]
    public void UnrelatedExecutable_IsNotRecognized()
    {
        using var monitor = new DiscordProcessMonitor(@"C:\Portable\Discord\MyDiscord.exe");

        Assert.False(monitor.TryGetEdition("AnotherApp.exe", out _));
    }
}
