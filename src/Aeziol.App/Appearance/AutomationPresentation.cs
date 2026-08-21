namespace Aeziol.App.Appearance;

internal readonly record struct AutomationPresentation(
    string ActionLocalizationKey,
    string IconGeometry,
    string AccentBrushKey,
    string BackgroundBrushKey,
    double ContentOpacity,
    bool ContentIsEnabled)
{
    public static AutomationPresentation For(bool enabled) => enabled
        ? new(
            "automation-disable",
            "M 2,1 L 2,11 M 8,1 L 8,11",
            "AeziolWarningOrange",
            "AeziolRaised",
            1,
            true)
        : new(
            "automation-enable",
            "M 1,1 L 10,6 L 1,11 Z",
            "AeziolSuccess",
            "AeziolRaised",
            0.62,
            false);
}
