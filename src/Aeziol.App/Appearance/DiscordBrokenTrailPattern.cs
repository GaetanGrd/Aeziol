namespace Aeziol.App.Appearance;

internal readonly record struct DiscordBrokenTrailStop(double Offset, bool IsTransparent);

internal static class DiscordBrokenTrailPattern
{
    public static IReadOnlyList<DiscordBrokenTrailStop> PrimaryStops { get; } =
    [
        new(0, false),
        new(0.245, false),
        new(0.268, true),
        new(0.316, true),
        new(0.34, false),
        new(0.532, false),
        new(0.555, true),
        new(0.632, true),
        new(0.655, false),
        new(0.748, false),
        new(0.77, true),
        new(0.832, true),
        new(0.855, false),
        new(1, false),
    ];

    public static IReadOnlyList<DiscordBrokenTrailStop> SecondaryStops { get; } =
    [
        new(0, false),
        new(0.28, false),
        new(0.302, true),
        new(0.35, true),
        new(0.372, false),
        new(0.558, false),
        new(0.58, true),
        new(0.659, true),
        new(0.681, false),
        new(0.773, false),
        new(0.795, true),
        new(0.859, true),
        new(0.881, false),
        new(1, false),
    ];
}
