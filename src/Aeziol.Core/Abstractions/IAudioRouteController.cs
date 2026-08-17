using Aeziol.Core.Models;

namespace Aeziol.Core.Abstractions;

public interface IAudioRouteController
{
    Task<IReadOnlyList<AudioEndpoint>> GetRenderEndpointsAsync(CancellationToken cancellationToken = default);

    Task<AudioRouteSnapshot> CaptureAsync(
        IReadOnlySet<AudioRole> roles,
        CancellationToken cancellationToken = default);

    Task<bool> IsEndpointUsableAsync(
        string endpointId,
        CancellationToken cancellationToken = default);

    Task ApplyAsync(
        string endpointId,
        IReadOnlySet<AudioRole> roles,
        CancellationToken cancellationToken = default);

    Task RestoreAsync(
        AudioRouteSnapshot snapshot,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyAsync(
        string endpointId,
        IReadOnlySet<AudioRole> roles,
        CancellationToken cancellationToken = default);

    Task<bool> VerifyAsync(
        AudioRouteSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
