using System;
using System.IO;
using System.Linq;
using YamlDotNet.RepresentationModel;

namespace RaceConfig.Core.Templates;

public static class YamlGenerator
{
    public static string GeneratePlayerYaml(GameTemplate template, string playerName, Action<YamlMappingNode>? mutateTopLevel = null)
    {
        // Load raw YAML, then mutate according to selections
        var yaml = new YamlStream();
        using var reader = new StringReader(template.RawYaml);
        yaml.Load(reader);

        var root = (YamlMappingNode)yaml.Documents[0].RootNode;

        // Replace top-level name
        root.Children[new YamlScalarNode("name")] = new YamlScalarNode(playerName);

        // Let caller mutate top-level if needed (description etc.)
        mutateTopLevel?.Invoke(root);

        // Find game block map
        if (root.Children.TryGetValue(new YamlScalarNode(template.GameName), out var gameBlock) && gameBlock is YamlMappingNode gameMap)
        {
            foreach (var opt in template.Options)
            {
                // Only handle options inside the game block
                var optKey = opt.DisplayName;
                if (!gameMap.Children.ContainsKey(new YamlScalarNode(optKey))) continue;

                switch (opt.Type)
                {
                    case OptionType.Bool:
                    {
                        var currentMap = (YamlMappingNode)gameMap.Children[new YamlScalarNode(optKey)];
                        var sel = (opt.SelectedValue ?? "").Trim().Trim('\'', '"').ToLowerInvariant();

                        foreach (var entry in currentMap.Children.ToList())
                        {
                            var keyRaw = ((YamlScalarNode)entry.Key).Value ?? string.Empty;
                            var keyNorm = keyRaw.Trim().Trim('\'', '"').ToLowerInvariant();

                            var newWeight = keyNorm == "true"
                                ? (sel == "true" ? 50 : 0)
                                : keyNorm == "false"
                                    ? (sel == "true" ? 0 : 50)
                                    : 0;

                            currentMap.Children[entry.Key] = new YamlScalarNode(newWeight.ToString());
                        }
                        break;
                    }

                    case OptionType.SelectList:
                    case OptionType.SelectCustom:
                    {
                        var currentMap = (YamlMappingNode)gameMap.Children[new YamlScalarNode(optKey)];
                        var sel = (opt.SelectedValue ?? "").Trim().Trim('\'', '"');

                        foreach (var entry in currentMap.Children.ToList())
                        {
                            var keyRaw = ((YamlScalarNode)entry.Key).Value ?? string.Empty;
                            var keyNorm = keyRaw.Trim().Trim('\'', '"');

                            var newWeight = string.Equals(sel, keyNorm, StringComparison.Ordinal)
                                ? 50
                                : 0;

                            currentMap.Children[entry.Key] = new YamlScalarNode(newWeight.ToString());
                        }
                        break;
                    }

                    case OptionType.Numeric:
                    {
                        var numMap = (YamlMappingNode)gameMap.Children[new YamlScalarNode(optKey)];
                        var matchedSelected = false;

                        // zero all existing, set selected numeric to 50 if present
                        foreach (var entry in numMap.Children.ToList())
                        {
                            var keyText = ((YamlScalarNode)entry.Key).Value ?? string.Empty;

                            if (int.TryParse(keyText, out var existingNum))
                            {
                                var newWeight = (opt.SelectedNumber.HasValue && opt.SelectedNumber.Value == existingNum) ? 50 : 0;
                                if (newWeight == 50) matchedSelected = true;
                                numMap.Children[entry.Key] = new YamlScalarNode(newWeight.ToString());
                            }
                            else
                            {
                                // random/normal/etc. -> force to 0
                                numMap.Children[entry.Key] = new YamlScalarNode("0");
                            }
                        }

                        // add the selected numeric key if it doesn't exist
                        if (opt.SelectedNumber.HasValue && !matchedSelected)
                        {
                            var selectedKey = new YamlScalarNode(opt.SelectedNumber.Value.ToString());
                            numMap.Children[selectedKey] = new YamlScalarNode("50");
                        }

                        break;
                    }

                    case OptionType.List:
                        gameMap.Children[new YamlScalarNode(optKey)] =
                            new YamlSequenceNode((opt.SelectedList ?? opt.DefaultList ?? new()).Select(s => new YamlScalarNode(s)));
                        break;

                    case OptionType.Dictionary:
                    {
                        var dict = new YamlMappingNode();
                        foreach (var kv in (opt.SelectedDictionary ?? opt.DefaultDictionary ?? new()))
                        {
                            dict.Add(kv.Key, kv.Value);
                        }
                        gameMap.Children[new YamlScalarNode(optKey)] = dict;
                        break;
                    }

                    case OptionType.Scalar:
                        gameMap.Children[new YamlScalarNode(optKey)] =
                            new YamlScalarNode(opt.SelectedValue ?? "");
                        break;
                }
            }
        }

        using var writer = new StringWriter();
        yaml.Save(writer, assignAnchors: false);
        return writer.ToString();
    }

    // Change signature
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
                // keep numeric in weights so generator can zero non-selected numeric keys
                weights[k] = weight;
            }
            else if (IsSpecialRandomKey(k))
            {
                specials[k] = weight;
                // keep in weights so generator can zero them later
                weights[k] = weight;
            }
            else
            {
                weights[k] = weight;
            }
        }

        // Numeric: if any numeric key exists, treat mapping as numeric-only with slider
        if (numericKeys.Count > 0)
        {
            var (minC, maxC) = ExtractNumericRangeFromComments(rawYaml, gameName, key);
            var min = minC ?? numericKeys.Min();
            var max = maxC ?? numericKeys.Max();

            // Choose default: if a numeric key has highest weight, use it; otherwise fallback to min or first numeric
            int defaultNum = numericKeys
                .OrderByDescending(n => weights.TryGetValue(n.ToString(), out var w) ? w : 0)
                .FirstOrDefault();

            if (defaultNum == 0 && min > 0) defaultNum = min; // avoid defaulting to 0 when comments specify a higher min

            return new RandomizerOption
            {
                KeyPath = $"{gameName}.{key}",
                DisplayName = key,
                Type = OptionType.Numeric,
                Weights = weights,   // includes numeric and specials; GUI hides specials
                Specials = specials,
                Min = min,
                Max = max,
                DefaultNumeric = defaultNum,
                SelectedNumber = defaultNum
            };
        }

        // Bool
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

        // Select list
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

    // Add this private helper method to the YamlGenerator class to fix CS0103
    private static int? TryParseInt(YamlNode node)
    {
        if (node is YamlScalarNode scalar && int.TryParse(scalar.Value, out var result))
            return result;
        return null;
    }

    // Add this private helper method to the YamlGenerator class to fix CS0103
    private static bool IsSpecialRandomKey(string key)
    {
        // Example implementation: treat certain keys as "special" (customize as needed)
        // Common special keys in randomizer configs: "random", "normal", "weighted", etc.
        var specials = new[] { "random", "normal", "weighted", "default" };
        return specials.Contains(key, StringComparer.OrdinalIgnoreCase);
    }

    // Add this private helper method to the YamlGenerator class to fix CS0103
    private static (int? min, int? max) ExtractNumericRangeFromComments(string rawYaml, string gameName, string key)
    {
        // Simple implementation: scan for comments like "# min: <num>" and "# max: <num>"

        int? min = null, max = null;
        using (var reader = new StringReader(rawYaml))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                // Look for lines containing the gameName and key for context
                if (line.Contains(gameName) && line.Contains(key))
                {
                    // Check for min
                    var minMatch = System.Text.RegularExpressions.Regex.Match(line, @"#\s*min:\s*(\d+)");
                    if (minMatch.Success && int.TryParse(minMatch.Groups[1].Value, out var minVal))
                        min = minVal;

                    // Check for max
                    var maxMatch = System.Text.RegularExpressions.Regex.Match(line, @"#\s*max:\s*(\d+)");
                    if (maxMatch.Success && int.TryParse(maxMatch.Groups[1].Value, out var maxVal))
                        max = maxVal;
                }
            }
        }
        return (min, max);
    }

    // Add this private helper method to the YamlGenerator class to fix CS0103
    private static bool IsBooleanWeights(List<string> keys)
    {
        // Typical boolean keys in randomizer configs: "true"/"false", "yes"/"no", "on"/"off", "enabled"/"disabled"
        var boolPairs = new[]
        {
            new[] { "true", "false" },
            new[] { "yes", "no" },
            new[] { "on", "off" },
            new[] { "enabled", "disabled" }
        };

        foreach (var pair in boolPairs)
        {
            if (keys.Count == 2 &&
                keys.Any(k => string.Equals(k, pair[0], StringComparison.OrdinalIgnoreCase)) &&
                keys.Any(k => string.Equals(k, pair[1], StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }
        return false;
    }
}