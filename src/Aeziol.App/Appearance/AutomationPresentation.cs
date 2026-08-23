namespace Aeziol.App.Appearance;

internal readonly record struct AutomationPresentation(
    string ActionLocalizationKey,
    string AccentBrushKey,
    string StateBrushKey,
    double ContentOpacity,
    bool ContentIsEnabled)
{
    public static AutomationPresentation For(bool enabled) => enabled
        ? new(
            "automation-disable",
            "AeziolDanger",
            "AeziolSuccess",
            1,
            true)
        : new(
            "automation-enable",
            "AeziolSuccess",
            "AeziolDanger",
            0.62,
            false);
}
