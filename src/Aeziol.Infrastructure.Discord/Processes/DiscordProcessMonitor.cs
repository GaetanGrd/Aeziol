using System.Diagnostics;
using System.Management;

namespace Aeziol.Infrastructure.Discord.Processes;

public sealed class DiscordProcessMonitor : IDisposable
{
    private readonly DiscordProcessStateTracker _tracker = new();
    private readonly string? _configuredExecutablePath;
    private readonly string? _configuredProcessName;
    private ManagementEventWatcher? _startWatcher;
    private ManagementEventWatcher? _stopWatcher;
    private CancellationTokenSource? _pollingCancellation;
    private Task? _pollingTask;
    private bool _started;
    private bool _disposed;

    public DiscordProcessMonitor(string? configuredExecutablePath = null)
    {
        _configuredExecutablePath = NormalizeExecutablePath(configuredExecutablePath);
        _configuredProcessName = _configuredExecutablePath is null
            ? null
            : Path.GetFileNameWithoutExtension(_configuredExecutablePath);
    }

    public event EventHandler<DiscordProcessChangedEventArgs>? ProcessChanged;

    public IReadOnlyList<DiscordProcessSnapshot> Current => _tracker.GetSnapshots();

    public DiscordProcessMonitoringMode MonitoringMode { get; private set; }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_started)
        {
            return;
        }

        _started = true;
        Rescan();
        try
        {
            _startWatcher = CreateWatcher("Win32_ProcessStartTrace", OnProcessStarted);
            _stopWatcher = CreateWatcher("Win32_ProcessStopTrace", OnProcessStopped);
            _startWatcher.Start();
            _stopWatcher.Start();
            MonitoringMode = DiscordProcessMonitoringMode.WindowsEvents;
            Rescan();
        }
        catch (ManagementException)
        {
            StopAndDispose(ref _startWatcher, OnProcessStarted);
            StopAndDispose(ref _stopWatcher, OnProcessStopped);
            StartPollingFallback();
        }
        catch (UnauthorizedAccessException)
        {
            StopAndDispose(ref _startWatcher, OnProcessStarted);
            StopAndDispose(ref _stopWatcher, OnProcessStopped);
            StartPollingFallback();
        }
    }

    public void Stop()
    {
        if (!_started && _startWatcher is null && _stopWatcher is null)
        {
            return;
        }

        StopAndDispose(ref _startWatcher, OnProcessStarted);
        StopAndDispose(ref _stopWatcher, OnProcessStopped);
        if (_pollingCancellation is not null)
        {
            _pollingCancellation.Cancel();
            try
            {
                _pollingTask?.GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }

            _pollingCancellation.Dispose();
            _pollingCancellation = null;
            _pollingTask = null;
        }

        _started = false;
        MonitoringMode = DiscordProcessMonitoringMode.Stopped;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void Rescan()
    {
        var discovered = new List<(DiscordEdition Edition, int ProcessId)>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (TryGetEdition(process, out var edition))
                    {
                        discovered.Add((edition, process.Id));
                    }
                }
                catch (InvalidOperationException)
                {
                    // The process ended between enumeration and inspection.
                }
            }
        }

        foreach (var snapshot in _tracker.ReplaceAll(discovered))
        {
            RaiseChanged(snapshot);
        }
    }

    private void StartPollingFallback()
    {
        _pollingCancellation = new CancellationTokenSource();
        MonitoringMode = DiscordProcessMonitoringMode.LowFrequencyScan;
        _pollingTask = PollAsync(_pollingCancellation.Token);
    }

    private async Task PollAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            Rescan();
        }
    }

    private void OnProcessStarted(object sender, EventArrivedEventArgs eventArgs) =>
        ApplyEvent(eventArgs, isStarting: true);

    private void OnProcessStopped(object sender, EventArrivedEventArgs eventArgs) =>
        ApplyEvent(eventArgs, isStarting: false);

    private void ApplyEvent(EventArrivedEventArgs eventArgs, bool isStarting)
    {
        var processName = eventArgs.NewEvent.Properties["ProcessName"]?.Value as string;
        if (!TryGetEdition(processName, out var edition)
            || !TryGetProcessId(eventArgs, out var processId))
        {
            return;
        }

        var snapshot = isStarting
            ? _tracker.Add(edition, processId)
            : _tracker.Remove(edition, processId);
        if (snapshot is not null)
        {
            RaiseChanged(snapshot);
        }
    }

    private void RaiseChanged(DiscordProcessSnapshot snapshot) =>
        ProcessChanged?.Invoke(this, new DiscordProcessChangedEventArgs(snapshot));

    internal bool TryGetEdition(string? processName, out DiscordEdition edition)
    {
        if (DiscordProcessNames.TryGetEdition(processName, out edition))
        {
            return true;
        }

        if (_configuredProcessName is not null
            && string.Equals(
                Path.GetFileNameWithoutExtension(processName),
                _configuredProcessName,
                StringComparison.OrdinalIgnoreCase))
        {
            edition = DiscordEdition.Stable;
            return true;
        }

        edition = default;
        return false;
    }

    private bool TryGetEdition(Process process, out DiscordEdition edition)
    {
        if (DiscordProcessNames.TryGetEdition(process.ProcessName, out edition))
        {
            return true;
        }

        if (!TryGetEdition(process.ProcessName, out edition) || _configuredExecutablePath is null)
        {
            return false;
        }

        try
        {
            return string.Equals(
                Path.GetFullPath(process.MainModule?.FileName ?? string.Empty),
                _configuredExecutablePath,
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return true;
        }
    }

    private static string? NormalizeExecutablePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(path.Trim());
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private static ManagementEventWatcher CreateWatcher(
        string eventClass,
        EventArrivedEventHandler handler)
    {
        var watcher = new ManagementEventWatcher(new EventQuery(
            "WQL",
            $"SELECT * FROM {eventClass}"));
        watcher.EventArrived += handler;
        return watcher;
    }

    private static bool TryGetProcessId(EventArrivedEventArgs eventArgs, out int processId)
    {
        var value = eventArgs.NewEvent.Properties["ProcessID"]?.Value;
        if (value is uint unsigned && unsigned <= int.MaxValue)
        {
            processId = (int)unsigned;
            return true;
        }

        processId = 0;
        return false;
    }

    private static void StopAndDispose(
        ref ManagementEventWatcher? watcher,
        EventArrivedEventHandler handler)
    {
        if (watcher is null)
        {
            return;
        }

        watcher.EventArrived -= handler;
        try
        {
            watcher.Stop();
        }
        catch (ManagementException)
        {
        }

        watcher.Dispose();
        watcher = null;
    }
}
