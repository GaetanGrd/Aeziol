using System.Text.Json;
using Aeziol.Core.Models;
using Aeziol.Infrastructure.Discord.Rpc;
using Aeziol.Infrastructure.Discord.Voice;

namespace Aeziol.Tests.Discord;

public sealed class DiscordVoiceEventInterpreterTests
{
    private static readonly DateTimeOffset ObservedAt = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("CONNECTING")]
    [InlineData("CONNECTED")]
    [InlineData("VOICE_CONNECTING")]
    [InlineData("ICE_CHECKING")]
    public void ConnectionStatus_OnlyVoiceConnectedMeansConnected(string state)
    {
        var interpreter = new DiscordVoiceEventInterpreter();
        var observation = Interpret(
            interpreter,
            "VOICE_CONNECTION_STATUS",
            $"{{\"state\":\"{state}\"}}");

        Assert.NotNull(observation);
        Assert.Equal(VoicePresenceState.Connecting, observation.State);
    }

    [Fact]
    public void VoiceConnected_MarksEstablishedVoiceSession()
    {
        var interpreter = new DiscordVoiceEventInterpreter();

        var connected = Interpret(interpreter, "VOICE_CONNECTION_STATUS", "{\"state\":\"VOICE_CONNECTED\"}");
        var channelChange = Interpret(interpreter, "VOICE_CHANNEL_SELECT", "{\"channel_id\":\"private-id\"}");

        Assert.NotNull(connected);
        Assert.Equal(VoicePresenceState.Connected, connected.State);
        Assert.NotNull(channelChange);
        Assert.Equal(VoicePresenceState.ChangingChannel, channelChange.State);
    }

    [Fact]
    public void ChannelSelection_DoesNotExposeDiscordIdentifiers()
    {
        var interpreter = new DiscordVoiceEventInterpreter();
        const string privateChannelId = "123456789012345678";

        var observation = Interpret(
            interpreter,
            "VOICE_CHANNEL_SELECT",
            $"{{\"channel_id\":\"{privateChannelId}\",\"guild_id\":\"987654321098765432\"}}");

        Assert.NotNull(observation);
        Assert.Equal(VoicePresenceState.Connecting, observation.State);
        Assert.DoesNotContain(privateChannelId, observation.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyChannelSelection_MeansDisconnected()
    {
        var interpreter = new DiscordVoiceEventInterpreter();
        var observation = Interpret(interpreter, "VOICE_CHANNEL_SELECT", "{\"channel_id\":null}");

        Assert.NotNull(observation);
        Assert.Equal(VoicePresenceState.Disconnected, observation.State);
    }

    private static VoiceObservation? Interpret(
        DiscordVoiceEventInterpreter interpreter,
        string eventName,
        string json)
    {
        using var document = JsonDocument.Parse(json);
        return interpreter.Interpret(
            "discord-stable",
            new DiscordRpcEventArgs(eventName, document.RootElement),
            ObservedAt);
    }
}
