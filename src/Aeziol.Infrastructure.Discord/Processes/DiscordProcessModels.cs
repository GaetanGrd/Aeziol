namespace Aeziol.Infrastructure.Discord.Processes;

public enum DiscordEdition
{
    Stable,
    Ptb,
    Canary,
    Development,
}

public enum DiscordProcessMonitoringMode
{
    Stopped,
    WindowsEvents,
    LowFrequencyScan,
}

public sealed record DiscordProcessSnapshot(
    DiscordEdition Edition,
    string SourceId,
    bool IsRunning,
    int ProcessCount);

public sealed class DiscordProcessChangedEventArgs(DiscordProcessSnapshot snapshot) : EventArgs
{
    public DiscordProcessSnapshot Snapshot { get; } = snapshot;
}
