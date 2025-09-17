namespace ScoutCore.Agent.Models;

public sealed class DiscoveryEvent
{
    public string Ts { get; set; } = DateTimeOffset.UtcNow.ToString("O");
    public string TenantId { get; set; } = "t-001";
    public string DeviceId { get; set; } = "";
    public string UserId { get; set; } = "";
    public string EventId { get; set; } = Guid.NewGuid().ToString("N");
    public string Kind { get; set; } = "discovery";

    public string Path { get; set; } = "";
    public FileLocation Location { get; set; } = new();
    public Meta Meta { get; set; } = new();
    public Acl? Acl { get; set; } = null;
    public Flags Flags { get; set; } = new();
    public ContentSummary Content { get; set; } = new();
    public AgentInfo Agent { get; set; } = new();
}
