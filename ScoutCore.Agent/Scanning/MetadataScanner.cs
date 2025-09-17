using ScoutCore.Agent.Models;

namespace ScoutCore.Agent.Scanning;

public sealed class MetadataScanner : IScanner
{
    public void Scan(ScanContext ctx)
    {
        var fi = new FileInfo(ctx.FilePath);
        ctx.Meta.Size          = fi.Exists ? fi.Length : 0;
        ctx.Meta.CreatedAt     = fi.Exists ? fi.CreationTimeUtc  : null;
        ctx.Meta.LastModifiedAt= fi.Exists ? fi.LastWriteTimeUtc : null;
        ctx.Meta.LastAccessedAt= fi.Exists ? fi.LastAccessTimeUtc: null;
        ctx.Meta.Extension     = fi.Extension?.ToLowerInvariant();

        // 超簡易 MIME 推定（PoC）
        ctx.Meta.Mime = GuessMime(ctx.Meta.Extension);
    }

    private static string GuessMime(string? ext) => ext switch
    {
        ".txt" => "text/plain",
        ".md"  => "text/markdown",
        ".csv" => "text/csv",
        ".log" => "text/plain",
        ".json"=> "application/json",
        ".xml" => "application/xml",
        ".pdf" => "application/pdf",
        ".docx"=> "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx"=> "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream"
    };
}
