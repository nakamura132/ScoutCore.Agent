namespace ScoutCore.Agent.Models;

/// <summary>
/// 走査時の作業バッファ
/// </summary>
public sealed class ScanContext
{
    public string FilePath { get; }
    public DateTimeOffset Now { get; }
    public Meta Meta { get; } = new();
    public ContentSummary Content { get; } = new();
    public Flags Flags { get; } = new();

    public ScanContext(string filePath, DateTimeOffset now)
    {
        FilePath = filePath;
        Now = now;
    }

    public DiscoveryEvent ToDiscoveryEvent(string deviceId, string tenantId, string userId)
    {
        return new DiscoveryEvent
        {
            Ts = Now.ToString("O"),
            TenantId = tenantId,
            DeviceId = deviceId,
            UserId = userId,
            EventId = Guid.NewGuid().ToString("N"),
            Kind = "discovery",
            Path = FilePath,
            Location = new FileLocation { Type = "local", Mount = null },
            Meta = Meta,
            Acl = null,
            Flags = Flags,
            Content = Content,
            Agent = new AgentInfo()
        };
    }
}
