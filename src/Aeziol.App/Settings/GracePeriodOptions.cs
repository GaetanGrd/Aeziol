namespace Aeziol.App.Settings;

internal static class GracePeriodOptions
{
    public const int RecommendedSeconds = 1;
    public const int MaximumSeconds = 30;

    public static bool IsSupported(int seconds) => seconds is >= 0 and <= MaximumSeconds;

    public static int Normalize(int seconds) => IsSupported(seconds) ? seconds : RecommendedSeconds;
}
