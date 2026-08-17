using System.IO.Pipes;
using System.Text.Json;

namespace Aeziol.Infrastructure.Discord.Rpc;

internal sealed class DiscordIpcConnection : IAsyncDisposable
{
    private const int PipeCount = 10;
    private const int ConnectionTimeoutMilliseconds = 100;
    private readonly NamedPipeClientStream _stream;
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private bool _disposed;

    private DiscordIpcConnection(NamedPipeClientStream stream, int pipeIndex)
    {
        _stream = stream;
        PipeIndex = pipeIndex;
    }

    public int PipeIndex { get; }

    public static async Task<DiscordIpcConnection> ConnectAsync(CancellationToken cancellationToken)
    {
        Exception? lastException = null;
        for (var index = 0; index < PipeCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stream = new NamedPipeClientStream(
                ".",
                $"discord-ipc-{index}",
                PipeDirection.InOut,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough);
            try
            {
                await stream.ConnectAsync(ConnectionTimeoutMilliseconds, cancellationToken).ConfigureAwait(false);
                stream.ReadMode = PipeTransmissionMode.Byte;
                return new DiscordIpcConnection(stream, index);
            }
            catch (Exception exception) when (exception is IOException or TimeoutException)
            {
                lastException = exception;
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }

        throw new DiscordRpcException("No local Discord RPC pipe is available.", innerException: lastException);
    }

    public static async Task<DiscordIpcConnection> ConnectAsync(
        int pipeIndex,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pipeIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pipeIndex, PipeCount);
        var stream = new NamedPipeClientStream(
            ".",
            $"discord-ipc-{pipeIndex}",
            PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.WriteThrough);
        try
        {
            await stream.ConnectAsync(ConnectionTimeoutMilliseconds, cancellationToken).ConfigureAwait(false);
            stream.ReadMode = PipeTransmissionMode.Byte;
            return new DiscordIpcConnection(stream, pipeIndex);
        }
        catch
        {
            await stream.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public Task<DiscordRpcFrame> ReadAsync(CancellationToken cancellationToken) =>
        DiscordRpcFrameCodec.ReadAsync(_stream, cancellationToken);

    public async Task WriteJsonAsync(
        DiscordRpcOpcode opcode,
        object payload,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, DiscordRpcJson.SerializerOptions);
        var frame = DiscordRpcFrameCodec.Encode(opcode, json);
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async Task WriteRawAsync(
        DiscordRpcOpcode opcode,
        byte[] payload,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var frame = DiscordRpcFrameCodec.Encode(opcode, payload);
        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _stream.DisposeAsync().ConfigureAwait(false);
        _writeGate.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

internal static class DiscordRpcJson
{
    public static JsonSerializerOptions SerializerOptions { get; } = new(JsonSerializerDefaults.Web);
}
