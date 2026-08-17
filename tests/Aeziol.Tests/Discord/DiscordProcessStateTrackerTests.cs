using Aeziol.Infrastructure.Discord.Processes;

namespace Aeziol.Tests.Discord;

public sealed class DiscordProcessStateTrackerTests
{
    [Fact]
    public void MultipleProcesses_OnlyEmitEditionAvailabilityTransitions()
    {
        var tracker = new DiscordProcessStateTracker();

        var started = tracker.Add(DiscordEdition.Stable, 10);
        var auxiliaryStarted = tracker.Add(DiscordEdition.Stable, 11);
        var auxiliaryStopped = tracker.Remove(DiscordEdition.Stable, 10);
        var stopped = tracker.Remove(DiscordEdition.Stable, 11);

        Assert.NotNull(started);
        Assert.True(started.IsRunning);
        Assert.Null(auxiliaryStarted);
        Assert.Null(auxiliaryStopped);
        Assert.NotNull(stopped);
        Assert.False(stopped.IsRunning);
    }

    [Fact]
    public void ReplaceAll_TracksEditionsIndependently()
    {
        var tracker = new DiscordProcessStateTracker();

        var changes = tracker.ReplaceAll(
        [
            (DiscordEdition.Stable, 10),
            (DiscordEdition.Stable, 11),
            (DiscordEdition.Canary, 20),
        ]);

        Assert.Equal(2, changes.Count);
        Assert.Contains(changes, snapshot => snapshot.SourceId == "discord-stable" && snapshot.ProcessCount == 2);
        Assert.Contains(changes, snapshot => snapshot.SourceId == "discord-canary" && snapshot.ProcessCount == 1);
    }
}
