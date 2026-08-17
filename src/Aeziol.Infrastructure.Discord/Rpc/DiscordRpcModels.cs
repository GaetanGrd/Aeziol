using System.Text.Json;

namespace Aeziol.Infrastructure.Discord.Rpc;

internal enum DiscordRpcOpcode
{
    Handshake = 0,
    Frame = 1,
    Close = 2,
    Ping = 3,
    Pong = 4,
}

internal sealed record DiscordRpcFrame(DiscordRpcOpcode Opcode, byte[] Payload);

public sealed class DiscordRpcEventArgs(string name, JsonElement data) : EventArgs
{
    public string Name { get; } = name;

    public JsonElement Data { get; } = data;
}

public sealed class DiscordRpcConnectionClosedEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}

public sealed class DiscordRpcException(string message, int? code = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    public int? Code { get; } = code;
}

public sealed record DiscordOAuthToken(
    string AccessToken,
    string TokenType,
    TimeSpan ExpiresIn,
    string? RefreshToken,
    IReadOnlySet<string> Scopes);

public sealed record DiscordAuthorizationCode(string Code, string CodeVerifier);

public interface IDiscordOAuthTokenExchange
{
    Task<DiscordOAuthToken> ExchangeCodeAsync(
        string clientId,
        Uri redirectUri,
        string authorizationCode,
        string codeVerifier,
        CancellationToken cancellationToken = default);

    Task<DiscordOAuthToken> RefreshAsync(
        string clientId,
        string refreshToken,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(
        string clientId,
        string token,
        CancellationToken cancellationToken = default);
}
