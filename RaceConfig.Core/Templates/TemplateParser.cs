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
        string raw = File.ReadAllText(filePath);

        YamlStream yaml = new YamlStream();
        using StringReader reader = new StringReader(raw);
        yaml.Load(reader);

        YamlMappingNode root = (YamlMappingNode)yaml.Documents[0].RootNode;

        string gameName = GetScalar(root, "game") ?? throw new InvalidOperationException("Template is missing 'game' field.");
        string? description = GetScalar(root, "description");

        string? requiredVersion = null;
        if (root.Children.TryGetValue(new YamlScalarNode("requires"), out var reqNode) && reqNode is YamlMappingNode reqMap)
        {
            requiredVersion = GetScalar(reqMap, "version");
        }

        List<RandomizerOption> options = new List<RandomizerOption>();
        if (root.Children.TryGetValue(new YamlScalarNode(gameName), out var optionsBlock) && optionsBlock is YamlMappingNode optionsMap)
        {
            foreach (var kvp in optionsMap.Children)
            {
                string key = ((YamlScalarNode)kvp.Key).Value ?? string.Empty;
                YamlNode node = kvp.Value;

                //if (IsEmptyNode(node))
                //    continue;

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
                            Type = OptionType.List
                        });
                        break;
                    //case YamlScalarNode scalar:
                    //    options.Add(new RandomizerOption());
                    //    break;
                    default:
                        throw new InvalidOperationException($"Unable to parse {gameName}.{key}");
                }
            }
        }

        return new GameTemplate{
            GameName = gameName,
            Description = description,
            RequiredVerison = requiredVersion,
            RawYaml = raw,
            Options = options
        };
    }

    private static string? GetScalar(YamlMappingNode map, string key)
    {
        return map.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? node) && node is YamlScalarNode s
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
                string? val = scalar.Value;
                return string.IsNullOrWhiteSpace(val);
            default:
                return false;
        }
    }

    private static RandomizerOption ClassifyMappingOption(string rawYaml, string gameName, string key, YamlMappingNode map)
    {
        List<string> keys = map.Children.Keys.OfType<YamlScalarNode>().Select(k => k.Value ?? string.Empty).ToList();

        if (keys.Count == 0)
        {
            return new RandomizerOption
            {
                KeyPath = $"{gameName}.{key}",
                DisplayName = key,
                Type = OptionType.Dictionary
            };
        }

        List<int> numericKeys = new List<int>();
        List<string> stringKeys = new List<string>();
        Dictionary<string, int> optionItems = new Dictionary<string, int>();

        foreach (string k in keys)
        {
            YamlScalarNode keyNode = new YamlScalarNode(k);
            var valNode = map.Children[keyNode];
            int weight = TryParseInt(valNode) ?? 0;

            if (int.TryParse(k, out int num))
                numericKeys.Add(num); // keys that are numeric
            else
                stringKeys.Add(k); // keys that are strings

            optionItems[k] = weight;
        }

        string defaultValue = optionItems.OrderByDescending(kv => kv.Value).FirstOrDefault().Key;

        if (numericKeys.Count == 0)
        {
            int countWeightsGreaterThanZero = optionItems.Values.Count(v => v > 0);
            
            // Distribution
            if (countWeightsGreaterThanZero > 1)
            {
                return new RandomizerOption
                {
                    KeyPath = $"{gameName}.{key}",
                    DisplayName = key,
                    Type = OptionType.Distribution,
                    Weights = optionItems
                };
            }
            // Bool
            else if (IsBoolean(stringKeys) && optionItems.Count == 2)
                return new RandomizerOption
                {
                    KeyPath = $"{gameName}.{key}",
                    DisplayName = key,
                    Type = OptionType.Bool,
                    Weights = optionItems,
                    SelectedValue = string.IsNullOrEmpty(defaultValue) ? keys.FirstOrDefault() : defaultValue
                };
            
            // SelectList
            return new RandomizerOption
            {
                KeyPath = $"{gameName}.{key}",
                DisplayName = key,
                Type = OptionType.SelectList,
                Weights = optionItems,
                SelectedValue = string.IsNullOrEmpty(defaultValue) ? keys.FirstOrDefault() : defaultValue
            };
        }
        
        // SelectCustom
        if (numericKeys.Count == 1)
        {
            var (rangeMin, rangeMax) = ExtractNumericRangeFromComments(rawYaml, gameName, key);
            int min = rangeMin ?? 0;
            int max = rangeMax ?? 100;

            int defaultNum = numericKeys[0];
            
            int valueOfNumericKey = optionItems[numericKeys[0].ToString()];
            optionItems.Remove(numericKeys[0].ToString());
            optionItems.Add("custom", valueOfNumericKey);

            if(int.TryParse(defaultValue, out int dv))
            {
                defaultValue = "custom";
            }

            return new RandomizerOption
            {
                KeyPath = $"{gameName}.{key}",
                DisplayName = key,
                Type = OptionType.SelectCustom,
                Weights = optionItems,
                Min = min,
                Max = max,
                DefaultNumeric = defaultNum,
                SelectedValue = string.IsNullOrEmpty(defaultValue) ? keys.FirstOrDefault() : defaultValue
            };
        }

        if (numericKeys.Count > 1)
        {
            return new RandomizerOption
            {
                KeyPath = $"{gameName}.{key}",
                DisplayName = key,
                Type = OptionType.SelectList,
                Weights = optionItems,
                SelectedValue = string.IsNullOrEmpty(defaultValue) ? keys.FirstOrDefault() : defaultValue
            };
        }


        throw new InvalidOperationException($"Unable to parse {gameName}.{key}");
    }

    private static int? TryParseInt(YamlNode node)
    {
        if (node is YamlScalarNode s && int.TryParse(s.Value, out int i)) return i;
        return null;
    }

    private static bool IsBoolean(List<string> keys)
    {
        static string Normalize(string s)
        {
            string t = s.Trim();
            if ((t.StartsWith("'") && t.EndsWith("'")) || (t.StartsWith("\"") && t.EndsWith("\"")))
            {
                t = t.Substring(1, t.Length - 2);
            }
            return t.ToLowerInvariant();
        }
        HashSet<string> normalized = new HashSet<string>(keys.Select(Normalize));
        return normalized.Contains("true") && normalized.Contains("false");
    }

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
}
