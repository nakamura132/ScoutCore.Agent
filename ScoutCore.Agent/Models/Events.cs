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
    public Acl? Acl { get; set; } = null; // PoCではnullのまま
    public Flags Flags { get; set; } = new();
    public ContentSummary Content { get; set; } = new();
    public AgentInfo Agent { get; set; } = new();
}

public sealed class FileLocation
{
    public string Type { get; set; } = "local"; // local/network/removable など将来
    public string? Mount { get; set; } = null;
}

public sealed class Meta
{
    public long Size { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? LastModifiedAt { get; set; }
    public DateTimeOffset? LastAccessedAt { get; set; }
    public string? Extension { get; set; }
    public string? Mime { get; set; }
}

public sealed class Acl
{
    public string? Owner { get; set; }
    public string? Sharing { get; set; } // org/public/private など将来カテゴリ化
}

public sealed class Flags
{
    public bool Encrypted { get; set; } = false;
    public bool PasswordProtected { get; set; } = false;
}

public sealed class ContentSummary
{
    public string? Hash { get; set; }
    public Sample? Sample { get; set; }
    public List<Hit> Hits { get; set; } = new();
    public Score Score { get; set; } = new();
    public bool OcrApplied { get; set; } = false; // PoC: false 固定
}

public sealed class Sample
{
    public string? Head { get; set; }
    public string? Tail { get; set; }
}

public sealed class Hit
{
    public string RuleId { get; set; } = "";
    public int Count { get; set; }
}

public sealed class Score
{
    public int Value { get; set; }
    public string Level { get; set; } = "none"; // none/low/medium/high
}

public sealed class AgentInfo
{
    public string Ver { get; set; } = "1.0.0-poc";
    public string Os { get; set; } = $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription}";
}

// 走査時の作業バッファ
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
