using Aeziol.Core.Models;
using Aeziol.Core.Voice;

namespace Aeziol.Tests.Voice;

public sealed class VoicePresenceCoordinatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Connecting_DoesNotEnterVoice()
    {
        var subject = new VoicePresenceCoordinator(TimeSpan.FromSeconds(2));

        var transition = subject.Apply(new VoiceObservation("stable", VoicePresenceState.Connecting, Start));

        Assert.False(transition.EnteredVoice);
        Assert.False(subject.IsSessionActive);
    }

    [Fact]
    public void Connected_EntersOnlyOnce()
    {
        var subject = new VoicePresenceCoordinator(TimeSpan.FromSeconds(2));

        var first = subject.Apply(new VoiceObservation("stable", VoicePresenceState.Connected, Start));
        var second = subject.Apply(new VoiceObservation("stable", VoicePresenceState.Connected, Start.AddMilliseconds(100)));

        Assert.True(first.EnteredVoice);
        Assert.False(second.EnteredVoice);
        Assert.True(subject.IsSessionActive);
    }

    [Fact]
    public void ChannelChange_PreservesSession()
    {
        var subject = new VoicePresenceCoordinator(TimeSpan.FromSeconds(2));
        subject.Apply(new VoiceObservation("stable", VoicePresenceState.Connected, Start));

        var transition = subject.Apply(new VoiceObservation("stable", VoicePresenceState.ChangingChannel, Start.AddSeconds(1)));

        Assert.False(transition.ExitScheduled);
        Assert.True(subject.IsSessionActive);
    }

    [Fact]
    public void ReconnectWithinGrace_CancelsExit()
    {
        var subject = new VoicePresenceCoordinator(TimeSpan.FromSeconds(2));
        subject.Apply(new VoiceObservation("stable", VoicePresenceState.Connected, Start));
        var disconnected = subject.Apply(new VoiceObservation("stable", VoicePresenceState.Disconnected, Start.AddSeconds(1)));

        var reconnected = subject.Apply(new VoiceObservation("stable", VoicePresenceState.Reconnecting, Start.AddSeconds(2)));

        Assert.True(disconnected.ExitScheduled);
        Assert.True(reconnected.ExitCanceled);
        Assert.False(reconnected.ExitedVoice);
        Assert.True(subject.IsSessionActive);
    }

    [Fact]
    public void DisconnectAfterGrace_ExitsOnce()
    {
        var subject = new VoicePresenceCoordinator(TimeSpan.FromSeconds(2));
        subject.Apply(new VoiceObservation("stable", VoicePresenceState.Connected, Start));
        subject.Apply(new VoiceObservation("stable", VoicePresenceState.Disconnected, Start.AddSeconds(1)));

        var beforeDeadline = subject.Tick(Start.AddSeconds(2));
        var atDeadline = subject.Tick(Start.AddSeconds(3));
        var afterward = subject.Tick(Start.AddSeconds(4));

        Assert.False(beforeDeadline.ExitedVoice);
        Assert.True(atDeadline.ExitedVoice);
        Assert.False(afterward.ExitedVoice);
        Assert.False(subject.IsSessionActive);
    }

    [Fact]
    public void ZeroGrace_DoesNotRestoreDuringTransientDiscordDisconnectOnJoin()
    {
        var subject = new VoicePresenceCoordinator(TimeSpan.Zero);
        subject.Apply(new VoiceObservation("stable", VoicePresenceState.Connected, Start));
        var disconnected = subject.Apply(new VoiceObservation(
            "stable",
            VoicePresenceState.Disconnected,
            Start.AddMilliseconds(100)));

        var tooEarly = subject.Tick(Start.AddMilliseconds(300));
        var reconnected = subject.Apply(new VoiceObservation(
            "stable",
            VoicePresenceState.Connected,
            Start.AddMilliseconds(400)));
        var later = subject.Tick(Start.AddSeconds(2));

        Assert.True(disconnected.ExitScheduled);
        Assert.False(tooEarly.ExitedVoice);
        Assert.True(reconnected.ExitCanceled);
        Assert.False(later.ExitedVoice);
        Assert.True(subject.IsSessionActive);
    }

    [Fact]
    public void ZeroGrace_StillExitsQuicklyWhenDisconnectPersists()
    {
        var subject = new VoicePresenceCoordinator(TimeSpan.Zero);
        subject.Apply(new VoiceObservation("stable", VoicePresenceState.Connected, Start));
        subject.Apply(new VoiceObservation(
            "stable",
            VoicePresenceState.Disconnected,
            Start.AddMilliseconds(100)));

        var exited = subject.Tick(Start.AddSeconds(1));

        Assert.True(exited.ExitedVoice);
        Assert.False(subject.IsSessionActive);
    }

    [Fact]
    public void MultipleClients_ExitOnlyAfterAllAreDisconnected()
    {
        var subject = new VoicePresenceCoordinator(TimeSpan.FromSeconds(2));
        subject.Apply(new VoiceObservation("stable", VoicePresenceState.Connected, Start));
        subject.Apply(new VoiceObservation("canary", VoicePresenceState.Connected, Start));
        subject.Apply(new VoiceObservation("stable", VoicePresenceState.Disconnected, Start.AddSeconds(1)));

        var stillActive = subject.Tick(Start.AddSeconds(10));
        var scheduled = subject.Apply(new VoiceObservation("canary", VoicePresenceState.Disconnected, Start.AddSeconds(11)));
        var exited = subject.Tick(Start.AddSeconds(13));

        Assert.False(stillActive.ExitedVoice);
        Assert.True(scheduled.ExitScheduled);
        Assert.True(exited.ExitedVoice);
    }
}
