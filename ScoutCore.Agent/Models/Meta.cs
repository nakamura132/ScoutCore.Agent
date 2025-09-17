namespace ScoutCore.Agent.Models;

public sealed class Meta
{
    public long Size { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? LastModifiedAt { get; set; }
    public DateTimeOffset? LastAccessedAt { get; set; }
    public string? Extension { get; set; }
    public string? Mime { get; set; }
}
