// ScoutCore.Reporter/DiscoveryJsonContext.cs
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScoutCore.Reporter;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    NumberHandling = JsonNumberHandling.AllowReadingFromString)]
[JsonSerializable(typeof(DiscoveryEvent))]
[JsonSerializable(typeof(Location))]
[JsonSerializable(typeof(Meta))]
[JsonSerializable(typeof(Flags))]
[JsonSerializable(typeof(Content))]
[JsonSerializable(typeof(Sample))]
[JsonSerializable(typeof(Hit))]
[JsonSerializable(typeof(Score))]
[JsonSerializable(typeof(Agent))]
[JsonSerializable(typeof(List<Hit>))]
internal partial class DiscoveryJsonContext : JsonSerializerContext
{
}

public sealed class DiscoveryEvent
{
    [JsonPropertyName("ts")] public DateTimeOffset Ts { get; set; }
    public string? TenantId { get; set; }
    public string? DeviceId { get; set; }
    public string? UserId { get; set; }
    public string? EventId { get; set; }
    public string? Kind { get; set; }
    public string? Path { get; set; }
    public Location? Location { get; set; }
    public Meta? Meta { get; set; }
    public Flags? Flags { get; set; }
    public Content? Content { get; set; }
    public Agent? Agent { get; set; }
}



public sealed class Location { public string? Type { get; set; } }

public sealed class Meta
{
    public long? Size { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? LastModifiedAt { get; set; }
    public DateTimeOffset? LastAccessedAt { get; set; }
    public string? Extension { get; set; }
    public string? Mime { get; set; }
}

public sealed class Flags
{
    public bool? Encrypted { get; set; }
    public bool? PasswordProtected { get; set; }
}

public sealed class Content
{
    public string? Hash { get; set; }
    public Sample? Sample { get; set; }
    public List<Hit>? Hits { get; set; }
    public Score? Score { get; set; }
    public bool? OcrApplied { get; set; }
}

public sealed class Sample { public string? Head { get; set; } public string? Tail { get; set; } }

public sealed class Hit { public string RuleId { get; set; } = ""; public int Count { get; set; } }

public sealed class Score { public int Value { get; set; } public string Level { get; set; } = "none"; }

public sealed class Agent { public string? Ver { get; set; } public string? Os { get; set; } }
