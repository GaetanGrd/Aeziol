using System.Windows.Media;
using Aeziol.Core.Models;

namespace Aeziol.App.Appearance;

internal enum VoicePresenceEntryMotion
{
    Settle,
    Converge,
    Slide,
    Return,
}

internal sealed record VoicePresenceVisual(
    VoicePresenceState State,
    string AssetFileName,
    string PathData,
    string LocalizationKey,
    string StrokeBrushKey,
    VoicePresenceEntryMotion EntryMotion)
{
    private Geometry? _geometry;

    public Geometry Geometry => _geometry ??= CreateGeometry(PathData);

    public static IReadOnlyList<VoicePresenceVisual> All { get; } =
    [
        new(
            VoicePresenceState.DiscordAbsent,
            "discord-absent.svg",
            "M 20,14 C 25,11 32,10 36,11 M 42,11 C 46,11 49,12 52,14 C 55,18 57,23 58,28 M 58,35 C 58,38 58,41 57,43 C 53,46 49,48 45,49 L 42,44 M 30,44 L 27,49 C 23,48 19,46 15,43 C 13,39 12,35 13,31 M 14,24 C 15,20 17,17 20,14 M 27,30 A 3,3 0 1 0 27,36 A 3,3 0 1 0 27,30 M 45,30 A 3,3 0 1 0 45,36 A 3,3 0 1 0 45,30",
            "status-discord-absent",
            "AeziolDim",
            VoicePresenceEntryMotion.Settle),
        new(
            VoicePresenceState.OutOfVoice,
            "discord-out-of-voice.svg",
            "M 20,14 C 25,11 47,11 52,14 C 58,22 61,35 57,43 C 53,46 49,48 45,49 L 42,44 C 38,46 34,46 30,44 L 27,49 C 23,48 19,46 15,43 C 11,35 14,22 20,14 Z M 27,30 A 3,3 0 1 0 27,36 A 3,3 0 1 0 27,30 M 45,30 A 3,3 0 1 0 45,36 A 3,3 0 1 0 45,30",
            "status-out-of-voice",
            "DiscordBlurple",
            VoicePresenceEntryMotion.Settle),
        new(
            VoicePresenceState.Connecting,
            "discord-connecting.svg",
            "M 34,12 C 28,11 23,12 20,14 C 15,21 13,31 15,40 C 19,44 23,47 27,48 L 30,43 C 31,44 33,45 34,45 M 38,12 C 44,11 49,12 52,14 C 57,21 59,31 57,40 C 53,44 49,47 45,48 L 42,43 C 41,44 39,45 38,45 M 30,25 L 34,29 L 30,33 M 42,25 L 38,29 L 42,33",
            "status-connecting",
            "DiscordBlurple",
            VoicePresenceEntryMotion.Converge),
        new(
            VoicePresenceState.Connected,
            "discord-connected.svg",
            "M 20,14 C 25,11 47,11 52,14 C 58,22 61,35 57,43 C 53,46 49,48 45,49 L 42,44 C 38,46 34,46 30,44 L 27,49 C 23,48 19,46 15,43 C 11,35 14,22 20,14 Z M 27,30 A 3,3 0 1 0 27,36 A 3,3 0 1 0 27,30 M 45,30 A 3,3 0 1 0 45,36 A 3,3 0 1 0 45,30 M 10,24 C 6,28 6,36 10,40 M 62,24 C 66,28 66,36 62,40",
            "status-connected",
            "DiscordBlurple",
            VoicePresenceEntryMotion.Settle),
        new(
            VoicePresenceState.ChangingChannel,
            "discord-changing-channel.svg",
            "M 20,14 C 25,11 47,11 52,14 C 58,22 61,35 57,43 C 53,46 49,48 45,49 L 42,44 C 38,46 34,46 30,44 L 27,49 C 23,48 19,46 15,43 C 11,35 14,22 20,14 Z M 27,30 A 3,3 0 1 0 27,36 A 3,3 0 1 0 27,30 M 45,30 A 3,3 0 1 0 45,36 A 3,3 0 1 0 45,30 M 8,27 L 4,32 L 8,37 M 64,27 L 68,32 L 64,37",
            "status-changing-channel",
            "DiscordBlurple",
            VoicePresenceEntryMotion.Slide),
        new(
            VoicePresenceState.Reconnecting,
            "discord-reconnecting.svg",
            "M 22,14 C 28,11 44,11 50,14 M 55,20 C 59,28 60,36 57,42 C 53,46 49,48 45,49 L 42,44 C 38,46 34,46 30,44 L 27,49 C 23,48 19,46 15,42 C 12,36 13,28 17,20 M 27,30 A 3,3 0 1 0 27,36 A 3,3 0 1 0 27,30 M 45,30 A 3,3 0 1 0 45,36 A 3,3 0 1 0 45,30 M 9,37 A 28,25 0 0 1 12,20 L 8,22 M 63,19 A 28,25 0 0 1 64,38 L 68,35",
            "status-reconnecting",
            "DiscordBlurple",
            VoicePresenceEntryMotion.Return),
        new(
            VoicePresenceState.Disconnected,
            "discord-disconnected.svg",
            "M 31,12 C 26,11 22,12 19,15 C 14,22 12,34 15,42 C 19,45 23,48 27,49 L 30,44 M 25,30 A 3,3 0 1 0 25,36 A 3,3 0 1 0 25,30 M 41,12 C 46,11 50,12 53,15 C 58,22 60,34 57,42 C 53,45 49,48 45,49 L 42,44 M 47,30 A 3,3 0 1 0 47,36 A 3,3 0 1 0 47,30 M 35,19 L 31,27 L 36,32 L 32,41 M 41,19 L 37,27 L 42,32 L 38,41",
            "status-disconnected",
            "DiscordBlurple",
            VoicePresenceEntryMotion.Settle),
        new(
            VoicePresenceState.AuthorizationRequired,
            "discord-authorization-required.svg",
            "M 20,14 C 25,11 47,11 52,14 C 58,22 61,35 57,43 C 53,46 49,48 45,49 L 42,44 C 38,46 34,46 30,44 L 27,49 C 23,48 19,46 15,43 C 11,35 14,22 20,14 Z M 30,31 V 27 A 6,6 0 0 1 42,27 V 31 M 28,31 H 44 V 43 H 28 Z M 36,35 V 39",
            "status-authorization-required",
            "AeziolMuted",
            VoicePresenceEntryMotion.Settle),
        new(
            VoicePresenceState.Unavailable,
            "discord-unavailable.svg",
            "M 20,14 C 25,11 31,11 35,11 M 43,11 C 47,11 50,12 52,14 C 55,18 57,23 58,27 M 58,36 C 58,39 58,41 57,43 C 53,46 49,48 45,49 M 38,46 C 35,46 32,46 30,44 L 27,49 C 23,48 19,46 15,43 M 13,36 C 12,31 13,26 15,22 M 28,28 L 44,40 M 44,28 L 28,40",
            "status-unavailable",
            "AeziolDim",
            VoicePresenceEntryMotion.Settle),
    ];

    public static VoicePresenceVisual For(VoicePresenceState state) =>
        All.Single(visual => visual.State == state);

    private static Geometry CreateGeometry(string pathData)
    {
        var geometry = Geometry.Parse(pathData);
        geometry.Freeze();
        return geometry;
    }
}
