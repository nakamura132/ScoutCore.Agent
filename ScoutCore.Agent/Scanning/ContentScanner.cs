using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ScoutCore.Agent.Models;

namespace ScoutCore.Agent.Scanning;

public sealed class ContentScanner : IScanner
{
    private readonly RuleSet _rules;
    private readonly IEvaluator _evaluator;
    private const int SampleBytes = 512; // 頭/尾のサンプル長

    // 互換コンストラクタ（従来どおり rules だけでも動く）
    public ContentScanner(RuleSet rules) : this(rules, new RuleEvaluator()) { }

    public ContentScanner(RuleSet rules, IEvaluator evaluator)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
    }

    public void Scan(ScanContext ctx)
    {
        // PoC 方針:
        // - 小～中サイズのテキスト系のみ本文スキャン（~10MB程度）
        // - それ以外はハッシュのみ（or スキップ）
        var fi = new FileInfo(ctx.FilePath);
        if (!fi.Exists) return;

        // ハッシュ（全体）— PoC：ファイルサイズが大きすぎる場合はスキップ可
        if (fi.Length <= 256L * 1024 * 1024) // 256MB 上限
        {
            using var fs = fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            ctx.Content.Hash = "sha256:" + ComputeSha256(fs);
        }

        // テキストスキャン対象判定（拡張子ベースの簡易判定）
        var isTextLike = ctx.Meta.Mime is not null &&
                         (ctx.Meta.Mime.StartsWith("text/") || ctx.Meta.Mime == "application/json" || ctx.Meta.Mime == "application/xml");

        if (!isTextLike || fi.Length == 0)
        {
            SetDefaultEvaluation(ctx); // 本文未取得でもスコアは 0 → Level none
            return;
        }

        string text;
        try
        {
            text = ReadFileAsText(fi.FullName);
        }
        catch
        {
            SetDefaultEvaluation(ctx);
            return;
        }

        // サンプル抽出
        var bytes = Encoding.UTF8.GetBytes(text);
        var head = Encoding.UTF8.GetString(bytes.Take(SampleBytes).ToArray());
        var tail = Encoding.UTF8.GetString(bytes.Skip(Math.Max(0, bytes.Length - SampleBytes)).ToArray());
        ctx.Content.Sample = new Sample { Head = head, Tail = tail };

        // 評価は外部コンポーネントに委譲
        var eval = _evaluator.Evaluate(text, _rules);
        ctx.Content.Hits = eval.Hits;
        ctx.Content.Score = eval.Score;
    }
    // 本文未取得・例外時など、ヒット無し/none に初期化
    private static void SetDefaultEvaluation(ScanContext ctx)
    {
        ctx.Content.Hits = new List<Hit>();
        ctx.Content.Score = new Score { Value = 0, Level = "none" };
    }
    private static string ComputeSha256(Stream s)
    {
        s.Position = 0;
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(s);
        s.Position = 0;
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ReadFileAsText(string path)
    {
        // 簡易 BOM 判定
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);
        var preamble = br.ReadBytes(4);
        fs.Position = 0;

        if (preamble.Length >= 3 && preamble[0] == 0xEF && preamble[1] == 0xBB && preamble[2] == 0xBF)
            return File.ReadAllText(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (preamble.Length >= 2 && preamble[0] == 0xFF && preamble[1] == 0xFE)
            return File.ReadAllText(path, Encoding.Unicode); // UTF-16 LE
        if (preamble.Length >= 2 && preamble[0] == 0xFE && preamble[1] == 0xFF)
            return File.ReadAllText(path, Encoding.BigEndianUnicode);

        // 既定は UTF-8 とする
        return File.ReadAllText(path, Encoding.UTF8);
    }

    private static int CountOccurrences(string text, string keyword)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(keyword, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += keyword.Length;
        }
        return count;
    }

    private static Score ScoreFromValue(int v, ScoreThresholds th)
    {
        string level = "none";
        if (v >= th.High) level = "high";
        else if (v >= th.Medium) level = "medium";
        else if (v >= th.Low) level = "low";
        return new Score { Value = v, Level = level };
    }
}
