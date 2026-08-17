namespace Aeziol.Core.Abstractions;

public interface IExclusionPolicy
{
    bool IsExcluded(string endpointId);
}
