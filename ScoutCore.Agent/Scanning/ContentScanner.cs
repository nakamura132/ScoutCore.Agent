using System.Security.Cryptography;
using System.Text;
using ScoutCore.Agent.Models;

namespace ScoutCore.Agent.Scanning;

/// <summary>
/// コンテンツの静的情報を収集するだけのスキャナ（ハッシュ・テキストサンプル）
/// 評価（Hits/Score）は行わない。評価は Evaluation 層で別途実施する。
/// </summary>
public sealed class ContentScanner : IScanner
{
    private const int SampleBytes = 512;          // Head/Tail 抽出長
    private const long HashSizeLimit = 256L * 1024 * 1024; // 256MB 以上はハッシュスキップ

    public void Scan( ScanContext ctx )
    {
        var fi = new FileInfo(ctx.FilePath);
        if ( !fi.Exists ) return;

        // ハッシュ（サイズ上限内のみ）
        if ( fi.Length <= HashSizeLimit )
        {
            using var fs = fi.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
            ctx.Content.Hash = "sha256:" + ComputeSha256( fs );
        }

        // テキストライク判定（MIME ベース）
        var isTextLike = ctx.Meta.Mime is not null &&
                         (ctx.Meta.Mime.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                          || ctx.Meta.Mime is "application/json" or "application/xml");

        if ( !isTextLike || fi.Length == 0 )
        {
            // テキスト抽出対象でない場合は何もしない（評価は外部）
            return;
        }

        string text;
        try
        {
            text = ReadFileAsText( fi.FullName );
        }
        catch
        {
            // 読めなければ諦める（評価は外部）
            return;
        }

        // サンプル抽出（Head/Tail）
        var bytes = Encoding.UTF8.GetBytes(text);
        var head = Encoding.UTF8.GetString(bytes.Take(SampleBytes).ToArray());
        var tail = Encoding.UTF8.GetString(bytes.Skip(Math.Max(0, bytes.Length - SampleBytes)).ToArray());
        ctx.Content.Sample = new Sample { Head = head, Tail = tail };

        // ※ ここで評価（Hits/Score）は行わない。RuleEvaluator など別コンポーネントで実施する。
    }

    private static string ComputeSha256( Stream s )
    {
        s.Position = 0;
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(s);
        s.Position = 0;
        return Convert.ToHexString( hash ).ToLowerInvariant();
    }

    private static string ReadFileAsText( string path )
    {
        // 簡易 BOM 判定
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);
        var preamble = br.ReadBytes(4);
        fs.Position = 0;

        if ( preamble.Length >= 3 && preamble[0] == 0xEF && preamble[1] == 0xBB && preamble[2] == 0xBF )
            return File.ReadAllText( path, new UTF8Encoding( encoderShouldEmitUTF8Identifier: false ) );
        if ( preamble.Length >= 2 && preamble[0] == 0xFF && preamble[1] == 0xFE )
            return File.ReadAllText( path, Encoding.Unicode ); // UTF-16 LE
        if ( preamble.Length >= 2 && preamble[0] == 0xFE && preamble[1] == 0xFF )
            return File.ReadAllText( path, Encoding.BigEndianUnicode );

        // 既定は UTF-8
        return File.ReadAllText( path, Encoding.UTF8 );
    }
}
