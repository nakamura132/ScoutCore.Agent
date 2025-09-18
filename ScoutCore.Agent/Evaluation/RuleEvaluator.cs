using System.Text;
using System.Text.RegularExpressions;
using ScoutCore.Agent.Models;

namespace ScoutCore.Agent.Evaluation;

/// <summary>
/// コンテンツ（テキスト）に対してルールを適用して Hits/Score を算出する Evaluator。
/// - 入力は ScanContext（スキャナで収集済みのメタ・サンプル等）
/// - テキストの取得は注入可能（既定: Sample の Head/Tail を結合。無ければ小容量テキストのみ再読込）
/// </summary>
public sealed class RuleEvaluator : IContentEvaluator
{
    private readonly RuleSet _rules;
    private readonly Func<ScanContext, string?> _textAccessor;

    public RuleEvaluator( RuleSet rules, Func<ScanContext, string?>? textAccessor = null )
    {
        _rules = rules ?? throw new ArgumentNullException( nameof( rules ) );
        _textAccessor = textAccessor ?? DefaultTextAccessor;
    }

    public EvaluationResult Evaluate( ScanContext ctx )
    {
        var text = _textAccessor(ctx);

        if ( string.IsNullOrEmpty( text ) || _rules.Rules is null || _rules.Rules.Count == 0 )
            return new EvaluationResult();

        var hits = new List<Hit>();
        int score = 0;
        var reasons = new List<string>();

        foreach ( var rule in _rules.Rules )
        {
            int count = 0;
            switch ( rule.Type.ToLowerInvariant() )
            {
                case "keyword":
                    if ( rule.Patterns is { Count: > 0 } )
                    {
                        foreach ( var kw in rule.Patterns )
                        {
                            if ( string.IsNullOrWhiteSpace( kw ) ) continue;
                            count += CountOccurrences( text!, kw );
                        }
                    }
                    break;

                case "regex":
                    if ( !string.IsNullOrEmpty( rule.Pattern ) )
                    {
                        try
                        {
                            var m = Regex.Matches(text!, rule.Pattern,
                                      RegexOptions.Compiled | RegexOptions.Multiline);
                            count += m.Count;
                        }
                        catch
                        {
                            // 無効な正規表現はスキップ
                        }
                    }
                    break;
            }

            if ( count > 0 )
            {
                hits.Add( new Hit { RuleId = rule.Id, Count = count } );
                score += rule.Weight * count;
                reasons.Add( $"{rule.Id} x{count} (+{rule.Weight * count})" );
            }
        }

        return new EvaluationResult
        {
            Hits = hits,
            Score = ScoreFromValue( score, _rules.Scoring ),
            Reasons = reasons
        };
    }

    // ===== テキスト取得（既定実装） =====
    private static string? DefaultTextAccessor( ScanContext ctx )
    {
        // 1) まずは Sample（Head/Tail）があれば結合して使う
        var head = ctx.Content.Sample?.Head;
        var tail = ctx.Content.Sample?.Tail;
        if ( !string.IsNullOrEmpty( head ) || !string.IsNullOrEmpty( tail ) )
            return ( head ?? "" ) + "\n" + ( tail ?? "" );

        // 2) 小さなテキストファイルは再読込（評価はスキャナ外にあるため）
        if ( ctx.Meta.Size <= 1_000_000 && // 1MB以下のみ
            ctx.Meta.Mime is string mime &&
            ( mime.StartsWith( "text/" ) || mime is "application/json" or "application/xml" ) )
        {
            try
            {
                return File.ReadAllText( ctx.FilePath, Encoding.UTF8 );
            }
            catch { /* 読めなければ null */ }
        }

        return null;
    }

    // ===== ヘルパ =====
    private static int CountOccurrences( string text, string keyword )
    {
        int count = 0, idx = 0;
        while ( ( idx = text.IndexOf( keyword, idx, StringComparison.Ordinal ) ) >= 0 )
        {
            count++;
            idx += keyword.Length;
        }
        return count;
    }

    private static Score ScoreFromValue( int v, ScoreThresholds th )
    {
        string level = "none";
        if ( v >= th.High ) level = "high";
        else if ( v >= th.Medium ) level = "medium";
        else if ( v >= th.Low ) level = "low";
        return new Score { Value = v, Level = level };
    }
}
