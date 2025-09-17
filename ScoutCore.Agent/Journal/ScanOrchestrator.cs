using ScoutCore.Agent.Scanning;
using ScoutCore.Agent.Models;

namespace ScoutCore.Agent.Journal;

/// <summary>
/// スキャナとジャーナルを束ねて「スキャン＋履歴付きコンテキスト」を返すオーケストレータ
/// </summary>
public sealed class ScanOrchestrator
{
    private readonly IScanner _scanner;
    private readonly IJournalQuery _journal;

    public ScanOrchestrator(IScanner scanner, IJournalQuery journal)
    {
        _scanner = scanner;
        _journal = journal;
    }

    public async Task<ScanContext> ScanWithJournalAsync(string path, CancellationToken ct)
    {
        // ScanContext は (string filePath, DateTimeOffset scanTime) のコンストラクタを持つはず
        var ctx = new ScanContext(path, DateTimeOffset.UtcNow);

        _scanner.Scan(ctx);

        // Content.Hash を簡易的に FileKey とみなす
        if (!string.IsNullOrEmpty(ctx.Content.Hash))
        {
            var summary = await _journal.GetSummaryAsync(ctx.Content.Hash, ct);
            // Content に JournalSummary をぶら下げられるように拡張する必要あり
            ctx.Content.Journal = summary;
        }

        return ctx;
    }
}
