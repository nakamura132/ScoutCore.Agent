using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ScoutCore.Agent.Models;

namespace ScoutCore.Agent.Scanning;

public static class RuleEngine
{
    public static RuleSet LoadFromYamlFile(string path)
    {
        if (!File.Exists(path))
        {
            // ルール未指定でも動くように空のセット返却
            return new RuleSet
            {
                Version = 1,
                Rules = new(),
                Scoring = new ScoreThresholds()
            };
        }

        var yaml = File.ReadAllText(path);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        var obj = deserializer.Deserialize<RuleYamlRoot>(yaml) ?? new RuleYamlRoot();
        return Convert(obj);
    }

    private static RuleSet Convert(RuleYamlRoot y)
    {
        var set = new RuleSet
        {
            Version = y.Version,
            Scoring = new ScoreThresholds
            {
                Low = y.Scoring?.Thresholds?.Low ?? 3,
                Medium = y.Scoring?.Thresholds?.Medium ?? 7,
                High = y.Scoring?.Thresholds?.High ?? 12
            }
        };

        if (y.Rules is { Count: >0 })
        {
            foreach (var r in y.Rules)
            {
                set.Rules.Add(new Rule
                {
                    Id = r.Id ?? Guid.NewGuid().ToString("N"),
                    Type = r.Type ?? "keyword",
                    Patterns = r.Patterns,
                    Pattern = r.Pattern,
                    Weight = r.Weight ?? 1,
                    MimeInclude = r.Mimes
                });
            }
        }
        return set;
    }

    // YAML マッピング用
    private sealed class RuleYamlRoot
    {
        public int Version { get; set; } = 1;
        public List<RuleYaml>? Rules { get; set; }
        public ScoringYaml? Scoring { get; set; }
    }

    private sealed class RuleYaml
    {
        public string? Id { get; set; }
        public string? Type { get; set; } // keyword | regex
        public List<string>? Patterns { get; set; } // for keyword
        public string? Pattern { get; set; }        // for regex
        public int? Weight { get; set; }
        public List<string>? Mimes { get; set; }    // reserved
    }

    private sealed class ScoringYaml
    {
        public ThresholdsYaml? Thresholds { get; set; }
    }
    private sealed class ThresholdsYaml
    {
        public int Low { get; set; } = 3;
        public int Medium { get; set; } = 7;
        public int High { get; set; } = 12;
    }
}
