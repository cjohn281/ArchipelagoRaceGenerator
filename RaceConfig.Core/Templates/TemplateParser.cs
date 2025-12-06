using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

                switch (node)
                {
                    case YamlMappingNode map:
                        options.Add(ClassifyMappingOption(gameName, key, map));
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
                            Type = OptionType.PassThrough,
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

    private static RandomizerOption ClassifyMappingOption(string gameName, string key, YamlMappingNode map)
    {
        var keys = map.Children.Keys.OfType<YamlScalarNode>().Select(k => k.Value ?? string.Empty).ToList();

        // Empty map: treat as dictionary with no entries
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

            // Attempt to parse weight as int; default to 0 if not an int
            var weight = TryParseInt(valNode) ?? 0;

            if (int.TryParse(k, out var num))
            {
                numericKeys.Add(num);
                weights[k] = weight;
            }
            else if (IsSpecialRandomKey(k))
            {
                specials[k] = weight;
            }
            else
            {
                weights[k] = weight;
            }
        }

        // Numeric option: at least one numeric key present
        if (numericKeys.Count > 0)
        {
            var defaultNum = numericKeys.Count > 0 ? numericKeys[0] : (int?)null;

            return new RandomizerOption
            {
                KeyPath = $"{gameName}.{key}",
                DisplayName = key,
                Type = OptionType.NumericWeighted,
                Weights = weights,
                Specials = specials,
                Min = numericKeys.Min(),
                Max = numericKeys.Max(),
                DefaultNumeric = defaultNum,
                SelectedNumber = defaultNum
            };
        }

        // Boolean weighted if keys contain both true/false (as strings)
        if (IsBooleanWeights(keys))
        {
            var defaultBool = weights.OrderByDescending(kv => kv.Value).FirstOrDefault().Key;
            return new RandomizerOption
            {
                KeyPath = $"{gameName}.{key}",
                DisplayName = key,
                Type = OptionType.BooleanWeighted,
                Weights = weights,
                SelectedValue = string.IsNullOrEmpty(defaultBool) ? null : defaultBool
            };
        }

        // Fallback: enum weighted
        var defaultEnum = weights.OrderByDescending(kv => kv.Value).FirstOrDefault().Key;
        return new RandomizerOption
        {
            KeyPath = $"{gameName}.{key}",
            DisplayName = key,
            Type = OptionType.EnumWeighted,
            Weights = weights,
            SelectedValue = string.IsNullOrEmpty(defaultEnum) ? keys.FirstOrDefault() : defaultEnum
        };
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
}
