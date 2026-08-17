using Aeziol.Core.Abstractions;

namespace Aeziol.Core.Policies;

public sealed class EndpointExclusionPolicy : IExclusionPolicy
{
    private readonly Lock _gate = new();
    private HashSet<string> _endpointIds;

    public EndpointExclusionPolicy(IEnumerable<string>? endpointIds = null)
    {
        _endpointIds = new HashSet<string>(endpointIds ?? [], StringComparer.OrdinalIgnoreCase);
    }

    public bool IsExcluded(string endpointId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);
        lock (_gate)
        {
            return _endpointIds.Contains(endpointId);
        }
    }

    public void Replace(IEnumerable<string> endpointIds)
    {
        ArgumentNullException.ThrowIfNull(endpointIds);
        lock (_gate)
        {
            _endpointIds = new HashSet<string>(endpointIds, StringComparer.OrdinalIgnoreCase);
        }
    }

    public IReadOnlySet<string> Snapshot()
    {
        lock (_gate)
        {
            return new HashSet<string>(_endpointIds, StringComparer.OrdinalIgnoreCase);
        }
    }
}
