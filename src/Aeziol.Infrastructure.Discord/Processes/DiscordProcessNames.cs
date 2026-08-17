namespace Aeziol.Infrastructure.Discord.Processes;

internal static class DiscordProcessNames
{
    private static readonly Dictionary<string, DiscordEdition> Editions =
        new Dictionary<string, DiscordEdition>(StringComparer.OrdinalIgnoreCase)
        {
            ["Discord"] = DiscordEdition.Stable,
            ["Discord.exe"] = DiscordEdition.Stable,
            ["DiscordPTB"] = DiscordEdition.Ptb,
            ["DiscordPTB.exe"] = DiscordEdition.Ptb,
            ["DiscordCanary"] = DiscordEdition.Canary,
            ["DiscordCanary.exe"] = DiscordEdition.Canary,
            ["DiscordDevelopment"] = DiscordEdition.Development,
            ["DiscordDevelopment.exe"] = DiscordEdition.Development,
        };

    public static bool TryGetEdition(string? processName, out DiscordEdition edition) =>
        Editions.TryGetValue(processName ?? string.Empty, out edition);

    public static string GetSourceId(DiscordEdition edition) => edition switch
    {
        DiscordEdition.Stable => "discord-stable",
        DiscordEdition.Ptb => "discord-ptb",
        DiscordEdition.Canary => "discord-canary",
        DiscordEdition.Development => "discord-development",
        _ => throw new ArgumentOutOfRangeException(nameof(edition), edition, null),
    };
}
