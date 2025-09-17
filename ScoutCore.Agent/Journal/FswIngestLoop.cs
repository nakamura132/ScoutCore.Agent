using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ScoutCore.Agent.Journal;

/// <summary>
/// 依存ゼロの取り込みループ。Start/Stop は呼び出し側で制御。
/// </summary>
public sealed class FswIngestLoop
{
    private readonly IFileEventSource _source;
    private readonly IJournalStore _store;

    public FswIngestLoop(IFileEventSource source, IJournalStore store)
    {
        _source = source;
        _store  = store;
    }

    /// <summary>
    /// 取り込みループ。呼び出し側で CancellationToken を管理してください。
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        const int maxBatch = 256;
        var buffer = new List<FileEvent>(maxBatch);
        var lastFlush = DateTime.UtcNow;

        try
        {
            await foreach (var ev in _source.ConsumeAsync(ct))
            {
                buffer.Add(ev);

                var needFlush = buffer.Count >= maxBatch || (DateTime.UtcNow - lastFlush) > TimeSpan.FromSeconds(2);
                if (needFlush)
                {
                    try
                    {
                        await _store.AppendAsync(buffer, ct).ConfigureAwait(false);
                    }
                    finally
                    {
                        buffer.Clear();
                        lastFlush = DateTime.UtcNow;
                    }
                }
            }
        }
        finally
        {
            // 終了時に残りをフラッシュ
            if (buffer.Count > 0 && !ct.IsCancellationRequested)
                await _store.AppendAsync(buffer, ct).ConfigureAwait(false);

            await _source.DisposeAsync();
        }
    }
}
