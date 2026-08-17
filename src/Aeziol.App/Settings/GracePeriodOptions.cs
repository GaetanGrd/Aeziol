namespace Aeziol.App.Settings;

internal static class GracePeriodOptions
{
    public const int RecommendedSeconds = 1;

    public static bool IsSupported(int seconds) => seconds is 0 or 1 or 2;

    public static int Normalize(int seconds) => IsSupported(seconds) ? seconds : RecommendedSeconds;
}
