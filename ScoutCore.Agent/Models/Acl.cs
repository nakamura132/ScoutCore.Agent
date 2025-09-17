namespace ScoutCore.Agent.Models;

public sealed class Acl
{
    public string? Owner { get; set; }
    public string? Sharing { get; set; } // org/public/private など将来カテゴリ化
}
