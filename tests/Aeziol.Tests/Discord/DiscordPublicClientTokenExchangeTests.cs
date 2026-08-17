using System.Net;
using System.Net.Http;
using System.Text;
using Aeziol.Infrastructure.Discord.Rpc;

namespace Aeziol.Tests.Discord;

public sealed class DiscordPublicClientTokenExchangeTests
{
    [Fact]
    public async Task ExchangeCodeAsync_SendsPkceVerifierForPublicClient()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"access_token":"token","token_type":"Bearer","expires_in":3600,"refresh_token":"refresh","scope":"rpc rpc.voice.read"}""",
                Encoding.UTF8,
                "application/json"),
        });
        using var httpClient = new HttpClient(handler);
        var exchange = new DiscordPublicClientTokenExchange(httpClient);

        var token = await exchange.ExchangeCodeAsync(
            "client-id",
            new Uri("http://127.0.0.1/callback"),
            "authorization-code",
            "pkce-verifier",
            TestContext.Current.CancellationToken);

        Assert.Equal("token", token.AccessToken);
        Assert.Contains("code_verifier=pkce-verifier", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("client_id=client-id", handler.RequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExchangeCodeAsync_ReportsSafeDiscordErrorDetail()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"error":"invalid_request","error_description":"Missing code verifier"}""",
                Encoding.UTF8,
                "application/json"),
        });
        using var httpClient = new HttpClient(handler);
        var exchange = new DiscordPublicClientTokenExchange(httpClient);

        var exception = await Assert.ThrowsAsync<DiscordRpcException>(() => exchange.ExchangeCodeAsync(
            "client-id",
            new Uri("http://127.0.0.1/callback"),
            "authorization-code",
            "pkce-verifier",
            TestContext.Current.CancellationToken));

        Assert.Contains("invalid_request", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Missing code verifier", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RevokeAsync_SendsPublicClientAndTokenAsFormData()
    {
        var handler = new RecordingHandler(new HttpResponseMessage(HttpStatusCode.OK));
        using var httpClient = new HttpClient(handler);
        var exchange = new DiscordPublicClientTokenExchange(httpClient);

        await exchange.RevokeAsync("client-id", "access-token", TestContext.Current.CancellationToken);

        Assert.Equal("https://discord.com/api/oauth2/token/revoke", handler.RequestUri?.AbsoluteUri);
        Assert.Contains("client_id=client-id", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("token=access-token", handler.RequestBody, StringComparison.Ordinal);
        Assert.Contains("token_type_hint=access_token", handler.RequestBody, StringComparison.Ordinal);
    }

    private sealed class RecordingHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public string RequestBody { get; private set; } = string.Empty;

        public Uri? RequestUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return response;
        }
    }
}
