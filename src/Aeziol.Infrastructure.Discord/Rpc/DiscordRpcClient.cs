using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Aeziol.Infrastructure.Discord.Rpc;

public sealed class DiscordRpcClient : IAsyncDisposable
{
    private static readonly string[] ReadOnlyScopes = ["rpc", "rpc.voice.read"];
    private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonElement>> _pending = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetime = new();
    private DiscordIpcConnection? _connection;
    private Task? _readLoop;
    private bool _isConnected;
    private bool _disposed;

    public event EventHandler<DiscordRpcEventArgs>? EventReceived;

    public event EventHandler<DiscordRpcConnectionClosedEventArgs>? ConnectionClosed;

    public bool IsConnected => _isConnected && !_lifetime.IsCancellationRequested;

    public int? PipeIndex => _connection?.PipeIndex;

    public async Task ConnectAsync(string clientId, CancellationToken cancellationToken = default)
    {
        await ConnectCoreAsync(clientId, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task ConnectToPipeAsync(
        string clientId,
        int pipeIndex,
        CancellationToken cancellationToken = default)
    {
        await ConnectCoreAsync(clientId, pipeIndex, cancellationToken).ConfigureAwait(false);
    }

    private async Task ConnectCoreAsync(
        string clientId,
        int? pipeIndex,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        if (_connection is not null)
        {
            throw new InvalidOperationException("The Discord RPC client is already connected.");
        }

        var connection = pipeIndex.HasValue
            ? await DiscordIpcConnection.ConnectAsync(pipeIndex.Value, cancellationToken).ConfigureAwait(false)
            : await DiscordIpcConnection.ConnectAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await connection.WriteJsonAsync(
                DiscordRpcOpcode.Handshake,
                new { v = 1, client_id = clientId },
                cancellationToken).ConfigureAwait(false);
            var readyFrame = await connection.ReadAsync(cancellationToken).ConfigureAwait(false);
            ValidateReadyFrame(readyFrame);

            _connection = connection;
            _isConnected = true;
            _readLoop = ReadLoopAsync(_lifetime.Token);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task<DiscordAuthorizationCode> AuthorizeReadOnlyAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        var verifier = CreateCodeVerifier();
        var challenge = Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var response = await SendCommandAsync(
            "AUTHORIZE",
            new
            {
                client_id = clientId,
                scopes = ReadOnlyScopes,
                code_challenge = challenge,
                code_challenge_method = "S256",
            },
            cancellationToken).ConfigureAwait(false);
        var data = GetRequiredProperty(response, "data");
        var code = GetRequiredString(data, "code");
        return new DiscordAuthorizationCode(code, verifier);
    }

    private static string CreateCodeVerifier() => Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static string Base64UrlEncode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public async Task AuthenticateAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        var response = await SendCommandAsync(
            "AUTHENTICATE",
            new { access_token = accessToken },
            cancellationToken).ConfigureAwait(false);
        var data = GetRequiredProperty(response, "data");
        if (!data.TryGetProperty("scopes", out var scopes)
            || scopes.ValueKind != JsonValueKind.Array
            || !scopes.EnumerateArray().Any(item =>
                string.Equals(item.GetString(), "rpc.voice.read", StringComparison.Ordinal)))
        {
            throw new DiscordRpcException("The Discord token does not include rpc.voice.read.");
        }
    }

    public async Task<bool> GetSelectedVoiceChannelAsync(CancellationToken cancellationToken = default)
    {
        var response = await SendCommandAsync("GET_SELECTED_VOICE_CHANNEL", new { }, cancellationToken)
            .ConfigureAwait(false);
        var data = GetRequiredProperty(response, "data");
        return data.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;
    }

    public async Task SubscribeToVoiceEventsAsync(CancellationToken cancellationToken = default)
    {
        await SubscribeAsync("VOICE_CHANNEL_SELECT", cancellationToken).ConfigureAwait(false);
        await SubscribeAsync("VOICE_CONNECTION_STATUS", cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _lifetime.Cancel();
        _isConnected = false;
        if (_readLoop is not null)
        {
            try
            {
                await _readLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (IOException)
            {
            }
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync().ConfigureAwait(false);
        }

        CancelPending(new ObjectDisposedException(nameof(DiscordRpcClient)));
        _lifetime.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private async Task SubscribeAsync(string eventName, CancellationToken cancellationToken)
    {
        _ = await SendCommandAsync(
            "SUBSCRIBE",
            new Dictionary<string, object?>(),
            cancellationToken,
            eventName).ConfigureAwait(false);
    }

    private async Task<JsonElement> SendCommandAsync(
        string command,
        object arguments,
        CancellationToken cancellationToken,
        string? eventName = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var connection = _connection ?? throw new InvalidOperationException("Discord RPC is not connected.");
        var nonce = Guid.NewGuid().ToString("D");
        var completion = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(nonce, completion))
        {
            throw new InvalidOperationException("Unable to allocate a Discord RPC request nonce.");
        }

        try
        {
            var payload = eventName is null
                ? new Dictionary<string, object?>
                {
                    ["cmd"] = command,
                    ["args"] = arguments,
                    ["nonce"] = nonce,
                }
                : new Dictionary<string, object?>
                {
                    ["cmd"] = command,
                    ["args"] = arguments,
                    ["evt"] = eventName,
                    ["nonce"] = nonce,
                };
            await connection.WriteJsonAsync(DiscordRpcOpcode.Frame, payload, cancellationToken).ConfigureAwait(false);
            return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _pending.TryRemove(nonce, out _);
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var connection = _connection;
                if (connection is null)
                {
                    return;
                }
                var frame = await connection.ReadAsync(cancellationToken).ConfigureAwait(false);
                switch (frame.Opcode)
                {
                    case DiscordRpcOpcode.Frame:
                        ProcessJsonFrame(frame.Payload);
                        break;
                    case DiscordRpcOpcode.Ping:
                        await connection.WriteRawAsync(DiscordRpcOpcode.Pong, frame.Payload, cancellationToken)
                            .ConfigureAwait(false);
                        break;
                    case DiscordRpcOpcode.Close:
                        throw new DiscordRpcException("Discord closed the local RPC connection.");
                    case DiscordRpcOpcode.Handshake:
                    case DiscordRpcOpcode.Pong:
                    default:
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _isConnected = false;
            CancelPending(exception);
            ConnectionClosed?.Invoke(this, new DiscordRpcConnectionClosedEventArgs(exception));
        }
    }

    private void ProcessJsonFrame(byte[] payload)
    {
        using var document = JsonDocument.Parse(payload, new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 32,
        });
        var root = document.RootElement;
        var nonce = root.TryGetProperty("nonce", out var nonceElement) ? nonceElement.GetString() : null;
        if (!string.IsNullOrWhiteSpace(nonce) && _pending.TryGetValue(nonce, out var completion))
        {
            if (IsError(root, out var error))
            {
                completion.TrySetException(error);
            }
            else
            {
                completion.TrySetResult(root.Clone());
            }

            return;
        }

        if (root.TryGetProperty("evt", out var eventElement)
            && eventElement.GetString() is { Length: > 0 } eventName
            && root.TryGetProperty("data", out var data))
        {
            EventReceived?.Invoke(this, new DiscordRpcEventArgs(eventName, data.Clone()));
        }
    }

    private static void ValidateReadyFrame(DiscordRpcFrame frame)
    {
        if (frame.Opcode != DiscordRpcOpcode.Frame)
        {
            throw new DiscordRpcException("Discord RPC did not answer the handshake with a data frame.");
        }

        using var document = JsonDocument.Parse(frame.Payload);
        var root = document.RootElement;
        if (!root.TryGetProperty("evt", out var eventName)
            || !string.Equals(eventName.GetString(), "READY", StringComparison.Ordinal))
        {
            throw new DiscordRpcException("Discord RPC did not emit READY after the handshake.");
        }
    }

    private static bool IsError(JsonElement root, out DiscordRpcException exception)
    {
        if (root.TryGetProperty("evt", out var eventName)
            && string.Equals(eventName.GetString(), "ERROR", StringComparison.Ordinal)
            && root.TryGetProperty("data", out var data))
        {
            var code = data.TryGetProperty("code", out var codeElement) && codeElement.TryGetInt32(out var parsedCode)
                ? parsedCode
                : (int?)null;
            var message = data.TryGetProperty("message", out var messageElement)
                ? messageElement.GetString()
                : null;
            exception = new DiscordRpcException(message ?? "Discord RPC returned an error.", code);
            return true;
        }

        exception = null!;
        return false;
    }

    private void CancelPending(Exception exception)
    {
        foreach (var completion in _pending.Values)
        {
            completion.TrySetException(exception);
        }

        _pending.Clear();
    }

    private static JsonElement GetRequiredProperty(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value)
            ? value
            : throw new DiscordRpcException($"Discord RPC response is missing '{name}'.");

    private static string GetRequiredString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.GetString() is { Length: > 0 } text
            ? text
            : throw new DiscordRpcException($"Discord RPC response is missing '{name}'.");
}
