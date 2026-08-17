using System.Text.Json;
using Aeziol.Core.Models;
using Aeziol.Infrastructure.Discord.Rpc;

namespace Aeziol.Infrastructure.Discord.Voice;

internal sealed class DiscordVoiceEventInterpreter
{
    private bool _hasEstablishedVoiceSession;

    public VoiceObservation FromInitialSelection(string sourceId, bool hasSelectedChannel, DateTimeOffset observedAt)
    {
        _hasEstablishedVoiceSession = hasSelectedChannel;
        return new VoiceObservation(
            sourceId,
            hasSelectedChannel ? VoicePresenceState.Connected : VoicePresenceState.OutOfVoice,
            observedAt);
    }

    public VoiceObservation? Interpret(string sourceId, DiscordRpcEventArgs rpcEvent, DateTimeOffset observedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentNullException.ThrowIfNull(rpcEvent);

        return rpcEvent.Name switch
        {
            "VOICE_CHANNEL_SELECT" => InterpretChannelSelection(sourceId, rpcEvent.Data, observedAt),
            "VOICE_CONNECTION_STATUS" => InterpretConnectionStatus(sourceId, rpcEvent.Data, observedAt),
            _ => null,
        };
    }

    private VoiceObservation InterpretChannelSelection(
        string sourceId,
        JsonElement data,
        DateTimeOffset observedAt)
    {
        var hasChannel = data.TryGetProperty("channel_id", out var channel)
            && channel.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
            && !string.IsNullOrWhiteSpace(channel.GetString());
        if (!hasChannel)
        {
            return new VoiceObservation(sourceId, VoicePresenceState.Disconnected, observedAt);
        }

        return new VoiceObservation(
            sourceId,
            _hasEstablishedVoiceSession ? VoicePresenceState.ChangingChannel : VoicePresenceState.Connecting,
            observedAt);
    }

    private VoiceObservation InterpretConnectionStatus(
        string sourceId,
        JsonElement data,
        DateTimeOffset observedAt)
    {
        var status = data.TryGetProperty("state", out var state) ? state.GetString() : null;
        var mapped = status switch
        {
            "VOICE_CONNECTED" => VoicePresenceState.Connected,
            "AWAITING_ENDPOINT" or
            "AUTHENTICATING" or
            "CONNECTING" or
            "CONNECTED" or
            "VOICE_CONNECTING" or
            "ICE_CHECKING" => _hasEstablishedVoiceSession
                ? VoicePresenceState.Reconnecting
                : VoicePresenceState.Connecting,
            "DISCONNECTED" or
            "VOICE_DISCONNECTED" or
            "NO_ROUTE" => VoicePresenceState.Disconnected,
            _ => VoicePresenceState.Unavailable,
        };

        if (mapped == VoicePresenceState.Connected)
        {
            _hasEstablishedVoiceSession = true;
        }

        return new VoiceObservation(sourceId, mapped, observedAt);
    }
}
