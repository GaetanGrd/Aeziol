namespace Aeziol.App.Settings;

internal static class DestinationSelectionPolicy
{
    public static bool ShouldPersist(string? selectedEndpointId, string? configuredEndpointId) =>
        !string.IsNullOrWhiteSpace(selectedEndpointId)
        && !string.Equals(selectedEndpointId, configuredEndpointId, StringComparison.OrdinalIgnoreCase);
}
