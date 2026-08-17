namespace Aeziol.App.Appearance;

internal readonly record struct AutomationPresentation(
    string ActionLocalizationKey,
    string ButtonStyleKey,
    double ContentOpacity,
    bool ContentIsEnabled)
{
    public static AutomationPresentation For(bool enabled) => enabled
        ? new("automation-disable", "WarningButton", 1, true)
        : new("automation-enable", "SuccessButton", 0.32, false);
}
