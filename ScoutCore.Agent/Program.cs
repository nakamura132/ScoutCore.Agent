using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ScoutCore.Agent.Models;
using ScoutCore.Agent.Scanning;

var argsDict = ParseArgs(args);
if (!argsDict.TryGetValue("--root", out var root) || string.IsNullOrWhiteSpace(root))
{
    Console.Error.WriteLine("Usage: dotnet run -- --root <scan-root> --rules <rules.yaml> [--out <file.ndjson>]");
    return 1;
}
var rulesPath = argsDict.TryGetValue("--rules", out var r) ? r : "rules.yaml";
var outPath   = argsDict.TryGetValue("--out", out var o) ? o : null;

// ルール読み込み
var ruleSet = RuleEngine.LoadFromYamlFile(rulesPath);

// スキャナ構成（PoC: メタ + テキストコンテンツ）
var scanners = new IScanner[]
{
    new MetadataScanner(),
    new ContentScanner(ruleSet)
};

var outputs = new List<DiscoveryEvent>();
var now = DateTimeOffset.UtcNow;

// 再帰走査（PoC: シンボリックリンクのループは未対応）
foreach (var path in EnumerateFilesSafe(root))
{
    try
    {
        var ctx = new ScanContext(path, now);
        foreach (var s in scanners)
            s.Scan(ctx);

        outputs.Add(ctx.ToDiscoveryEvent(deviceId: GetDeviceId(), tenantId: "t-001", userId: GetUserName()));
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"[WARN] {path}: {ex.Message}");
    }
}

// 出力: NDJSON
var options = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
};

Directory.CreateDirectory(Path.GetDirectoryName(outPath ?? "./") ?? "./");
using var writer = outPath is null ? Console.Out : new StreamWriter(outPath, false, new UTF8Encoding(false));

foreach (var ev in outputs)
{
    writer.WriteLine(JsonSerializer.Serialize(ev, options));
}
if (outPath is not null)
{
    Console.WriteLine($"[OK] Written {outputs.Count} events to {outPath}");
}

return 0;

// ----------------- helpers -----------------
static Dictionary<string,string> ParseArgs(string[] a)
{
    var d = new Dictionary<string,string>();
    for (int i=0;i<a.Length;i++)
    {
        if (a[i].StartsWith("--"))
        {
            var key = a[i];
            var val = (i+1 < a.Length && !a[i+1].StartsWith("--")) ? a[++i] : "true";
            d[key]=val;
        }
    }
    return d;
}

static IEnumerable<string> EnumerateFilesSafe(string root)
{
    var stack = new Stack<string>();
    stack.Push(root);
    while (stack.Count>0)
    {
        var dir = stack.Pop();
        IEnumerable<string> files = Array.Empty<string>();
        IEnumerable<string> dirs = Array.Empty<string>();
        try { files = Directory.EnumerateFiles(dir); } catch { }
        try { dirs  = Directory.EnumerateDirectories(dir); } catch { }

        foreach (var f in files) yield return f;
        foreach (var d in dirs) stack.Push(d);
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
