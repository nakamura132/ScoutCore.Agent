namespace ScoutCore.Agent.Models;

public sealed class FileLocation
{
    public string Type { get; set; } = "local"; // local/network/removable など将来
    public string? Mount { get; set; } = null;
}
