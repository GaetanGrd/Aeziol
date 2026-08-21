namespace Aeziol.App.Appearance;

internal readonly record struct RouteSummaryPresentation(
    string OutputLabelLocalizationKey,
    bool ShowCurrentOutputSelector,
    bool ShowRestoreOutput,
    bool ShowForceRestore,
    bool ShowIdenticalOutputWarning)
{
    public static RouteSummaryPresentation For(
        string? currentEndpointId,
        string? targetEndpointId,
        bool hasPendingRestoration)
    {
        var outputsAreIdentical = !string.IsNullOrWhiteSpace(currentEndpointId)
            && !string.IsNullOrWhiteSpace(targetEndpointId)
            && string.Equals(currentEndpointId, targetEndpointId, StringComparison.OrdinalIgnoreCase);

        return new(
            hasPendingRestoration ? "restore-output" : "current-output",
            ShowCurrentOutputSelector: !hasPendingRestoration,
            ShowRestoreOutput: hasPendingRestoration,
            ShowForceRestore: hasPendingRestoration,
            ShowIdenticalOutputWarning: outputsAreIdentical && !hasPendingRestoration);
    }
}
