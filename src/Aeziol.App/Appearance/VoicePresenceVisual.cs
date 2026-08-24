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
    string? CutoutPathData,
    string? OverlayPathData,
    string LocalizationKey,
    string FillBrushKey,
    VoicePresenceEntryMotion EntryMotion)
{
    internal const string DiscordLogoPathData = "M41.2351 0C40.6164 1.09866 40.0607 2.2352 39.5556 3.397C34.7569 2.67719 29.8697 2.67719 25.0584 3.397C24.5658 2.2352 23.9976 1.09866 23.3788 0C18.8705 0.770324 14.4759 2.12155 10.3085 4.02841C2.04967 16.2652 -0.185531 28.1863 0.925755 39.9432C5.76238 43.517 11.1799 46.2447 16.951 47.9874C18.2517 46.2447 19.4009 44.3883 20.3859 42.4562C18.5169 41.7616 16.7111 40.8903 14.981 39.88C15.4356 39.5517 15.8776 39.2107 16.307 38.8824C26.4475 43.6559 38.1917 43.6559 48.3449 38.8824C48.7742 39.236 49.2162 39.577 49.6708 39.88C47.9408 40.9029 46.1349 41.7616 44.2533 42.4688C45.2383 44.4009 46.3875 46.2573 47.6882 48C53.4593 46.2573 58.8768 43.5422 63.7134 39.9684C65.0268 26.3299 61.4656 14.5099 54.3054 4.04104C50.1507 2.13418 45.7561 0.782952 41.2478 0.0252565L41.2351 0ZM21.8003 32.7072C18.6811 32.7072 16.0923 29.8785 16.0923 26.3804C16.0923 22.8824 18.5801 20.041 21.7876 20.041C24.9952 20.041 27.5461 22.895 27.4956 26.3804C27.4451 29.8658 24.9826 32.7072 21.8003 32.7072ZM42.8389 32.7072C39.7071 32.7072 37.1436 29.8785 37.1436 26.3804C37.1436 22.8824 39.6314 20.041 42.8389 20.041C46.0465 20.041 48.5848 22.895 48.5343 26.3804C48.4838 29.8658 46.0213 32.7072 42.8389 32.7072Z";

    private Geometry? _logoGeometry;
    private Geometry? _overlayGeometry;

    public Geometry LogoGeometry => _logoGeometry ??= CreateLogoGeometry(CutoutPathData);

    public Geometry? OverlayGeometry => OverlayPathData is null
        ? null
        : _overlayGeometry ??= CreateGeometry(OverlayPathData);

    public static IReadOnlyList<VoicePresenceVisual> All { get; } =
    [
        new(VoicePresenceState.DiscordAbsent, "discord-absent.svg",
            "M0 12L7 9L10 13L3 17ZM55 5L62 8L59 13L52 10ZM1 34L9 36L8 41L0 39Z", null,
            "status-discord-absent", "AeziolDim", VoicePresenceEntryMotion.Settle),
        new(VoicePresenceState.OutOfVoice, "discord-out-of-voice.svg", null, null,
            "status-out-of-voice", "DiscordBlurple", VoicePresenceEntryMotion.Settle),
        new(VoicePresenceState.Connecting, "discord-connecting.svg",
            "M31.2 1H33.8L33.2 16L34.2 25L32.7 47H30.8L31.7 25L30.7 16Z",
            "M25 21L30 24.5L25 28ZM40 21L35 24.5L40 28Z",
            "status-connecting", "DiscordBlurple", VoicePresenceEntryMotion.Converge),
        new(VoicePresenceState.Connected, "discord-connected.svg", null,
            "M3 17C0 21-0.2 27 3 32L5.5 29C3.7 26 3.7 22.5 5.5 20ZM62 17C65 21 65.2 27 62 32L59.5 29C61.3 26 61.3 22.5 59.5 20Z",
            "status-connected", "DiscordBlurple", VoicePresenceEntryMotion.Settle),
        new(VoicePresenceState.ChangingChannel, "discord-changing-channel.svg", null,
            "M0 18L5.5 24L0 30H4L9.5 24L4 18ZM65 18L59.5 24L65 30H61L55.5 24L61 18Z",
            "status-changing-channel", "DiscordBlurple", VoicePresenceEntryMotion.Slide),
        new(VoicePresenceState.Reconnecting, "discord-reconnecting.svg",
            "M5 8L12 9L9 15L3 13ZM53 35L62 34L61 41L54 41Z",
            "M2 33C-0.5 25 1 17 7 11L10 14C5.5 19 4.5 25.5 6 31ZM63 15C65.5 23 64 31 58 37L55 34C59.5 29 60.5 22.5 59 17Z",
            "status-reconnecting", "DiscordBlurple", VoicePresenceEntryMotion.Return),
        new(VoicePresenceState.Disconnected, "discord-disconnected.svg",
            "M29-1H36L32.2 13L37 22L30.5 32L35 49H28L31 33L26 23L33 12Z", null,
            "status-disconnected", "DiscordBlurple", VoicePresenceEntryMotion.Settle),
        new(VoicePresenceState.AuthorizationRequired, "discord-authorization-required.svg",
            "M45 27H62V48H43V31C43 28.8 43.8 27.5 45 27Z",
            "M48 34V31C48 25.8 56 25.8 56 31V34H58V45H46V34ZM50.5 34H53.5V31C53.5 29 50.5 29 50.5 31ZM52 37.2C50.9 37.2 50.4 38.5 51.2 39.2V42H52.8V39.2C53.6 38.5 53.1 37.2 52 37.2Z",
            "status-authorization-required", "AeziolMuted", VoicePresenceEntryMotion.Settle),
        new(VoicePresenceState.Unavailable, "discord-unavailable.svg",
            "M0 9L9 6L11 11L2 14ZM54 3L63 7L60 13L51 9ZM1 34L10 36L8 42L0 39ZM55 36L65 34L64 40L57 43Z",
            "M25 19L32.5 26.5L40 19L43 22L35.5 29.5L43 37L40 40L32.5 32.5L25 40L22 37L29.5 29.5L22 22Z",
            "status-unavailable", "AeziolDim", VoicePresenceEntryMotion.Settle),
    ];

    public static VoicePresenceVisual For(VoicePresenceState state) =>
        All.Single(visual => visual.State == state);

    private static Geometry CreateLogoGeometry(string? cutoutPathData)
    {
        var logo = CreateGeometry(DiscordLogoPathData);
        if (cutoutPathData is null)
        {
            return logo;
        }

        var cutout = CreateGeometry(cutoutPathData);
        var combined = Geometry.Combine(logo, cutout, GeometryCombineMode.Exclude, null);
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
