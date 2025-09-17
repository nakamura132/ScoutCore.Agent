using ScoutCore.Agent.Journal;

namespace ScoutCore.Agent.Models;

public sealed class ContentSummary
{
    public string? Hash { get; set; }
    public Sample? Sample { get; set; }
    public List<Hit> Hits { get; set; } = new();
    public Score Score { get; set; } = new();
    public bool OcrApplied { get; set; } = false;
    public JournalSummary? Journal { get; set; }
}
