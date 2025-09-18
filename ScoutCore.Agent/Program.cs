using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScoutCore.Agent.Models;
using ScoutCore.Agent.Scanning;
using ScoutCore.Agent.Journal;
using ScoutCore.Agent.Evaluation;               // ★ 追加: 評価（Content/Journal）

// 引数
var argsDict = ParseArgs(args);
if ( !argsDict.TryGetValue( "--root", out var root ) || string.IsNullOrWhiteSpace( root ) )
{
    Console.Error.WriteLine( "Usage: dotnet run -- --root <scan-root> --rules <rules.yaml> [--out <file.ndjson>] [--journal-root <watch-root>] [--watch] [--wait <sec>]" );
    return 1;
}
var rulesPath    = argsDict.TryGetValue("--rules", out var r) ? r : "rules.yaml";
var outPath      = argsDict.TryGetValue("--out", out var o) ? o : null;
// 監視ルート（未指定なら --root と同じ）
var journalRoot  = argsDict.TryGetValue("--journal-root", out var jr) && !string.IsNullOrWhiteSpace(jr) ? jr : root;

// 常駐オプション
var watchMode = argsDict.ContainsKey("--watch");
var waitSec   = argsDict.TryGetValue("--wait", out var ws) && int.TryParse(ws, out var tmp) ? tmp : 0;

// ルール読み込み
var ruleSet = RuleEngine.LoadFromYamlFile(rulesPath);

// ==== スキャナ（収集のみ） ====
var scanners = new IScanner[]
{
    new MetadataScanner(),
    new ContentScanner() // ★ 収集のみ。評価は下の Evaluator が担当
};

// ==== Evaluator 準備（新）====
var contentEval = new RuleEvaluator(ruleSet);     // コンテンツ評価
var journalEval = new SimpleJournalEvaluator();   // ジャーナル評価（シンプル実装）

// 監視セットアップ（必要時のみ実行）
CancellationTokenSource? cts = null;
Task? ingestTask = null;
InMemoryJournalStore? store = null;
if ( watchMode || waitSec > 0 )
{
    cts = new CancellationTokenSource();
    Console.CancelKeyPress += ( s, e ) => { e.Cancel = true; cts.Cancel(); };

    store = new InMemoryJournalStore();
    var source = new FswEventSource(rootPath: journalRoot, filter: "*.*", includeSubdirectories: true);
    var ingest = new FswIngestLoop(source, store);

    ingestTask = Task.Run( () => ingest.RunAsync( cts.Token ) );

    // 監視を先に走らせる
    Console.WriteLine(
        waitSec > 0
        ? $"[WATCH] Watching '{journalRoot}' for {waitSec} seconds... (Ctrl+C to stop early)"
        : $"[WATCH] Watching '{journalRoot}'. Press Ctrl+C to scan & export."
    );

    try
    {
        if ( waitSec > 0 ) await Task.Delay( TimeSpan.FromSeconds( waitSec ), cts.Token );
        else await Task.Delay( Timeout.Infinite, cts.Token );
    }
    catch ( TaskCanceledException ) { /* Ctrl+C */ }

    Console.WriteLine( "[WATCH] Stopping watcher and running scan..." );
}

// === スキャン → 評価 → 合成 ===
var outputs = new List<DiscoveryEvent>();
var now = DateTimeOffset.UtcNow;

// 再帰走査（PoC: シンボリックリンクのループは未対応）
foreach ( var path in EnumerateFilesSafe( root ) )
{
    try
    {
        var ctx = new ScanContext(path, now);

        // 収集のみ
        foreach ( var s in scanners )
            s.Scan( ctx );

        // （任意）ジャーナル要約を付与
        JournalSummary? jSummary = null;
        if ( store is not null )
        {
            var fileKey = FileKeyUtil.TryGetFileKey(path) ?? path; // NTFS: VolumeSerial+FRN / それ以外: パス暫定
            jSummary = await store.GetSummaryAsync( fileKey, CancellationToken.None );
            ctx.Content.Journal = jSummary; // ContentSummary に Journal プロパティがある前提
        }

        // コンテンツ評価
        var cRes = contentEval.Evaluate(ctx);
        ctx.Content.Hits = cRes.Hits;
        ctx.Content.Score = cRes.Score;

        // ジャーナル評価（要約があれば）
        if ( jSummary is not null )
        {
            var jRes = journalEval.Evaluate(jSummary);

            // 合成：content をベースに “1 + journal/100” 係数で底上げ
            var combinedValue = (int)Math.Round(cRes.Score.Value * (1 + (jRes.Score.Value / 100.0)));

            ctx.Content.Score = new Score
            {
                Value = combinedValue,
                Level = MapLevel( combinedValue, ruleSet.Scoring ) // 既存のしきい値に合わせてレベル再算定
            };

            // （必要なら）理由のログ出力
            // Console.WriteLine($"[DEBUG] {path} content={cRes.Score.Value} journal={jRes.Value} -> final={combinedValue} ({string.Join("; ", jRes.Reasons)})");
        }

        outputs.Add( ctx.ToDiscoveryEvent( deviceId: GetDeviceId(), tenantId: "t-001", userId: GetUserName() ) );
    }
    catch ( Exception ex )
    {
        Console.Error.WriteLine( $"[WARN] {path}: {ex.Message}" );
    }
}

// === NDJSON 出力 ===
var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
};

Directory.CreateDirectory( Path.GetDirectoryName( outPath ?? "./" ) ?? "./" );
using ( var writer = outPath is null ? Console.Out : new StreamWriter( outPath, false, new UTF8Encoding( false ) ) )
{
    foreach ( var ev in outputs )
        writer.WriteLine( JsonSerializer.Serialize( ev, options ) );
}

if ( outPath is not null )
    Console.WriteLine( $"[OK] Written {outputs.Count} events to {outPath}" );

// 監視を止める（必要時）
if ( cts is not null )
{
    cts.Cancel();
    try { if ( ingestTask is not null ) await ingestTask; } catch { /* 終了時例外は握りつぶし */ }
}

return 0;

// ----------------- helpers -----------------
static Dictionary<string, string> ParseArgs( string[] a )
{
    var d = new Dictionary<string,string>();
    for ( int i = 0; i < a.Length; i++ )
    {
        if ( a[i].StartsWith( "--" ) )
        {
            var key = a[i];
            var val = (i+1 < a.Length && !a[i+1].StartsWith("--")) ? a[++i] : "true";
            d[key] = val;
        }
    }
    return d;
}

static IEnumerable<string> EnumerateFilesSafe( string root )
{
    var stack = new Stack<string>();
    stack.Push( root );
    while ( stack.Count > 0 )
    {
        var dir = stack.Pop();
        IEnumerable<string> files = Array.Empty<string>();
        IEnumerable<string> dirs = Array.Empty<string>();
        try { files = Directory.EnumerateFiles( dir ); } catch { }
        try { dirs = Directory.EnumerateDirectories( dir ); } catch { }

        foreach ( var f in files ) yield return f;
        foreach ( var d in dirs ) stack.Push( d );
    }
}

static string GetUserName()
    => Environment.UserName;

static string GetDeviceId()
{
    // PoC: マシン名 + OS + CPUアーキテクチャをハッシュ
    var sig = $"{Environment.MachineName}|{Environment.OSVersion}|{RuntimeInformation.OSArchitecture}";
    using var sha = SHA256.Create();
    var hash = Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(sig)));
    return $"dev-{hash[..12].ToLowerInvariant()}";
}

// 既存しきい値に合わせて Level を決定
static string MapLevel( int value, ScoreThresholds th )
    => value >= th.High ? "high"
     : value >= th.Medium ? "medium"
     : value >= th.Low ? "low"
     : "none";
