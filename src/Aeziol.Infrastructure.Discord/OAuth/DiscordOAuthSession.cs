using Aeziol.Infrastructure.Discord.Rpc;

namespace Aeziol.Infrastructure.Discord.OAuth;

public sealed record DiscordStoredToken(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    string? RefreshToken,
    IReadOnlySet<string> Scopes);

public interface IDiscordTokenStore
{
    Task<DiscordStoredToken?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(DiscordStoredToken token, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed class DiscordOAuthSession(
    string clientId,
    Uri redirectUri,
    IDiscordOAuthTokenExchange tokenExchange,
    IDiscordTokenStore tokenStore,
    TimeProvider? timeProvider = null) : IDisposable
{
    private static readonly TimeSpan ExpiryMargin = TimeSpan.FromMinutes(1);
    private readonly string _clientId = !string.IsNullOrWhiteSpace(clientId)
        ? clientId
        : throw new ArgumentException("A Discord application client ID is required.", nameof(clientId));
    private readonly Uri _redirectUri = redirectUri ?? throw new ArgumentNullException(nameof(redirectUri));
    private readonly IDiscordOAuthTokenExchange _tokenExchange = tokenExchange ?? throw new ArgumentNullException(nameof(tokenExchange));
    private readonly IDiscordTokenStore _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _authorizationGate = new(1, 1);

    public async Task<string?> TryGetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        await _authorizationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await TryGetStoredOrRefreshedTokenAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _authorizationGate.Release();
        }
    }

    public async Task<string> AuthorizeAsync(
        DiscordRpcClient rpcClient,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rpcClient);
        await _authorizationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var reusableToken = await TryGetStoredOrRefreshedTokenAsync(cancellationToken).ConfigureAwait(false);
            if (reusableToken is not null)
            {
                return reusableToken;
            }

            var authorization = await rpcClient.AuthorizeReadOnlyAsync(_clientId, cancellationToken).ConfigureAwait(false);
            var token = await _tokenExchange.ExchangeCodeAsync(
                    _clientId,
                    _redirectUri,
                    authorization.Code,
                    authorization.CodeVerifier,
                    cancellationToken)
                .ConfigureAwait(false);
            return await SaveAndReturnAsync(token, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _authorizationGate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _authorizationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _tokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _authorizationGate.Release();
        }
    }

    public async Task<bool> HasStoredAuthorizationAsync(CancellationToken cancellationToken = default)
    {
        await _authorizationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await _tokenStore.LoadAsync(cancellationToken).ConfigureAwait(false) is not null;
        }
        finally
        {
            _authorizationGate.Release();
        }
    }

    public async Task<bool> RevokeAsync(CancellationToken cancellationToken = default)
    {
        await _authorizationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var stored = await _tokenStore.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (stored is null)
            {
                return false;
            }

            await _tokenExchange.RevokeAsync(_clientId, stored.AccessToken, cancellationToken).ConfigureAwait(false);
            await _tokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _authorizationGate.Release();
        }
    }

    private async Task<string?> TryGetStoredOrRefreshedTokenAsync(CancellationToken cancellationToken)
    {
        var stored = await _tokenStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (stored is not null && IsUsable(stored))
        {
            return stored.AccessToken;
        }

        if (stored?.RefreshToken is not { Length: > 0 } refreshToken)
        {
            return null;
        }

        try
        {
            var refreshed = await _tokenExchange.RefreshAsync(_clientId, refreshToken, cancellationToken)
                .ConfigureAwait(false);
            return await SaveAndReturnAsync(refreshed, cancellationToken).ConfigureAwait(false);
        }
        catch (DiscordRpcException)
        {
            await _tokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    private bool IsUsable(DiscordStoredToken token) =>
        token.ExpiresAt > _timeProvider.GetUtcNow() + ExpiryMargin
        && token.Scopes.Contains("rpc")
        && token.Scopes.Contains("rpc.voice.read");

    private async Task<string> SaveAndReturnAsync(
        DiscordOAuthToken token,
        CancellationToken cancellationToken)
    {
        if (!token.Scopes.Contains("rpc.voice.read"))
        {
            throw new DiscordRpcException("Discord did not grant the required rpc.voice.read scope.");
        }

        var stored = new DiscordStoredToken(
            token.AccessToken,
            token.TokenType,
            _timeProvider.GetUtcNow() + token.ExpiresIn,
            token.RefreshToken,
            token.Scopes);
        await _tokenStore.SaveAsync(stored, cancellationToken).ConfigureAwait(false);
        return stored.AccessToken;
    }

    public void Dispose()
    {
        _authorizationGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
