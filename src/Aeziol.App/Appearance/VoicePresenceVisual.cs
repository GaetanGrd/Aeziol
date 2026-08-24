using System.Windows.Media;
using Aeziol.Core.Models;

namespace Aeziol.App.Appearance;

internal sealed record VoicePresenceVisual(
    VoicePresenceState State,
    string AssetFileName,
    string PrimaryPathData,
    string? CutoutPathData,
    string? OverlayPathData,
    string FillBrushKey)
{
    internal const string DiscordLogoPathData = "M41.2351 0C40.6164 1.09866 40.0607 2.2352 39.5556 3.397C34.7569 2.67719 29.8697 2.67719 25.0584 3.397C24.5658 2.2352 23.9976 1.09866 23.3788 0C18.8705 0.770324 14.4759 2.12155 10.3085 4.02841C2.04967 16.2652 -0.185531 28.1863 0.925755 39.9432C5.76238 43.517 11.1799 46.2447 16.951 47.9874C18.2517 46.2447 19.4009 44.3883 20.3859 42.4562C18.5169 41.7616 16.7111 40.8903 14.981 39.88C15.4356 39.5517 15.8776 39.2107 16.307 38.8824C26.4475 43.6559 38.1917 43.6559 48.3449 38.8824C48.7742 39.236 49.2162 39.577 49.6708 39.88C47.9408 40.9029 46.1349 41.7616 44.2533 42.4688C45.2383 44.4009 46.3875 46.2573 47.6882 48C53.4593 46.2573 58.8768 43.5422 63.7134 39.9684C65.0268 26.3299 61.4656 14.5099 54.3054 4.04104C50.1507 2.13418 45.7561 0.782952 41.2478 0.0252565L41.2351 0ZM21.8003 32.7072C18.6811 32.7072 16.0923 29.8785 16.0923 26.3804C16.0923 22.8824 18.5801 20.041 21.7876 20.041C24.9952 20.041 27.5461 22.895 27.4956 26.3804C27.4451 29.8658 24.9826 32.7072 21.8003 32.7072ZM42.8389 32.7072C39.7071 32.7072 37.1436 29.8785 37.1436 26.3804C37.1436 22.8824 39.6314 20.041 42.8389 20.041C46.0465 20.041 48.5848 22.895 48.5343 26.3804C48.4838 29.8658 46.0213 32.7072 42.8389 32.7072Z";
    internal const string DiscordCracksPathData = "M11 10L16 12L14 18L19 22L15 29L10 26L14 21L10 17ZM28 2L34 8L31 15L36 20L32 27L35 34L30 42L27 35L30 27L26 21L29 14L25 8ZM48 7L54 13L50 20L56 25L52 32L47 29L51 25L47 20L50 15L46 11Z";
    internal const string SpeakerPathData = "M7 18H18L31 8V40L18 30H7ZM35 17C39 21 39 27 35 31L38 34C44 28 44 20 38 14ZM43 10C52 18 52 30 43 38L46 42C58 32 58 16 46 6Z";
    internal const string QuestionMarkPathData = "M24 16C24 8 30 3 39 3C48 3 54 8.5 54 16.5C54 23 50.5 26 45.5 29C41.5 31.4 40 33.2 40 37H32C32 29.2 35 25.5 41 22C44.5 20 46 18.7 46 16C46 12.4 43.2 10 39 10C34.8 10 32 12.4 32 16ZM36 39A4 4 0 1 0 36 47A4 4 0 1 0 36 39Z";

    private Geometry? _primaryGeometry;
    private Geometry? _overlayGeometry;

    public Geometry PrimaryGeometry => _primaryGeometry ??= CreatePrimaryGeometry();

    public Geometry? OverlayGeometry => OverlayPathData is null
        ? null
        : _overlayGeometry ??= CreateGeometry(OverlayPathData);

    public static IReadOnlyList<VoicePresenceVisual> All { get; } =
    [
        new(VoicePresenceState.DiscordAbsent, "discord-absent.svg",
            DiscordLogoPathData, DiscordCracksPathData, null, "AeziolDim"),
        new(VoicePresenceState.OutOfVoice, "discord-out-of-voice.svg",
            DiscordLogoPathData, null, null, "DiscordBlurple"),
        new(VoicePresenceState.Connected, "discord-connected.svg",
            SpeakerPathData, null, null, "DiscordBlurple"),
        new(VoicePresenceState.AuthorizationRequired, "discord-authorization-required.svg",
            DiscordLogoPathData, null, null, "AeziolMuted"),
        new(VoicePresenceState.Unavailable, "discord-unavailable.svg",
            QuestionMarkPathData, null, null, "AeziolDim"),
    ];

    public static VoicePresenceVisual For(VoicePresenceState state) => state switch
    {
        VoicePresenceState.DiscordAbsent => All[0],
        VoicePresenceState.OutOfVoice
            or VoicePresenceState.Connecting
            or VoicePresenceState.ChangingChannel
            or VoicePresenceState.Reconnecting
            or VoicePresenceState.Disconnected => All[1],
        VoicePresenceState.Connected => All[2],
        VoicePresenceState.AuthorizationRequired => All[3],
        VoicePresenceState.Unavailable => All[4],
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    public static string LocalizationKeyFor(VoicePresenceState state) => state switch
    {
        VoicePresenceState.Disconnected => "status-out-of-voice",
        VoicePresenceState.DiscordAbsent => "status-discord-absent",
        VoicePresenceState.OutOfVoice => "status-out-of-voice",
        VoicePresenceState.Connecting => "status-connecting",
        VoicePresenceState.Connected => "status-connected",
        VoicePresenceState.ChangingChannel => "status-changing-channel",
        VoicePresenceState.Reconnecting => "status-reconnecting",
        VoicePresenceState.AuthorizationRequired => "status-authorization-required",
        VoicePresenceState.Unavailable => "status-unavailable",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    public static bool Rotates(VoicePresenceState state) => state is
        VoicePresenceState.Connecting
        or VoicePresenceState.ChangingChannel
        or VoicePresenceState.Reconnecting;

    private Geometry CreatePrimaryGeometry()
    {
        var primary = CreateGeometry(PrimaryPathData);
        if (CutoutPathData is null)
        {
            return primary;
        }

        var cutout = CreateGeometry(CutoutPathData);
        var combined = Geometry.Combine(primary, cutout, GeometryCombineMode.Exclude, null);
        combined.Freeze();
        return combined;
    }

    private static Geometry CreateGeometry(string pathData)
    {
        var geometry = Geometry.Parse(pathData);
        geometry.Freeze();
        return geometry;
    }
}
