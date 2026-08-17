namespace Aeziol.Core.Models;

public sealed record AudioRouteSnapshot(IReadOnlyDictionary<AudioRole, string> Endpoints)
{
    public string? Get(AudioRole role) => Endpoints.GetValueOrDefault(role);
}
