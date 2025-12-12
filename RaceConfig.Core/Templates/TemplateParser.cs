using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

namespace RaceConfig.Core.Templates;

public static class TemplateParser
{
    public static GameTemplate ParseFromFile(string filePath)
    {
        var raw = File.ReadAllText(filePath);

        var yaml = new YamlStream();
        using var reader = new StringReader(raw);
        yaml.Load(reader);

        var root = (YamlMappingNode)yaml.Documents[0].RootNode;

        var gameName = GetScalar(root, "game") ?? throw new InvalidOperationException("Missing 'game' in template.");
        var description = GetScalar(root, "description");
        var defaultPlayerName = GetScalar(root, "name");

        string? requiredVersion = null;
        if (root.Children.TryGetValue(new YamlScalarNode("requires"), out var reqNode) && reqNode is YamlMappingNode reqMap)
        {
            requiredVersion = GetScalar(reqMap, "version");
        }

        var options = new List<RandomizerOption>();
        if (root.Children.TryGetValue(new YamlScalarNode(gameName), out var gameBlock) && gameBlock is YamlMappingNode gameMap)
        {
            foreach (var kvp in gameMap.Children)
            {
                var key = ((YamlScalarNode)kvp.Key).Value ?? string.Empty;
                var node = kvp.Value;

                if (IsEmptyNode(node))
                    continue;

                switch (node)
                {
                    case YamlMappingNode map:
                        options.Add(ClassifyMappingOption(raw, gameName, key, map));
                        break;

                    case YamlSequenceNode seq:
                        options.Add(new RandomizerOption
                        {
                            KeyPath = $"{gameName}.{key}",
                            DisplayName = key,
                            Type = OptionType.List,
                            DefaultList = seq.Children.OfType<YamlScalarNode>().Select(s => s.Value ?? string.Empty).ToList(),
                            SelectedList = new List<string>()
                        });
                        break;

                    case YamlScalarNode scalar:
                        options.Add(new RandomizerOption
                        {
                            KeyPath = $"{gameName}.{key}",
                            DisplayName = key,
                            Type = OptionType.Scalar,
                            SelectedValue = scalar.Value
                        });
                        break;
                }
            }
        }

        return new GameTemplate
        {
            GameName = gameName,
            Description = description,
            RequiredVersion = requiredVersion,
            RawYaml = raw,
            Options = options,
            DefaultPlayerName = defaultPlayerName
        };
    }

    private static RandomizerOption ClassifyMappingOption(string rawYaml, string gameName, string key, YamlMappingNode map)
    {
        var keys = map.Children.Keys.OfType<YamlScalarNode>().Select(k => k.Value ?? string.Empty).ToList();

        if (keys.Count == 0)
        {
            return new RandomizerOption
            {
                KeyPath = $"{gameName}.{key}",
                DisplayName = key,
                Type = OptionType.Dictionary,
                DefaultDictionary = new Dictionary<string, string>(),
                SelectedDictionary = new Dictionary<string, string>()
            };
        }

        var numericKeys = new List<int>();
        var specials = new Dictionary<string, int>();
        var weights = new Dictionary<string, int>();

        foreach (var k in keys)
        {
            var keyNode = new YamlScalarNode(k);
            var valNode = map.Children[keyNode];
            var weight = TryParseInt(valNode) ?? 0;

            if (int.TryParse(k, out var num))
            {
                numericKeys.Add(num);
                weights[k] = weight; // keep numeric in weights for export zeroing
            }
            else if (IsSpecialRandomKey(k))
            {
                specials[k] = weight;
                weights[k] = weight; // keep in weights, but GUI won’t show specials for numeric
            }
            else
            {
                weights[k] = weight;
            }
        }

        // Numeric (slider) if any numeric key exists, even with specials
        if (numericKeys.Count > 0)
        {
            var (minC, maxC) = ExtractNumericRangeFromComments(rawYaml, gameName, key);
            var min = minC ?? numericKeys.Min();
            var max = maxC ?? numericKeys.Max();

            // Default to highest-weight numeric; fallback to min
            var defaultNum = numericKeys
                .OrderByDescending(n => weights.TryGetValue(n.ToString(), out var w) ? w : 0)
                .FirstOrDefault();
            if (defaultNum < min) defaultNum = min;
            if (defaultNum > max) defaultNum = max;

            return new RandomizerOption
            {
                KeyPath = $"{gameName}.{key}",
                DisplayName = key,
                Type = OptionType.Numeric,
                Weights = weights,
                Specials = specials,
                Min = min,
                Max = max,
                DefaultNumeric = defaultNum,
                SelectedNumber = defaultNum
            };
        }

        // Bool (true/false)
        if (IsBooleanWeights(keys))
        {
            var defaultBool = weights.OrderByDescending(kv => kv.Value).FirstOrDefault().Key;
            return new RandomizerOption
            {
                KeyPath = $"{gameName}.{key}",
                DisplayName = key,
                Type = OptionType.Bool,
                Weights = weights,
                SelectedValue = string.IsNullOrEmpty(defaultBool) ? keys.FirstOrDefault() : defaultBool
            };
        }

        // Select list (enum)
        var defaultEnum = weights.OrderByDescending(kv => kv.Value).FirstOrDefault().Key;
        return new RandomizerOption
        {
            KeyPath = $"{gameName}.{key}",
            DisplayName = key,
            Type = OptionType.SelectList,
            Weights = weights,
            SelectedValue = string.IsNullOrEmpty(defaultEnum) ? keys.FirstOrDefault() : defaultEnum
        };
    }

    // Robust comment range extraction near the option header
    private static (int? min, int? max) ExtractNumericRangeFromComments(string rawYaml, string gameName, string optionKey)
    {
        // Find the game section and option block, then scan next N lines for min/max phrases
        var sectionRegex = new Regex($@"^{Regex.Escape(gameName)}\s*:\s*$", RegexOptions.Multiline);
        var optionRegex = new Regex($@"^\s*{Regex.Escape(optionKey)}\s*:\s*$", RegexOptions.Multiline);

        var sectionMatch = sectionRegex.Match(rawYaml);
        if (!sectionMatch.Success) return (null, null);

        var searchText = rawYaml.Substring(sectionMatch.Index);

        var optMatch = optionRegex.Match(searchText);
        if (!optMatch.Success) return (null, null);

        var windowText = searchText.Substring(optMatch.Index);
        var lines = windowText.Split('\n').Take(80).ToArray();
        var window = string.Join("\n", lines);

        // Recognize multiple phrasings
        var minPatterns = new[]
        {
            @"Minimum\s+value\s+is\s+(?<num>\d+)",
            @"Minimum\s*:\s*(?<num>\d+)",
            @"Min(?:imum)?\s*value?\s*:\s*(?<num>\d+)"
        };
        var maxPatterns = new[]
        {
            @"Maximum\s+value\s+is\s+(?<num>\d+)",
            @"Maximum\s*:\s*(?<num>\d+)",
            @"Max(?:imum)?\s*value?\s*:\s*(?<num>\d+)"
        };

        int? min = null, max = null;

        foreach (var pat in minPatterns)
        {
            var m = Regex.Match(window, pat, RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups["num"].Value, out var n)) { min = n; break; }
        }
        foreach (var pat in maxPatterns)
        {
            var m = Regex.Match(window, pat, RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups["num"].Value, out var n)) { max = n; break; }
        }

        return (min, max);
    }

    private static int? TryParseInt(YamlNode node)
    {
        if (node is YamlScalarNode s && int.TryParse(s.Value, out var i)) return i;
        return null;
    }

    private static bool IsBooleanWeights(IEnumerable<string> keys)
    {
        static string Normalize(string s)
        {
            var t = s.Trim();
            if ((t.StartsWith("'") && t.EndsWith("'")) || (t.StartsWith("\"") && t.EndsWith("\"")))
            {
                t = t.Substring(1, t.Length - 2);
            }
            return t.ToLowerInvariant();
        }
        var normalized = new HashSet<string>(keys.Select(Normalize));
        return normalized.Contains("true") && normalized.Contains("false");
    }

    private static bool IsSpecialRandomKey(string k)
    {
        var v = k.Trim().ToLowerInvariant();
        return v is "random" or "random-low" or "random_high" or "random-high" or "disabled" or "normal" or "extreme";
    }

    public static string? GetScalar(YamlMappingNode map, string key)
    {
        return map.Children.TryGetValue(new YamlScalarNode(key), out var node) && node is YamlScalarNode s
            ? s.Value
            : null;
    }

    private static bool IsEmptyNode(YamlNode node)
    {
        switch (node)
        {
            case YamlMappingNode map:
                return map.Children == null || map.Children.Count == 0;
            case YamlSequenceNode seq:
                return seq.Children == null || seq.Children.Count == 0;
            case YamlScalarNode scalar:
                var val = scalar.Value;
                return string.IsNullOrWhiteSpace(val);
            default:
                return false;
        }
    }
}
