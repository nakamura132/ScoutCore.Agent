namespace ScoutCore.Agent.Models;

public sealed class RuleSet
{
    public int Version { get; set; } = 1;
    public List<Rule> Rules { get; set; } = new();
    public ScoreThresholds Scoring { get; set; } = new();
}

public sealed class Rule
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "keyword"; // "keyword" | "regex"
    public List<string>? Patterns { get; set; }    // keyword
    public string? Pattern { get; set; }           // regex
    public int Weight { get; set; } = 1;
    public List<string>? MimeInclude { get; set; } // 予約（将来）
}

public sealed class ScoreThresholds
{
    public int Low { get; set; } = 3;
    public int Medium { get; set; } = 7;
    public int High { get; set; } = 12;
}
