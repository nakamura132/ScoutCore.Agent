namespace ScoutCore.Agent.Models;

public sealed class AgentInfo
{
    public string Ver { get; set; } = "1.0.0-poc";
    public string Os { get; set; } = $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription}";
}
