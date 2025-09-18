using ScoutCore.Agent.Models;

namespace ScoutCore.Agent.Evaluation;

/// <summary>
/// ルール評価の結果（既存の Hits/Score を踏襲）
/// </summary>
public sealed class EvaluationResult
{
    public List<Hit> Hits { get; init; } = new();
    public Score Score { get; init; } = new();
    /// <summary>説明責任用（どのルールで加点されたか等）</summary>
    public List<string> Reasons { get; init; } = new();
}
