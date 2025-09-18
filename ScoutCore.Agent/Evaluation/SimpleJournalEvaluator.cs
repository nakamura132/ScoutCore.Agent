using ScoutCore.Agent.Journal;
using ScoutCore.Agent.Models;

namespace ScoutCore.Agent.Evaluation;

public sealed class SimpleJournalEvaluator : IJournalEvaluator
{
    public EvaluationResult Evaluate( JournalSummary s )
    {
        int v = 0;
        var reasons = new List<string>();

        // あるものだけ使う（Rename/Copy/LastProc/LastSeenAt）
        if ( s.OpCounts.TryGetValue( FileOpKind.Rename, out var rn ) && rn >= 3 )
        {
            v += 20; reasons.Add( "Many renames" );
        }
        if ( s.OpCounts.TryGetValue( FileOpKind.Copy, out var cp ) && cp >= 50 )
        {
            v += 25; reasons.Add( "Bulk copy" );
        }
        if ( !string.IsNullOrEmpty( s.LastProc ) && IsScriptProc( s.LastProc! ) )
        {
            v += 10; reasons.Add( $"Last process: {s.LastProc}" );
        }

        // 時間減衰（τ=24h）
        if ( s.LastSeenAt is DateTimeOffset last )
        {
            var tau = TimeSpan.FromHours(24);
            var decay = Math.Exp(-Math.Max(0, (DateTimeOffset.UtcNow - last).TotalSeconds) / tau.TotalSeconds);
            v = (int)Math.Round( v * decay );
        }

        string level = v >= 60 ? "high" : v >= 30 ? "medium" : v > 0 ? "low" : "none";
        return new EvaluationResult
        {
            Score = new Score { Value = v, Level = level },
            Reasons = reasons
        };
    }

    private static bool IsScriptProc( string p )
    {
        var n = p.ToLowerInvariant();
        return n.Contains( "powershell" ) || n.Contains( "wscript" ) || n.Contains( "cscript" ) || n.Contains( "mshta" );
    }
}
