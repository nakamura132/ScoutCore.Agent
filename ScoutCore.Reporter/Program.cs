using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ScoutCore.Reporter
{
    internal static class Program
    {
        static async Task<int> Main(string[] args)
        {
            var (input, output) = ParseArgs(args);
            output ??= "report.html";

            IEnumerable<string> lines;
            if (!string.IsNullOrEmpty(input))
            {
                if (!File.Exists(input))
                {
                    Console.Error.WriteLine($"入力ファイルが見つかりません: {input}");
                    return 1;
                }
                lines = File.ReadLines(input, Encoding.UTF8);
            }
            else
            {
                using var sr = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);
                var all = await sr.ReadToEndAsync();
                lines = all.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            }

            var events = new List<DiscoveryEvent>();
            foreach (var line in lines)
            {
                try
                {
                    // ソース生成されたメタデータを使用（AOT/トリミング安全）
                    var ev = JsonSerializer.Deserialize(
                        line,
                        DiscoveryJsonContext.Default.DiscoveryEvent);
                    if (ev != null && string.Equals(ev.Kind, "discovery", StringComparison.OrdinalIgnoreCase))
                        events.Add(ev);
                }
                catch { /* 壊れた行はスキップ */ }
            }

            var total = events.Count;
            var byLevel = events
                .GroupBy(e => e.Content?.Score?.Level ?? "none")
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            var ruleAgg = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in events)
            {
                var hits = e.Content?.Hits;
                if (hits == null) continue;
                foreach (var h in hits)
                {
                    ruleAgg[h.RuleId] = ruleAgg.TryGetValue(h.RuleId, out var c) ? c + h.Count : h.Count;
                }
            }

            var ordered = events
                .OrderByDescending(e => e.Content?.Score?.Value ?? 0)
                .ThenBy(e => e.Content?.Score?.Level ?? "none")
                .ThenBy(e => e.Ts)
                .ToList();

            var html = BuildHtmlReport(ordered, total, byLevel, ruleAgg);
            await File.WriteAllTextAsync(output, html, new UTF8Encoding(false));
            Console.WriteLine($"レポートを出力しました: {Path.GetFullPath(output)}");
            return 0;
        }

        private static (string? InputPath, string? OutputPath) ParseArgs(string[] args)
        {
            string? input = null, output = null;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-i":
                    case "--input":
                        input = i + 1 < args.Length ? args[++i] : null; break;
                    case "-o":
                    case "--output":
                        output = i + 1 < args.Length ? args[++i] : null; break;
                }
            }
            return (input, output);
        }

        private static string BuildHtmlReport(
            List<DiscoveryEvent> events,
            int total,
            Dictionary<string, int> byLevel,
            Dictionary<string, int> ruleAgg)
        {
            static string Lv(string? lv) => (lv ?? "none").ToLowerInvariant();
            static string Html(string? s) => s is null ? "" : HtmlEncoder.Default.Encode(s);
            static string Card(string label, string value, string? badgeCls = null)
            {
                var val = badgeCls is null ? $"<div class=\"v\">{Html(value)}</div>"
                                           : $"<div class=\"v\"><span class=\"{badgeCls}\">{Html(value)}</span></div>";
                return $"<div class=\"card\"><div class=\"k\">{Html(label)}</div>{val}</div>";
            }
            string Badge(string? lv)
            {
                var cls = Lv(lv) switch
                {
                    "high" => "b-high",
                    "medium" => "b-med",
                    "low" => "b-low",
                    _ => "b-none"
                };
                return $"<span class=\"badge {cls}\">{Html(lv)}</span>";
            }

            var sb = new StringBuilder();
            sb.Append("""
<!doctype html>
<html lang="ja">
<meta charset="utf-8">
<title>ScoutCore Discovery Report</title>
<style>
  :root { --fg:#1b1f24; --bg:#f8fafc; --muted:#6b7280; --border:#e5e7eb;
          --high:#dc2626; --med:#d97706; --low:#2563eb; --none:#64748b; }
  body { font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,"Noto Sans JP",sans-serif;
         color:var(--fg); background:var(--bg); margin:24px; }
  h1 { margin:0 0 16px; }
  .grid { display:grid; grid-template-columns: repeat(4, minmax(160px, 1fr)); gap:12px; margin:12px 0 24px;}
  .card { background:#fff; border:1px solid var(--border); border-radius:12px; padding:16px; box-shadow:0 1px 2px rgba(0,0,0,.04); }
  .k { color:var(--muted); font-size:12px; }
  .v { font-size:20px; font-weight:600; }
  table { width:100%; border-collapse:collapse; background:#fff; border:1px solid var(--border); border-radius:12px; overflow:hidden; }
  th, td { padding:10px 12px; border-bottom:1px solid var(--border); vertical-align:top; }
  th { text-align:left; background:#f3f4f6; font-weight:600; }
  .path { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, "Liberation Mono", monospace; }
  .muted { color:var(--muted); }
  .badge { padding:2px 8px; border-radius:999px; font-size:12px; font-weight:700; color:#fff; }
  .b-high { background:var(--high); }
  .b-med { background:var(--med); }
  .b-low { background:var(--low); }
  .b-none { background:var(--none); }
  details { margin:8px 0; }
  summary { cursor:pointer; color:#111827; }
  code { background:#f3f4f6; padding:2px 4px; border-radius:6px; }
  .hits { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, "Liberation Mono", monospace; }
  .rules { display:flex; gap:8px; flex-wrap:wrap; }
  .pill { border:1px solid var(--border); padding:2px 8px; border-radius:999px; background:#fff; }
  .right { text-align:right; }
</style>
<h1>ScoutCore Discovery Report</h1>
""");

            sb.Append("<div class=\"grid\">");
            sb.Append(Card("Total", total.ToString("N0")));
            sb.Append(Card("High", byLevel.TryGetValue("high", out var h) ? h.ToString("N0") : "0", "badge b-high"));
            sb.Append(Card("Medium", byLevel.TryGetValue("medium", out var m) ? m.ToString("N0") : "0", "badge b-med"));
            sb.Append(Card("Low", byLevel.TryGetValue("low", out var l) ? l.ToString("N0") : "0", "badge b-low"));
            sb.Append("</div>");

            if (ruleAgg.Count > 0)
            {
                sb.Append("<div class=\"card\"><div class=\"k\">Rule hits (aggregated)</div><div class=\"rules\">");
                foreach (var kv in ruleAgg.OrderByDescending(kv => kv.Value))
                {
                    sb.Append($"<span class=\"pill\"><b>{Html(kv.Key)}</b>&nbsp;×{kv.Value}</span>");
                }
                sb.Append("</div></div>");
            }

            sb.Append("<table><thead><tr>");
            sb.Append("<th>Time</th><th>Level</th><th>Score</th><th>Path</th><th>Hits</th><th>Preview</th>");
            sb.Append("</tr></thead><tbody>");

            foreach (var e in events)
            {
                var level = e.Content?.Score?.Level ?? "none";
                var score = e.Content?.Score?.Value ?? 0;
                var hits = e.Content?.Hits ?? new List<Hit>();
                var hitStr = hits.Count == 0 ? "<span class=\"muted\">-</span>"
                                             : string.Join("; ", hits.Select(hh => $"{Html(hh.RuleId)}×{hh.Count}"));
                var head = e.Content?.Sample?.Head;
                if (!string.IsNullOrEmpty(head) && head.Length > 280) head = head.Substring(0, 280) + " …";
                var tsLocal = e.Ts.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

                sb.Append("<tr>");
                sb.Append($"<td class=\"muted\">{Html(tsLocal)}</td>");
                sb.Append($"<td>{Badge(level)}</td>");
                sb.Append($"<td class=\"right\"><b>{score}</b></td>");
                sb.Append($"<td class=\"path\">{Html(e.Path)}</td>");
                sb.Append($"<td class=\"hits\">{hitStr}</td>");
                sb.Append("<td>");

                if (!string.IsNullOrEmpty(head))
                {
                    sb.Append("<details><summary>head/tail を表示</summary>");
                    sb.Append("<div class=\"muted\"><div><b>Head</b></div><pre><code>");
                    sb.Append(Html(head));
                    sb.Append("</code></pre>");
                    if (!string.IsNullOrEmpty(e.Content?.Sample?.Tail))
                    {
                        sb.Append("<div><b>Tail</b></div><pre><code>");
                        sb.Append(Html(e.Content.Sample.Tail));
                        sb.Append("</code></pre>");
                    }
                    sb.Append("<div><b>Meta</b></div><div class=\"muted\">");
                    if (e.Meta?.Size is long sz) sb.Append($"size: {sz:N0} bytes<br>");
                    if (!string.IsNullOrEmpty(e.Meta?.Mime)) sb.Append($"mime: {Html(e.Meta.Mime)}<br>");
                    if (!string.IsNullOrEmpty(e.Content?.Hash)) sb.Append($"hash: <code>{Html(e.Content.Hash)}</code><br>");
                    sb.Append("</div></details>");
                }
                else
                {
                    sb.Append("<span class=\"muted\">(no preview)</span>");
                }

                sb.Append("</td></tr>");
            }

            sb.Append("</tbody></table></html>");
            return sb.ToString();
        }
    }
}
