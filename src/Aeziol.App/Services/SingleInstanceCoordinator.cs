namespace Aeziol.App.Services;

internal sealed class SingleInstanceCoordinator : IDisposable
{
    private const string DefaultInstanceName = "GaetanGrd.Aeziol";
    private readonly Semaphore _instanceGate;
    private readonly EventWaitHandle? _activationEvent;
    private readonly RegisteredWaitHandle? _activationRegistration;
    private readonly string _activationEventName;
    private bool _disposed;

    public SingleInstanceCoordinator(string instanceName = DefaultInstanceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        var normalizedName = instanceName.Replace('\\', '.');
        _activationEventName = $"Local\\{normalizedName}.Activate";
        _instanceGate = new Semaphore(
            initialCount: 1,
            maximumCount: 1,
            $"Local\\{normalizedName}.Instance",
            out var createdNew);
        IsPrimaryInstance = createdNew;
        if (!createdNew)
        {
            return;
        }

        _activationEvent = new EventWaitHandle(
            initialState: false,
            EventResetMode.AutoReset,
            _activationEventName);
        _activationRegistration = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            static (state, timedOut) =>
            {
                if (!timedOut && state is SingleInstanceCoordinator coordinator)
                {
                    coordinator.ActivationRequested?.Invoke(coordinator, EventArgs.Empty);
                }
            },
            this,
            Timeout.Infinite,
            executeOnlyOnce: false);
    }

    public bool IsPrimaryInstance { get; }

    public event EventHandler? ActivationRequested;

    public bool SignalPrimaryInstance()
    {
        if (IsPrimaryInstance || _disposed)
        {
            return false;
        }

        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                using var activationEvent = EventWaitHandle.OpenExisting(_activationEventName);
                return activationEvent.Set();
            }
            catch (WaitHandleCannotBeOpenedException) when (attempt < 9)
            {
                Thread.Sleep(25);
            }
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _activationRegistration?.Unregister(null);
        _activationEvent?.Dispose();
        _instanceGate.Dispose();
        _disposed = true;
    }
}
