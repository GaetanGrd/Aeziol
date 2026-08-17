using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Aeziol.Core.Abstractions;
using Aeziol.Core.Models;

namespace Aeziol.Infrastructure.Windows.Audio;

[SupportedOSPlatform("windows10.0.22000")]
public sealed class WindowsAudioRouteController : IAudioRouteController
{
    private const uint ReadPropertyStore = 0;
    private static readonly PropertyKey FriendlyNameKey = new(
        new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),
        14);
    private static readonly PropertyKey InterfaceNameKey = new(
        new Guid("026E516E-B814-414B-83CD-856D6FEF4822"),
        2);
    private static readonly PropertyKey ContainerIdKey = new(
        new Guid("8C7ED206-3F8A-4827-B3AB-AE9E1FAEFC6C"),
        2);

    public Task<IReadOnlyList<AudioEndpoint>> GetRenderEndpointsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var enumerator = CreateEnumerator();
        IMMDeviceCollection? collection = null;
        try
        {
            Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(EDataFlow.Render, NativeDeviceState.All, out collection));
            Marshal.ThrowExceptionForHR(collection.GetCount(out var count));
            var endpoints = new List<AudioEndpoint>(checked((int)count));
            for (uint index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Marshal.ThrowExceptionForHR(collection.Item(index, out var device));
                try
                {
                    endpoints.Add(ReadEndpoint(device));
                }
                finally
                {
                    ReleaseComObject(device);
                }
            }

            return Task.FromResult<IReadOnlyList<AudioEndpoint>>(endpoints);
        }
        finally
        {
            ReleaseComObject(collection);
            ReleaseComObject(enumerator);
        }
    }

    public Task<AudioRouteSnapshot> CaptureAsync(
        IReadOnlySet<AudioRole> roles,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roles);
        cancellationToken.ThrowIfCancellationRequested();
        var enumerator = CreateEnumerator();
        try
        {
            var endpoints = new Dictionary<AudioRole, string>();
            foreach (var role in roles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ToNativeRole(role), out var device));
                try
                {
                    endpoints[role] = GetEndpointId(device);
                }
                finally
                {
                    ReleaseComObject(device);
                }
            }

            return Task.FromResult(new AudioRouteSnapshot(endpoints));
        }
        finally
        {
            ReleaseComObject(enumerator);
        }
    }

    public Task<bool> IsEndpointUsableAsync(
        string endpointId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);
        cancellationToken.ThrowIfCancellationRequested();
        var enumerator = CreateEnumerator();
        try
        {
            var result = enumerator.GetDevice(endpointId, out var device);
            if (result < 0)
            {
                return Task.FromResult(false);
            }

            try
            {
                Marshal.ThrowExceptionForHR(device.GetState(out var state));
                return Task.FromResult(state == NativeDeviceState.Active);
            }
            finally
            {
                ReleaseComObject(device);
            }
        }
        finally
        {
            ReleaseComObject(enumerator);
        }
    }

    public Task ApplyAsync(
        string endpointId,
        IReadOnlySet<AudioRole> roles,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpointId);
        ArgumentNullException.ThrowIfNull(roles);
        foreach (var role in roles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetDefaultEndpoint(endpointId, role);
        }

        return Task.CompletedTask;
    }

    public Task RestoreAsync(AudioRouteSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        foreach (var pair in snapshot.Endpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SetDefaultEndpoint(pair.Value, pair.Key);
        }

        return Task.CompletedTask;
    }

    public async Task<bool> VerifyAsync(
        string endpointId,
        IReadOnlySet<AudioRole> roles,
        CancellationToken cancellationToken = default)
    {
        var current = await CaptureAsync(roles, cancellationToken).ConfigureAwait(false);
        return roles.All(role => string.Equals(current.Get(role), endpointId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> VerifyAsync(
        AudioRouteSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var current = await CaptureAsync(snapshot.Endpoints.Keys.ToHashSet(), cancellationToken).ConfigureAwait(false);
        return snapshot.Endpoints.All(pair =>
            string.Equals(current.Get(pair.Key), pair.Value, StringComparison.OrdinalIgnoreCase));
    }

    private static IMMDeviceEnumerator CreateEnumerator() =>
        (IMMDeviceEnumerator)(object)new MMDeviceEnumeratorComObject();

    private static AudioEndpoint ReadEndpoint(IMMDevice device)
    {
        var id = GetEndpointId(device);
        Marshal.ThrowExceptionForHR(device.GetState(out var state));
        Marshal.ThrowExceptionForHR(device.OpenPropertyStore(ReadPropertyStore, out var properties));
        try
        {
            return new AudioEndpoint(
                id,
                ReadStringProperty(properties, FriendlyNameKey) ?? id,
                ToManagedState(state),
                ReadGuidProperty(properties, ContainerIdKey)?.ToString("D"),
                ReadStringProperty(properties, InterfaceNameKey));
        }
        finally
        {
            ReleaseComObject(properties);
        }
    }

    private static string GetEndpointId(IMMDevice device)
    {
        Marshal.ThrowExceptionForHR(device.GetId(out var pointer));
        try
        {
            return Marshal.PtrToStringUni(pointer)
                ?? throw new InvalidDataException("Windows returned an empty audio endpoint identifier.");
        }
        finally
        {
            Marshal.FreeCoTaskMem(pointer);
        }
    }

    private static string? ReadStringProperty(IPropertyStore properties, PropertyKey key)
    {
        var localKey = key;
        Marshal.ThrowExceptionForHR(properties.GetValue(ref localKey, out var value));
        try
        {
            return value.GetString();
        }
        finally
        {
            _ = NativeMethods.PropVariantClear(ref value);
        }
    }

    private static Guid? ReadGuidProperty(IPropertyStore properties, PropertyKey key)
    {
        var localKey = key;
        Marshal.ThrowExceptionForHR(properties.GetValue(ref localKey, out var value));
        try
        {
            return value.GetGuid();
        }
        finally
        {
            _ = NativeMethods.PropVariantClear(ref value);
        }
    }

    private static void SetDefaultEndpoint(string endpointId, AudioRole role)
    {
        object? policyObject = null;
        try
        {
            policyObject = new PolicyConfigClientComObject();
            var policy = (IPolicyConfig)policyObject;
            Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(endpointId, ToNativeRole(role)));
        }
        catch (InvalidCastException)
        {
            ReleaseComObject(policyObject);
            policyObject = new PolicyConfigVistaClientComObject();
            var policy = (IPolicyConfigVista)policyObject;
            Marshal.ThrowExceptionForHR(policy.SetDefaultEndpoint(endpointId, ToNativeRole(role)));
        }
        finally
        {
            ReleaseComObject(policyObject);
        }
    }

    private static ERole ToNativeRole(AudioRole role) => role switch
    {
        AudioRole.Console => ERole.Console,
        AudioRole.Multimedia => ERole.Multimedia,
        AudioRole.Communications => ERole.Communications,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    private static AudioEndpointState ToManagedState(NativeDeviceState state) => state switch
    {
        NativeDeviceState.Active => AudioEndpointState.Active,
        NativeDeviceState.Disabled => AudioEndpointState.Disabled,
        NativeDeviceState.NotPresent => AudioEndpointState.NotPresent,
        NativeDeviceState.Unplugged => AudioEndpointState.Unplugged,
        _ => AudioEndpointState.NotPresent,
    };

    private static void ReleaseComObject(object? instance)
    {
        if (instance is not null && Marshal.IsComObject(instance))
        {
            _ = Marshal.FinalReleaseComObject(instance);
        }
    }
}
