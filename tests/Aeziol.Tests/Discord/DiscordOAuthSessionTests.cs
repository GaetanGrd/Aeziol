using Aeziol.Infrastructure.Discord.OAuth;
using Aeziol.Infrastructure.Discord.Rpc;

namespace Aeziol.Tests.Discord;

public sealed class DiscordOAuthSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TryGetAccessTokenAsync_UsesValidCredentialWithoutNetworkExchange()
    {
        var stored = new DiscordStoredToken(
            "cached-token",
            "Bearer",
            Now.AddHours(1),
            "refresh-token",
            new HashSet<string>(["rpc", "rpc.voice.read"], StringComparer.Ordinal));
        var store = new FakeTokenStore(stored);
        var exchange = new FakeTokenExchange();
        using var session = new DiscordOAuthSession(
            "client-id",
            new Uri("http://127.0.0.1/callback"),
            exchange,
            store,
            new FixedTimeProvider(Now));
        var token = await session.TryGetAccessTokenAsync(TestContext.Current.CancellationToken);

        Assert.Equal("cached-token", token);
        Assert.Equal(0, exchange.RefreshCalls);
    }

    [Fact]
    public async Task TryGetAccessTokenAsync_RefreshesExpiredCredentialAndPersistsReplacement()
    {
        var stored = new DiscordStoredToken(
            "expired-token",
            "Bearer",
            Now.AddMinutes(-1),
            "refresh-token",
            new HashSet<string>(["rpc", "rpc.voice.read"], StringComparer.Ordinal));
        var store = new FakeTokenStore(stored);
        var exchange = new FakeTokenExchange
        {
            RefreshedToken = new DiscordOAuthToken(
                "new-token",
                "Bearer",
                TimeSpan.FromHours(1),
                "new-refresh",
                new HashSet<string>(["rpc", "rpc.voice.read"], StringComparer.Ordinal)),
        };
        using var session = new DiscordOAuthSession(
            "client-id",
            new Uri("http://127.0.0.1/callback"),
            exchange,
            store,
            new FixedTimeProvider(Now));
        var token = await session.TryGetAccessTokenAsync(TestContext.Current.CancellationToken);

        Assert.Equal("new-token", token);
        Assert.Equal(1, exchange.RefreshCalls);
        Assert.Equal("new-token", store.Token?.AccessToken);
    }

    [Fact]
    public async Task TryGetAccessTokenAsync_DoesNotStartInteractiveAuthorizationWithoutStoredCredential()
    {
        var store = new FakeTokenStore(null);
        var exchange = new FakeTokenExchange();
        using var session = new DiscordOAuthSession(
            "client-id",
            new Uri("http://127.0.0.1/callback"),
            exchange,
            store,
            new FixedTimeProvider(Now));

        var token = await session.TryGetAccessTokenAsync(TestContext.Current.CancellationToken);

        Assert.Null(token);
        Assert.Equal(0, exchange.ExchangeCalls);
        Assert.Equal(0, exchange.RefreshCalls);
    }

    [Fact]
    public async Task RevokeAsync_RevokesRemoteTokenBeforeClearingCredential()
    {
        var stored = new DiscordStoredToken(
            "cached-token",
            "Bearer",
            Now.AddHours(1),
            "refresh-token",
            new HashSet<string>(["rpc", "rpc.voice.read"], StringComparer.Ordinal));
        var store = new FakeTokenStore(stored);
        var exchange = new FakeTokenExchange();
        using var session = new DiscordOAuthSession(
            "client-id",
            new Uri("http://127.0.0.1/callback"),
            exchange,
            store,
            new FixedTimeProvider(Now));

        var revoked = await session.RevokeAsync(TestContext.Current.CancellationToken);

        Assert.True(revoked);
        Assert.Equal("cached-token", exchange.RevokedToken);
        Assert.Null(store.Token);
    }

    private sealed class FakeTokenStore(DiscordStoredToken? token) : IDiscordTokenStore
    {
        public DiscordStoredToken? Token { get; private set; } = token;

        public Task<DiscordStoredToken?> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(Token);

        public Task SaveAsync(DiscordStoredToken token, CancellationToken cancellationToken = default)
        {
            Token = token;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            Token = null;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTokenExchange : IDiscordOAuthTokenExchange
    {
        public int RefreshCalls { get; private set; }

        public int ExchangeCalls { get; private set; }

        public string? RevokedToken { get; private set; }

        public DiscordOAuthToken RefreshedToken { get; init; } = new(
            "unused",
            "Bearer",
            TimeSpan.FromHours(1),
            null,
            new HashSet<string>(["rpc", "rpc.voice.read"], StringComparer.Ordinal));

        public Task<DiscordOAuthToken> ExchangeCodeAsync(
            string clientId,
            Uri redirectUri,
            string authorizationCode,
            string codeVerifier,
            CancellationToken cancellationToken = default)
        {
            ExchangeCalls++;
            throw new NotSupportedException();
        }

        public Task RevokeAsync(
            string clientId,
            string token,
            CancellationToken cancellationToken = default)
        {
            RevokedToken = token;
            return Task.CompletedTask;
        }

        public Task<DiscordOAuthToken> RefreshAsync(
            string clientId,
            string refreshToken,
            CancellationToken cancellationToken = default)
        {
            RefreshCalls++;
            return Task.FromResult(RefreshedToken);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
