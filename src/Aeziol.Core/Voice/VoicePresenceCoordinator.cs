using Aeziol.Core.Models;

namespace Aeziol.Core.Voice;

public sealed class VoicePresenceCoordinator
{
    private static readonly TimeSpan MinimumExitDebounce = TimeSpan.FromMilliseconds(750);
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, VoicePresenceState> _sources = new(StringComparer.Ordinal);
    private readonly TimeSpan _exitGracePeriod;
    private bool _sessionActive;
    private DateTimeOffset? _exitDueAt;

    public VoicePresenceCoordinator(TimeSpan exitGracePeriod)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(exitGracePeriod, TimeSpan.Zero);
        _exitGracePeriod = exitGracePeriod;
    }

    public VoicePresenceState AggregateState { get; private set; } = VoicePresenceState.DiscordAbsent;

    public bool IsSessionActive
    {
        get
        {
            lock (_syncRoot)
            {
                return _sessionActive;
            }
        }
    }

    public VoiceTransition Apply(VoiceObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentException.ThrowIfNullOrWhiteSpace(observation.SourceId);

        lock (_syncRoot)
        {
            return ApplyUnderLock(observation);
        }
    }

    public VoiceTransition Tick(DateTimeOffset now)
    {
        lock (_syncRoot)
        {
            if (_sessionActive && _exitDueAt is { } dueAt && now >= dueAt && !HasActiveVoice())
            {
                _sessionActive = false;
                _exitDueAt = null;
                AggregateState = CalculateAggregate();
                return new VoiceTransition(AggregateState, ExitedVoice: true);
            }

            return new VoiceTransition(AggregateState, ExitDueAt: _exitDueAt);
        }
    }

    public void RemoveSource(string sourceId, DateTimeOffset observedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        lock (_syncRoot)
        {
            _sources.Remove(sourceId);

            if (_sessionActive && !HasActiveVoice() && !_exitDueAt.HasValue)
            {
                _exitDueAt = observedAt + EffectiveExitDelay;
            }

            AggregateState = CalculateAggregate();
        }
    }

    private TimeSpan EffectiveExitDelay => _exitGracePeriod > MinimumExitDebounce
        ? _exitGracePeriod
        : MinimumExitDebounce;

    private VoiceTransition ApplyUnderLock(VoiceObservation observation)
    {

        _sources[observation.SourceId] = observation.State;
        var aggregate = CalculateAggregate();
        var hasActiveVoice = HasActiveVoice();

        if (hasActiveVoice)
        {
            var canceled = _exitDueAt.HasValue;
            _exitDueAt = null;
            var entered = !_sessionActive;
            _sessionActive = true;
            AggregateState = aggregate;
            return new VoiceTransition(aggregate, EnteredVoice: entered, ExitCanceled: canceled);
        }

        if (_sessionActive && !_exitDueAt.HasValue)
        {
            _exitDueAt = observation.ObservedAt + EffectiveExitDelay;
            AggregateState = aggregate;
            return new VoiceTransition(aggregate, ExitScheduled: true, ExitDueAt: _exitDueAt);
        }

        AggregateState = aggregate;
        return new VoiceTransition(aggregate, ExitDueAt: _exitDueAt);
    }

    private bool HasActiveVoice() => _sources.Values.Any(IsVoiceContinuityState);

    private VoicePresenceState CalculateAggregate()
    {
        if (_sources.Count == 0 || _sources.Values.All(state => state == VoicePresenceState.DiscordAbsent))
        {
            return VoicePresenceState.DiscordAbsent;
        }

        if (_sources.Values.Any(state => state == VoicePresenceState.Connected))
        {
            return VoicePresenceState.Connected;
        }

        if (_sources.Values.Any(state => state == VoicePresenceState.ChangingChannel))
        {
            return VoicePresenceState.ChangingChannel;
        }

        if (_sources.Values.Any(state => state == VoicePresenceState.Reconnecting))
        {
            return VoicePresenceState.Reconnecting;
        }

        if (_sources.Values.Any(state => state == VoicePresenceState.Connecting))
        {
            return VoicePresenceState.Connecting;
        }

        if (_sources.Values.Any(state => state == VoicePresenceState.AuthorizationRequired))
        {
            return VoicePresenceState.AuthorizationRequired;
        }

        if (_sources.Values.Any(state => state == VoicePresenceState.Unavailable))
        {
            return VoicePresenceState.Unavailable;
        }

        if (_sources.Values.Any(state => state == VoicePresenceState.OutOfVoice))
        {
            return VoicePresenceState.OutOfVoice;
        }

        return VoicePresenceState.Disconnected;
    }

    private static bool IsVoiceContinuityState(VoicePresenceState state) => state is
        VoicePresenceState.Connected or
        VoicePresenceState.ChangingChannel or
        VoicePresenceState.Reconnecting;
}
