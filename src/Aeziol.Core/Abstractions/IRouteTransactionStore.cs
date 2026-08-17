using Aeziol.Core.Models;

namespace Aeziol.Core.Abstractions;

public interface IRouteTransactionStore
{
    Task<RouteTransaction?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(RouteTransaction transaction, CancellationToken cancellationToken = default);

    Task DeleteAsync(CancellationToken cancellationToken = default);
}
