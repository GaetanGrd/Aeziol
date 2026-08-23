namespace Aeziol.App.Appearance;

internal readonly record struct AutomationPresentation(
    string ActionLocalizationKey,
    string AccentBrushKey,
    double ContentOpacity,
    bool ContentIsEnabled)
{
    public static AutomationPresentation For(bool enabled) => enabled
        ? new(
            "automation-disable",
            "AeziolDanger",
            1,
            true)
        : new(
            "automation-enable",
            "AeziolSuccess",
            0.62,
            false);
}
