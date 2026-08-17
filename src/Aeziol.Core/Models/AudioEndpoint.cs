namespace Aeziol.Core.Models;

public enum AudioEndpointState
{
    Active,
    Disabled,
    NotPresent,
    Unplugged,
}
public sealed record AudioEndpoint(
    string Id,
    string DisplayName,
    AudioEndpointState State,
    string? ContainerId = null,
    string? InterfaceName = null)
{
    public bool IsUsable => State == AudioEndpointState.Active;
}
