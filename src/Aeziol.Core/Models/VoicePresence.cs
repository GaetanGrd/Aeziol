namespace Aeziol.Core.Models;

public enum VoicePresenceState
{
    DiscordAbsent,
    OutOfVoice,
    Connecting,
    Connected,
    ChangingChannel,
    Reconnecting,
    Disconnected,
    AuthorizationRequired,
    Unavailable,
}

public sealed record VoiceObservation(
    string SourceId,
    VoicePresenceState State,
    DateTimeOffset ObservedAt);

public sealed record VoiceTransition(
    VoicePresenceState AggregateState,
    bool EnteredVoice = false,
    bool ExitScheduled = false,
    bool ExitCanceled = false,
    bool ExitedVoice = false,
    DateTimeOffset? ExitDueAt = null);
