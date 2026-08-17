using System.Text;
using Aeziol.Infrastructure.Discord.OAuth;

namespace Aeziol.Tests.Discord;

public sealed class WindowsCredentialDiscordTokenStoreTests
{
    [Fact]
    public void DeserializeReadsExistingCredentialPayloadWithScopesArray()
    {
        var payload = Encoding.UTF8.GetBytes(
            """
            {
              "AccessToken": "access-token",
              "TokenType": "Bearer",
              "ExpiresAt": "2030-01-02T03:04:05+00:00",
              "RefreshToken": "refresh-token",
              "Scopes": ["rpc", "rpc.voice.read"]
            }
            """);

        var token = WindowsCredentialDiscordTokenStore.Deserialize(payload);

        Assert.NotNull(token);
        Assert.Equal("access-token", token.AccessToken);
        Assert.Equal("Bearer", token.TokenType);
        Assert.Contains("rpc", token.Scopes);
        Assert.Contains("rpc.voice.read", token.Scopes);
    }

    [Fact]
    public void StoredTokenRoundTripsThroughConcreteCredentialPayload()
    {
        var expected = new DiscordStoredToken(
            "access-token",
            "Bearer",
            new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero),
            "refresh-token",
            new HashSet<string>(["rpc", "rpc.voice.read"], StringComparer.Ordinal));

        var actual = WindowsCredentialDiscordTokenStore.Deserialize(
            WindowsCredentialDiscordTokenStore.Serialize(expected));

        Assert.NotNull(actual);
        Assert.Equal(expected.AccessToken, actual.AccessToken);
        Assert.Equal(expected.TokenType, actual.TokenType);
        Assert.Equal(expected.ExpiresAt, actual.ExpiresAt);
        Assert.Equal(expected.RefreshToken, actual.RefreshToken);
        Assert.True(expected.Scopes.SetEquals(actual.Scopes));
    }
}
