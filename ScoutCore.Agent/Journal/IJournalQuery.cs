namespace ScoutCore.Agent.Journal;

public interface IJournalQuery
{
    Task<JournalSummary?> GetSummaryAsync(string fileKey, CancellationToken ct);
}
