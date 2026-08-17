using System.Text.Json;
using Aeziol.App.Services;

namespace Aeziol.Tests.App;

public sealed class AppLoggerTests
{
    [Fact]
    public async Task WriteAsync_RedactsSensitiveValuesBeforeTheyReachDisk()
    {
        var directory = Path.Combine(Path.GetTempPath(), "Aeziol.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var endpointId = "{0.0.0.00000000}.{f13f084c-7725-4c8f-8949-325c34e12b77}";
        var legacyToken = "legacy-secret-token";
        var currentToken = "current-secret-token";
        var logPath = Path.Combine(directory, "aeziol.log.jsonl");

        try
        {
            var legacyEntry = JsonSerializer.Serialize(new
            {
                eventName = "legacy-entry",
                properties = new
                {
                    endpointId,
                    token = legacyToken,
                    path = @"C:\Users\SecretUser\AppData\Local\Discord\Discord.exe",
                },
            });
            await File.WriteAllTextAsync(logPath, legacyEntry + Environment.NewLine, TestContext.Current.CancellationToken);

            using (var logger = new AppLogger(directory))
            {
                await logger.WriteAsync(
                    "information",
                    "privacy-test",
                    new
                    {
                        accessToken = currentToken,
                        endpointId,
                        DiscordExecutablePath = @"D:\Private\Discord\Discord.exe",
                        outcome = "restored",
                        message = "Bearer bearer-secret access_token=query-secret user@example.com 192.168.1.40 123456789012345678",
                    },
                    TestContext.Current.CancellationToken);
            }

            var contents = await File.ReadAllTextAsync(logPath, TestContext.Current.CancellationToken);
            Assert.DoesNotContain(legacyToken, contents, StringComparison.Ordinal);
            Assert.DoesNotContain(currentToken, contents, StringComparison.Ordinal);
            Assert.DoesNotContain("bearer-secret", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("query-secret", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("SecretUser", contents, StringComparison.Ordinal);
            Assert.DoesNotContain(endpointId, contents, StringComparison.Ordinal);
            Assert.DoesNotContain("user@example.com", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("192.168.1.40", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("123456789012345678", contents, StringComparison.Ordinal);
            Assert.Contains("[redacted]", contents, StringComparison.Ordinal);
            Assert.Contains("[identifier:", contents, StringComparison.Ordinal);
            Assert.Contains("[path:", contents, StringComparison.Ordinal);
            Assert.Contains("restored", contents, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SanitizeText_PreservesUsefulDiagnosticsWhileRemovingPersonalData()
    {
        const string input = "RPC failed for C:\\Users\\Alice\\AppData\\Discord.exe with code 4007 on 10.0.0.4";

        var sanitized = LogSanitizer.SanitizeText(input);

        Assert.Contains("RPC failed", sanitized, StringComparison.Ordinal);
        Assert.Contains("code 4007", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("Alice", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("10.0.0.4", sanitized, StringComparison.Ordinal);
    }
}
