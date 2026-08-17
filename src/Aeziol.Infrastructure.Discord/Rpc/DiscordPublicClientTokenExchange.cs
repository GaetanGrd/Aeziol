using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aeziol.Infrastructure.Discord.Rpc;

public sealed class DiscordPublicClientTokenExchange(HttpClient httpClient) : IDiscordOAuthTokenExchange
{
    private readonly HttpClient _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    public async Task<DiscordOAuthToken> ExchangeCodeAsync(
        string clientId,
        Uri redirectUri,
        string authorizationCode,
        string codeVerifier,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(redirectUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorizationCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeVerifier);

        return await RequestTokenAsync(
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["grant_type"] = "authorization_code",
                ["code"] = authorizationCode,
                ["code_verifier"] = codeVerifier,
                ["redirect_uri"] = redirectUri.AbsoluteUri,
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<DiscordOAuthToken> RefreshAsync(
        string clientId,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        return await RequestTokenAsync(
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task RevokeAsync(
        string clientId,
        string token,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://discord.com/api/oauth2/token/revoke")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["token"] = token,
                ["token_type_hint"] = "access_token",
            }),
        };
        using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await ReadErrorDetailAsync(response, cancellationToken).ConfigureAwait(false);
            throw new DiscordRpcException(
                $"Discord rejected the OAuth revocation ({(int)response.StatusCode} {response.ReasonPhrase}){detail}.");
        }
    }

    private async Task<DiscordOAuthToken> RequestTokenAsync(
        Dictionary<string, string> fields,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://discord.com/api/oauth2/token")
        {
            Content = new FormUrlEncodedContent(fields),
        };

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await ReadErrorDetailAsync(response, cancellationToken).ConfigureAwait(false);
            throw new DiscordRpcException(
                $"Discord rejected the public-client token exchange " +
                $"({(int)response.StatusCode} {response.ReasonPhrase}){detail}.");
        }

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(DiscordRpcJson.SerializerOptions, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new DiscordRpcException("Discord returned an empty OAuth token response.");
        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            throw new DiscordRpcException("Discord returned an OAuth response without an access token.");
        }

        var scopes = (token.Scope ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
        return new DiscordOAuthToken(
            token.AccessToken,
            token.TokenType ?? "Bearer",
            TimeSpan.FromSeconds(token.ExpiresIn),
            token.RefreshToken,
            scopes);
    }

    private static async Task<string> ReadErrorDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>(
                DiscordRpcJson.SerializerOptions,
                cancellationToken).ConfigureAwait(false);
            var parts = new[] { error?.Error, error?.ErrorDescription }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return parts.Length == 0 ? string.Empty : $": {string.Join(" — ", parts)}";
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("scope")] string? Scope);

    private sealed record ErrorResponse(
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("error_description")] string? ErrorDescription);
}
