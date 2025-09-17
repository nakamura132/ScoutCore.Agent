using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Runtime.InteropServices;
using ScoutCore.Agent.Journal;

namespace ScoutCore.Agent.Journal;

/// <summary>
/// FileSystemWatcher ベースの簡易イベントソース（プロセス情報は取得不可・null）
/// </summary>
public sealed class FswEventSource : IFileEventSource
{
    private readonly FileSystemWatcher _w;
    private readonly Channel<FileEvent> _ch;
    private readonly ConcurrentDictionary<string, string> _lastRenameFrom = new(StringComparer.OrdinalIgnoreCase);

    public FswEventSource(string rootPath, string filter = "*.*", bool includeSubdirectories = true)
    {
        _w = new FileSystemWatcher(rootPath, filter) {
            IncludeSubdirectories = includeSubdirectories,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime
        };

        // バッファ溢れ対策：適度に大きめ。ただし限界あり。
        _w.InternalBufferSize = 64 * 1024;

        _ch = Channel.CreateUnbounded<FileEvent>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });

        _w.Created += (_, e) => Publish(FileOpKind.Create, e.FullPath, null);
        _w.Changed += (_, e) => Publish(FileOpKind.Modify, e.FullPath, null);
        _w.Deleted += (_, e) => Publish(FileOpKind.Delete, e.FullPath, null);
        _w.Renamed += (_, e) =>
        {
            // Renamed は旧パス・新パスのペアを送る
            Publish(FileOpKind.Rename, e.FullPath, e.OldFullPath);
            // 追加：Move に近いものとして解釈する場合はここで Move も派生可
        };
        _w.Error += (_, e) =>
        {
            // バッファオーバーフロー等。必要ならリスタート（今回はログ想定）。
            // Console.Error.WriteLine($"[FSW] Error: {e.GetException()}");
        };

        _w.EnableRaisingEvents = true;
    }

    private void Publish(FileOpKind kind, string path, string? fromPath)
    {
        var now = DateTimeOffset.UtcNow;
        var fileKey = FileKeyUtil.TryGetFileKey(path) ?? path; // 取れなければパスを暫定キー
        var ev = new FileEvent(
            Time: now,
            FileKey: fileKey,
            FromPath: fromPath,
            ToPath: path,
            Kind: kind,
            ProcessId: null,
            ProcessName: null,
            UserSid: null
        );
        _ = _ch.Writer.TryWrite(ev);
    }

    public async IAsyncEnumerable<FileEvent> ConsumeAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        // Channel から読み出してそのまま流す
        while (await _ch.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
        {
            while (_ch.Reader.TryRead(out var ev))
                yield return ev;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _w.EnableRaisingEvents = false;
        await Task.Yield();
        _w.Dispose();
    }
}
