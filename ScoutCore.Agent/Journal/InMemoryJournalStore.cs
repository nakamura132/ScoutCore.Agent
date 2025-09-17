using System.Collections.Concurrent;

namespace ScoutCore.Agent.Journal;

public sealed class InMemoryJournalStore : IJournalStore, IJournalQuery
{
    private readonly ConcurrentDictionary<string,List<FileEvent>> _events = new();

    public Task AppendAsync(IEnumerable<FileEvent> events, CancellationToken ct)
    {
        foreach (var ev in events)
        {
            var list = _events.GetOrAdd(ev.FileKey, _ => new List<FileEvent>());
            list.Add(ev);
        }
        return Task.CompletedTask;
    }

    public Task<JournalSummary?> GetSummaryAsync(string fileKey, CancellationToken ct)
    {
        if (!_events.TryGetValue(fileKey, out var list) || list.Count == 0)
            return Task.FromResult<JournalSummary?>(null);

        var first = list.Min(e => e.Time);
        var last  = list.Max(e => e.Time);
        var counts = list.GroupBy(e => e.Kind).ToDictionary(g => g.Key, g => g.Count());
        var lastEv = list.OrderByDescending(e => e.Time).First();

        var summary = new JournalSummary {
            FirstSeenAt = first,
            LastSeenAt  = last,
            OpCounts    = counts,
            LastProc    = lastEv.ProcessName,
            LastUser    = lastEv.UserSid
        };
        return Task.FromResult<JournalSummary?>(summary);
    }
}
