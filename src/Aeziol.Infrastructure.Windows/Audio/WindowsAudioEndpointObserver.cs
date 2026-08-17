using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Aeziol.Core.Models;

namespace Aeziol.Infrastructure.Windows.Audio;

public sealed class DefaultAudioEndpointChangedEventArgs(AudioRole role, string? endpointId) : EventArgs
{
    public AudioRole Role { get; } = role;

    public string? EndpointId { get; } = endpointId;
}

public sealed class AudioEndpointCatalogChangedEventArgs(string endpointId) : EventArgs
{
    public string EndpointId { get; } = endpointId;
}

[SupportedOSPlatform("windows10.0.22000")]
public sealed class WindowsAudioEndpointObserver : IMMNotificationClient, IDisposable
{
    private readonly IMMDeviceEnumerator _enumerator;
    private bool _disposed;

    public WindowsAudioEndpointObserver()
    {
        _enumerator = (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();
        Marshal.ThrowExceptionForHR(_enumerator.RegisterEndpointNotificationCallback(this));
    }

    public event EventHandler<DefaultAudioEndpointChangedEventArgs>? DefaultEndpointChanged;

    public event EventHandler<AudioEndpointCatalogChangedEventArgs>? EndpointCatalogChanged;

    int IMMNotificationClient.OnDeviceStateChanged(string deviceId, NativeDeviceState newState)
    {
        QueueCatalogChanged(deviceId);
        return 0;
    }

    int IMMNotificationClient.OnDeviceAdded(string deviceId)
    {
        QueueCatalogChanged(deviceId);
        return 0;
    }

    int IMMNotificationClient.OnDeviceRemoved(string deviceId)
    {
        QueueCatalogChanged(deviceId);
        return 0;
    }

    int IMMNotificationClient.OnDefaultDeviceChanged(EDataFlow flow, ERole role, string? defaultDeviceId)
    {
        if (flow == EDataFlow.Render && role is >= ERole.Console and <= ERole.Communications)
        {
            var eventArgs = new DefaultAudioEndpointChangedEventArgs(ToManagedRole(role), defaultDeviceId);
            ThreadPool.QueueUserWorkItem(_ => DefaultEndpointChanged?.Invoke(this, eventArgs));
        }

        return 0;
    }

    int IMMNotificationClient.OnPropertyValueChanged(string deviceId, PropertyKey key)
    {
        QueueCatalogChanged(deviceId);
        return 0;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _ = _enumerator.UnregisterEndpointNotificationCallback(this);
        if (Marshal.IsComObject(_enumerator))
        {
            _ = Marshal.FinalReleaseComObject(_enumerator);
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static AudioRole ToManagedRole(ERole role) => role switch
    {
        ERole.Console => AudioRole.Console,
        ERole.Multimedia => AudioRole.Multimedia,
        ERole.Communications => AudioRole.Communications,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    private void QueueCatalogChanged(string endpointId)
    {
        var eventArgs = new AudioEndpointCatalogChangedEventArgs(endpointId);
        ThreadPool.QueueUserWorkItem(_ => EndpointCatalogChanged?.Invoke(this, eventArgs));
    }
}
