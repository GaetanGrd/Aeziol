namespace Aeziol.Infrastructure.Discord.Processes;

internal sealed class DiscordProcessStateTracker
{
    private readonly Lock _gate = new();
    private readonly Dictionary<DiscordEdition, HashSet<int>> _processes = Enum
        .GetValues<DiscordEdition>()
        .ToDictionary(edition => edition, _ => new HashSet<int>());

    public DiscordProcessSnapshot? Add(DiscordEdition edition, int processId)
    {
        lock (_gate)
        {
            var processes = _processes[edition];
            var wasRunning = processes.Count > 0;
            if (!processes.Add(processId) || wasRunning)
            {
                return null;
            }

            return CreateSnapshot(edition, processes);
        }
    }

    public DiscordProcessSnapshot? Remove(DiscordEdition edition, int processId)
    {
        lock (_gate)
        {
            var processes = _processes[edition];
            if (!processes.Remove(processId) || processes.Count > 0)
            {
                return null;
            }

            return CreateSnapshot(edition, processes);
        }
    }

    public IReadOnlyList<DiscordProcessSnapshot> ReplaceAll(
        IEnumerable<(DiscordEdition Edition, int ProcessId)> processes)
    {
        ArgumentNullException.ThrowIfNull(processes);
        lock (_gate)
        {
            var previous = _processes.ToDictionary(pair => pair.Key, pair => pair.Value.Count > 0);
            foreach (var entries in _processes.Values)
            {
                entries.Clear();
            }

            foreach (var (edition, processId) in processes)
            {
                _processes[edition].Add(processId);
            }

            return _processes
                .Where(pair => previous[pair.Key] != (pair.Value.Count > 0))
                .Select(pair => CreateSnapshot(pair.Key, pair.Value))
                .ToArray();
        }
    }

    public IReadOnlyList<DiscordProcessSnapshot> GetSnapshots()
    {
        lock (_gate)
        {
            return _processes
                .Select(pair => CreateSnapshot(pair.Key, pair.Value))
                .ToArray();
        }
    }

    private static DiscordProcessSnapshot CreateSnapshot(DiscordEdition edition, HashSet<int> processes) =>
        new(edition, DiscordProcessNames.GetSourceId(edition), processes.Count > 0, processes.Count);
}
