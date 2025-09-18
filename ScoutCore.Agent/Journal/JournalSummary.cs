namespace ScoutCore.Agent.Journal;

public sealed class JournalSummary
{
    public DateTimeOffset? FirstSeenAt { get; init; }
    public DateTimeOffset? LastSeenAt  { get; init; }
    public Dictionary<FileOpKind,int> OpCounts { get; init; } = new();
    public string? LastProc { get; init; }
    public string? LastUser { get; init; }

    // ★ 追加（任意）
    public int PathDiversity { get; init; } = 0;  // たどった異なるパス数
    public int? MotwZone { get; init; }           // 0..4（不明は null）
}
