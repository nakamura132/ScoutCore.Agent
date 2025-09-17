namespace ScoutCore.Agent.Journal;

public interface IJournalStore
{
    Task AppendAsync(IEnumerable<FileEvent> events, CancellationToken ct);
}
