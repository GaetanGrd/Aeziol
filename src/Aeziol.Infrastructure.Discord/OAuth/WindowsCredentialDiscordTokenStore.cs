using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Aeziol.Infrastructure.Discord.OAuth;

public sealed partial class WindowsCredentialDiscordTokenStore(string targetName = "Aeziol/DiscordOAuth") : IDiscordTokenStore
{
    private const uint CredentialTypeGeneric = 1;
    private const uint CredentialPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private readonly string _targetName = !string.IsNullOrWhiteSpace(targetName)
        ? targetName
        : throw new ArgumentException("A credential target name is required.", nameof(targetName));

    public Task<DiscordStoredToken?> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CredentialNativeMethods.CredRead(_targetName, CredentialTypeGeneric, 0, out var credentialPointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
            {
                return Task.FromResult<DiscordStoredToken?>(null);
            }

            throw new Win32Exception(error, "Unable to read the Discord OAuth credential.");
        }

        try
        {
            var credential = Marshal.PtrToStructure<CredentialNative>(credentialPointer);
            var payload = new byte[credential.CredentialBlobSize];
            if (payload.Length > 0)
            {
                Marshal.Copy(credential.CredentialBlob, payload, 0, payload.Length);
            }

            return Task.FromResult(Deserialize(payload));
        }
        finally
        {
            CredentialNativeMethods.CredFree(credentialPointer);
        }
    }

    public unsafe Task SaveAsync(DiscordStoredToken token, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(token);
        cancellationToken.ThrowIfCancellationRequested();
        var payload = Serialize(token);
        fixed (byte* payloadPointer = payload)
        {
            var credential = new CredentialNative
            {
                Type = CredentialTypeGeneric,
                TargetName = _targetName,
                CredentialBlobSize = (uint)payload.Length,
                CredentialBlob = (nint)payloadPointer,
                Persist = CredentialPersistLocalMachine,
                UserName = "Aeziol",
            };
            var credentialPointer = Marshal.AllocHGlobal(Marshal.SizeOf<CredentialNative>());
            try
            {
                Marshal.StructureToPtr(credential, credentialPointer, fDeleteOld: false);
                if (!CredentialNativeMethods.CredWrite(credentialPointer, 0))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to store the Discord OAuth credential.");
                }
            }
            finally
            {
                Marshal.DestroyStructure<CredentialNative>(credentialPointer);
                Marshal.FreeHGlobal(credentialPointer);
            }
        }

        return Task.CompletedTask;
    }

    internal static byte[] Serialize(DiscordStoredToken token) =>
        JsonSerializer.SerializeToUtf8Bytes(new CredentialPayload(
            token.AccessToken,
            token.TokenType,
            token.ExpiresAt,
            token.RefreshToken,
            token.Scopes.ToArray()));

    internal static DiscordStoredToken? Deserialize(ReadOnlySpan<byte> payload)
    {
        var stored = JsonSerializer.Deserialize<CredentialPayload>(payload);
        return stored is null
            ? null
            : new DiscordStoredToken(
                stored.AccessToken,
                stored.TokenType,
                stored.ExpiresAt,
                stored.RefreshToken,
                stored.Scopes.ToHashSet(StringComparer.Ordinal));
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CredentialNativeMethods.CredDelete(_targetName, CredentialTypeGeneric, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != ErrorNotFound)
            {
                throw new Win32Exception(error, "Unable to delete the Discord OAuth credential.");
            }
        }

        return Task.CompletedTask;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CredentialNative
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? TargetName;
        public nint Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public nint CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public nint Attributes;
        public nint TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? UserName;
    }

    private sealed record CredentialPayload(
        string AccessToken,
        string TokenType,
        DateTimeOffset ExpiresAt,
        string? RefreshToken,
        string[] Scopes);

    private static partial class CredentialNativeMethods
    {
        [LibraryImport("Advapi32.dll", EntryPoint = "CredReadW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CredRead(string target, uint type, uint flags, out nint credential);

        [LibraryImport("Advapi32.dll", EntryPoint = "CredWriteW", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CredWrite(nint credential, uint flags);

        [LibraryImport("Advapi32.dll", EntryPoint = "CredDeleteW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool CredDelete(string target, uint type, uint flags);

        [LibraryImport("Advapi32.dll")]
        public static partial void CredFree(nint credential);
    }
}
