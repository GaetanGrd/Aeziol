using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Aeziol.App.Services;

internal static partial class LogSanitizer
{
    private const string Redacted = "[redacted]";

    internal static JsonNode? Sanitize(object? value)
    {
        if (value is null)
        {
            return null;
        }

        var node = JsonSerializer.SerializeToNode(value, value.GetType());
        return SanitizeNode(node, propertyName: null);
    }

    internal static string SanitizeText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var sanitized = ReplaceKnownValue(value, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "[user-profile]");
        sanitized = ReplaceKnownValue(sanitized, Environment.UserName, "[user]");
        sanitized = ReplaceKnownValue(sanitized, Environment.MachineName, "[machine]");
        sanitized = BearerTokenRegex().Replace(sanitized, "Bearer [redacted]");
        sanitized = OAuthParameterRegex().Replace(
            sanitized,
            match => $"{match.Groups[1].Value}={Redacted}");
        sanitized = JsonSecretRegex().Replace(
            sanitized,
            match => $"{match.Groups[1].Value}{Redacted}{match.Groups[3].Value}");
        sanitized = WindowsUserProfileRegex().Replace(sanitized, "[user-profile]");
        sanitized = QuotedWindowsPathRegex().Replace(
            sanitized,
            match => Pseudonymize("local-path", match.Value.Trim('"', '\'')));
        sanitized = WindowsPathTokenRegex().Replace(
            sanitized,
            match => Pseudonymize("local-path", match.Value));
        sanitized = UncPathRegex().Replace(sanitized, "[network-path]");
        sanitized = AudioEndpointRegex().Replace(
            sanitized,
            match => Pseudonymize("audio-endpoint", match.Value));
        sanitized = EmailRegex().Replace(sanitized, "[email]");
        sanitized = DiscordSnowflakeRegex().Replace(
            sanitized,
            match => Pseudonymize("discord-id", match.Value));
        sanitized = Ipv4Regex().Replace(sanitized, "[ip-address]");
        return sanitized;
    }

    internal static string SanitizeJsonLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return line;
        }

        try
        {
            var node = JsonNode.Parse(line);
            return SanitizeNode(node, propertyName: null)?.ToJsonString() ?? "null";
        }
        catch (JsonException)
        {
            return SanitizeText(line);
        }
    }

    private static JsonNode? SanitizeNode(JsonNode? node, string? propertyName)
    {
        if (node is null)
        {
            return null;
        }

        var classification = ClassifyProperty(propertyName);
        if (classification is PropertyClassification.Secret)
        {
            return JsonValue.Create(Redacted);
        }

        if (classification is PropertyClassification.Identifier or PropertyClassification.Path)
        {
            return JsonValue.Create(Pseudonymize(
                classification is PropertyClassification.Path ? "path" : "identifier",
                NodeText(node)));
        }

        if (node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text))
        {
            return JsonValue.Create(SanitizeText(text));
        }

        if (node is JsonObject jsonObject)
        {
            foreach (var key in jsonObject.Select(item => item.Key).ToArray())
            {
                jsonObject[key] = SanitizeNode(jsonObject[key], key);
            }

            return jsonObject;
        }

        if (node is JsonArray jsonArray)
        {
            for (var index = 0; index < jsonArray.Count; index++)
            {
                jsonArray[index] = SanitizeNode(jsonArray[index], propertyName: null);
            }
        }

        return node;
    }

    private static PropertyClassification ClassifyProperty(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return PropertyClassification.None;
        }

        var normalized = new string(propertyName.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
        if (normalized is "token" or "accesstoken" or "refreshtoken" or "authorizationcode"
            or "codeverifier" or "clientsecret" or "secret" or "password" or "credential"
            or "cookie" or "authorizationheader")
        {
            return PropertyClassification.Secret;
        }

        if (normalized.EndsWith("path", StringComparison.Ordinal)
            || normalized.EndsWith("directory", StringComparison.Ordinal))
        {
            return PropertyClassification.Path;
        }

        if (normalized is "endpointid" or "deviceid" or "userid" or "discorduserid"
            or "clientid" or "accountid" or "guildid" or "channelid" or "sessionid")
        {
            return PropertyClassification.Identifier;
        }

        return PropertyClassification.None;
    }

    private static string NodeText(JsonNode node) =>
        node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text)
            ? text
            : node.ToJsonString();

    private static string Pseudonymize(string category, string value)
    {
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return $"[{category}:{Convert.ToHexString(digest.AsSpan(0, 6)).ToLowerInvariant()}]";
    }

    private static string ReplaceKnownValue(string source, string value, string replacement) =>
        string.IsNullOrWhiteSpace(value)
            ? source
            : source.Replace(value, replacement, StringComparison.OrdinalIgnoreCase);

    private enum PropertyClassification
    {
        None,
        Secret,
        Identifier,
        Path,
    }

    [GeneratedRegex(@"(?i)\bBearer\s+[A-Za-z0-9._~+/=-]+")]
    private static partial Regex BearerTokenRegex();

    [GeneratedRegex(@"(?i)\b(access_token|refresh_token|token|authorization_code|code_verifier|client_secret)=([^&\s]+)")]
    private static partial Regex OAuthParameterRegex();

    [GeneratedRegex("(?i)([\\\"](?:access_token|refresh_token|token|authorization_code|code_verifier|client_secret)[\\\"]\\s*:\\s*[\\\"])([^\\\"]*)([\\\"])")]
    private static partial Regex JsonSecretRegex();

    [GeneratedRegex("(?i)\\b[A-Z]:\\\\Users\\\\[^\\\\\\s\\\"'<>|]+")]
    private static partial Regex WindowsUserProfileRegex();

    [GeneratedRegex("(?i)([\\\"])[A-Z]:\\\\.*?\\1")]
    private static partial Regex QuotedWindowsPathRegex();

    [GeneratedRegex("(?i)\\b[A-Z]:\\\\[^\\s\\\"'<>|]+")]
    private static partial Regex WindowsPathTokenRegex();

    [GeneratedRegex("\\\\\\\\[^\\\\\\s]+\\\\[^\\\\\\s]+(?:\\\\[^\\s\\\"']*)?")]
    private static partial Regex UncPathRegex();

    [GeneratedRegex(@"(?i)\{0\.0\.0\.[^}]+\}\.\{[0-9a-f-]+\}")]
    private static partial Regex AudioEndpointRegex();

    [GeneratedRegex(@"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?<!\d)\d{17,20}(?!\d)")]
    private static partial Regex DiscordSnowflakeRegex();

    [GeneratedRegex(@"(?<!\d)(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)(?!\d)")]
    private static partial Regex Ipv4Regex();
}
