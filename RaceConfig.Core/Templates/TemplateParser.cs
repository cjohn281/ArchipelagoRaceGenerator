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

        // top-level scalars
        var gameName = GetScalar(root, "game") ?? throw new InvalidOperationException("Missing 'game' in template.");
        var description = GetScalar(root, "description");
        var defaultPlayerName = GetScalar(root, "name");

        // requires.version
        string? requiredVersion = null;
        if (root.Children.TryGetValue(new YamlScalarNode("requires"), out var reqNode) && reqNode is YamlMappingNode reqMap)
        {
            requiredVersion = GetScalar(reqMap, "version");
        }

        // game block (e.g., "The Witness:")
        var options = new List<RandomizerOption>();
        if (root.Children.TryGetValue(new YamlScalarNode(gameName), out var gameBlock) && gameBlock is YamlMappingNode gameMap)
        {
            foreach (var kvp in gameMap.Children)
            {
                var key = ((YamlScalarNode)kvp.Key).Value ?? string.Empty;
                var node = kvp.Value;

                // Skip node if it's empty
                if (IsEmptyNode(node))
                    continue;


                switch (node)
                {
                    case YamlMappingNode map:
                        if (key == "progression_balancing")
                            Console.WriteLine("");
                        options.Add(ClassifyMappingOption(gameName, key, map, raw));
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

    private static RandomizerOption ClassifyMappingOption(string gameName, string key, YamlMappingNode map, string rawYaml)
    {
        var keys = map.Children.Keys.OfType<YamlScalarNode>().Select(k => k.Value ?? string.Empty).ToList();

        if (keys.Count == 0)
        {
            return new RandomizerOption
            {
                KeyPath = $"{gameName}.{key}",
                DisplayName = key,
                Type = OptionType.Dictionary,
                DefaultDictionary = new(),
                SelectedDictionary = new()
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
                // We do not add numeric entries into Weights for GUI choices; slider handles numeric selection.
                weights[k] = weight; // keep for generator to zero-out later
            }
            else if (IsSpecialRandomKey(k))
            {
                specials[k] = weight;
            }
            else
            {
                // non-numeric, non-special enum keys (rare) – treat as enum if there are no numeric keys
                weights[k] = weight;
            }
        }

        if (numericKeys.Count > 0)
        {
            // Prefer min/max from comments; fallback to observed numeric keys
            var (commentMin, commentMax) = ExtractNumericRangeFromComments(rawYaml, gameName, key);
            var min = commentMin ?? (numericKeys.Count > 0 ? numericKeys.Min() : (int?)null);
            var max = commentMax ?? (numericKeys.Count > 0 ? numericKeys.Max() : (int?)null);
            var defaultNum = numericKeys.FirstOrDefault();

            return new RandomizerOption
            {
                KeyPath = $"{gameName}.{key}",
                DisplayName = key,
                Type = OptionType.Numeric,
                Weights = weights,   // includes numeric keys too (for export zeroing)
                Specials = specials, // random/random-low/high/etc.
                Min = min,
                Max = max,
                DefaultNumeric = defaultNum,
                SelectedNumber = defaultNum
            };
        }

        if (IsBooleanWeights(keys))
        {
            var defaultBool = weights.OrderByDescending(kv => kv.Value).FirstOrDefault().Key;
            return new RandomizerOption
            {
                KeyPath = $"{gameName}.{key}",
                DisplayName = key,
                Type = OptionType.Bool,
                Weights = weights,
                SelectedValue = string.IsNullOrEmpty(defaultBool) ? null : defaultBool
            };
        }

        var defaultEnum = weights.OrderByDescending(kv => kv.Value).FirstOrDefault().Key;
        return new RandomizerOption
        {
            KeyPath = $"{gameName}.{key}",
            DisplayName = key,
            Type = OptionType.SelectList,
            Weights = specials.Count > 0 ? specials : weights,
            SelectedValue = string.IsNullOrEmpty(defaultEnum) ? keys.FirstOrDefault() : defaultEnum
        };
    }

    // Heuristic: search raw text near the option for min/max lines
    private static (int? min, int? max) ExtractNumericRangeFromComments(string rawYaml, string gameName, string optionKey)
    {
        // Find the section header line: "<gameName>:" then later the "<optionKey>:" block
        // Use a forgiving regex to extract nearby "Minimum value is X" and "Maximum value is Y"
        // Also handle variants: "Minimum value: X", "Minimum: X", "Min: X", "Max: X", etc.
        var rangePatterns = new[]
        {
            @"Minimum\s+value\s+is\s+(?<num>\d+)",
            @"Maximum\s+value\s+is\s+(?<num>\d+)",
            @"Minimum\s*:\s*(?<num>\d+)",
            @"Maximum\s*:\s*(?<num>\d+)",
            @"Min(?:imum)?\s*value?\s*:\s*(?<num>\d+)",
            @"Max(?:imum)?\s*value?\s*:\s*(?<num>\d+)"
        };

        var sectionRegex = new Regex($@"^{Regex.Escape(gameName)}\s*:\s*$", RegexOptions.Multiline);
        var optionRegex = new Regex($@"^\s*{Regex.Escape(optionKey)}\s*:\s*$", RegexOptions.Multiline);

        var sectionMatch = sectionRegex.Match(rawYaml);
        if (!sectionMatch.Success) return (null, null);

        var startIndex = sectionMatch.Index;
        var endIndex = rawYaml.IndexOf('\n', startIndex);
        // Narrow search to after the game section start
        var searchText = rawYaml.Substring(startIndex);

        var optMatch = optionRegex.Match(searchText);
        if (!optMatch.Success) return (null, null);

        // Search within some window following the option header (e.g., next 40 lines)
        var windowStart = optMatch.Index;
        var windowText = searchText.Substring(windowStart);
        var lines = windowText.Split('\n').Take(60).ToArray();
        var window = string.Join("\n", lines);

        int? min = null, max = null;
        foreach (var pat in rangePatterns)
        {
            var rx = new Regex(pat, RegexOptions.IgnoreCase);
            foreach (Match m in rx.Matches(window))
            {
                if (m.Success && int.TryParse(m.Groups["num"].Value, out var n))
                {
                    if (pat.StartsWith("Minimum", StringComparison.OrdinalIgnoreCase) || pat.StartsWith("Min", StringComparison.OrdinalIgnoreCase))
                        min = min ?? n;
                    else
                        max = max ?? n;
                }
            }
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
        // normalize by trimming whitespace and surrounding quotes, then lowercasing
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
        // Accept both hyphen and underscore variants for compatibility with tests/templates
        return v is "random" or "random-low" or "random_high" or "random-high" or "disabled" or "normal" or "extreme";
    }

    // Replace the existing GetScalar with this public variant
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
                // map with no entries
                return map.Children == null || map.Children.Count == 0;

            case YamlSequenceNode seq:
                // sequence with no items
                return seq.Children == null || seq.Children.Count == 0;

            case YamlScalarNode scalar:
                // null or whitespace scalar
                var val = scalar.Value;
                return string.IsNullOrWhiteSpace(val);

            default:
                return false;
        }
    }
}
