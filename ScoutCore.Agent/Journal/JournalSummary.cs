namespace ScoutCore.Agent.Journal;

public sealed class JournalSummary
{
    public DateTimeOffset? FirstSeenAt { get; init; }
    public DateTimeOffset? LastSeenAt  { get; init; }
    public Dictionary<FileOpKind,int> OpCounts { get; init; } = new();
    public string? LastProc { get; init; }
    public string? LastUser { get; init; }
}
