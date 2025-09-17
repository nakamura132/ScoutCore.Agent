using ScoutCore.Agent.Models;

namespace ScoutCore.Agent.Scanning;

public interface IEvaluator
{
    EvaluationResult Evaluate(string text, RuleSet rules);
}

public sealed class EvaluationResult
{
    public List<Hit> Hits { get; set; } = new();
    public Score Score { get; set; } = new();
    // ルールセットのバージョン等が必要ならここに追加
    // public string RuleSetVersion { get; set; } = "";
}
