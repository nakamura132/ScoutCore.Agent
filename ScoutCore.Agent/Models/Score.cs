namespace ScoutCore.Agent.Models;

public sealed class Score
{
    public int Value { get; set; }
    public string Level { get; set; } = "none"; // none/low/medium/high
}
