namespace ScoutCore.Agent.Evaluation.Abstractions;

public interface IEvaluator<in TInput, out TOutput>
{
    TOutput Evaluate( TInput input );
}

// 既存の評価結果を再利用する想定（必要に応じて Models に置いた Score/Hit 等を使う）
public sealed class EvaluationResult
{
    public int Value { get; init; }
    public string Level { get; init; } = "none";
    public List<string> Reasons { get; init; } = new();
}
