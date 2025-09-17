using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ScoutCore.Agent.Models;

namespace ScoutCore.Agent.Scanning;

public sealed class ContentScanner : IScanner
{
    private readonly RuleSet _rules;
    private const int SampleBytes = 512; // 頭/尾のサンプル長

    public ContentScanner(RuleSet rules) => _rules = rules;

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
            ApplyScore(ctx); // 本文未取得でもスコアは 0 → Level none
            return;
        }

        string text;
        try
        {
            text = ReadFileAsText(fi.FullName);
        }
        catch
        {
            ApplyScore(ctx);
            return;
        }

        // サンプル抽出
        var bytes = Encoding.UTF8.GetBytes(text);
        var head = Encoding.UTF8.GetString(bytes.Take(SampleBytes).ToArray());
        var tail = Encoding.UTF8.GetString(bytes.Skip(Math.Max(0, bytes.Length - SampleBytes)).ToArray());
        ctx.Content.Sample = new Sample { Head = head, Tail = tail };

        // ルール適用
        var hits = new List<Hit>();
        int score = 0;

        foreach (var rule in _rules.Rules)
        {
            int count = 0;
            switch (rule.Type.ToLowerInvariant())
            {
                case "keyword":
                    if (rule.Patterns is { Count: > 0 })
                    {
                        foreach (var kw in rule.Patterns)
                        {
                            if (string.IsNullOrEmpty(kw)) continue;
                            count += CountOccurrences(text, kw);
                        }
                    }
                    break;

                case "regex":
                    if (!string.IsNullOrEmpty(rule.Pattern))
                    {
                        try
                        {
                            var m = Regex.Matches(text, rule.Pattern, RegexOptions.Compiled | RegexOptions.Multiline);
                            count += m.Count;
                        }
                        catch { /* 無効な正規表現はスキップ */ }
                    }
                    break;

                default:
                    break;
            }

            if (count > 0)
            {
                hits.Add(new Hit { RuleId = rule.Id, Count = count });
                score += rule.Weight * count;
            }
        }

        ctx.Content.Hits = hits;
        ctx.Content.Score = ScoreFromValue(score, _rules.Scoring);
    }
    /// <summary>
    /// 本文未取得・例外時など、ヒット無しとしてスコア計算だけ行うヘルパー。
    /// ヒット 0、スコア 0（Level "none"）をセットします。
    /// </summary>
    /// <param name="ctx">スキャンコンテキスト</param>
    private void ApplyScore(ScanContext ctx)
    {
        // 念のため既存のヒットをクリア
        ctx.Content.Hits = new List<Hit>();
        ctx.Content.Score = ScoreFromValue(0, _rules.Scoring);
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
