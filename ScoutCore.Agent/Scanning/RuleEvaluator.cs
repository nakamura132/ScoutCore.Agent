using System.Text.RegularExpressions;
using ScoutCore.Agent.Models;

namespace ScoutCore.Agent.Scanning;

public sealed class RuleEvaluator : IEvaluator
{
    public EvaluationResult Evaluate(string text, RuleSet rules)
    {
        var result = new EvaluationResult();
        if (string.IsNullOrEmpty(text) || rules?.Rules is null) return result;

        var hits = new List<Hit>();
        int score = 0;

        foreach (var rule in rules.Rules)
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
            }

            if (count > 0)
            {
                hits.Add(new Hit { RuleId = rule.Id, Count = count });
                score += rule.Weight * count;
            }
        }

        result.Hits = hits;
        result.Score = ScoreFromValue(score, rules.Scoring);
        return result;
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
