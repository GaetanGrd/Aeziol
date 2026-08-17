using Aeziol.Core.Models;

namespace Aeziol.Core.Abstractions;

public sealed class VoiceObservationEventArgs(VoiceObservation observation) : EventArgs
{
    public VoiceObservation Observation { get; } = observation;
}
public interface IVoicePresenceObserver : IAsyncDisposable
{
    event EventHandler<VoiceObservationEventArgs>? ObservationReceived;

    Task StartAsync(CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
