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
        var gameName = GetScalar(root, "game");
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

                // Skip comment-only sections; only process mappings/scalars/seq
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
                        // pass-through scalar for uncommon simple options
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
        // Determine if mapping keys are numeric -> NumericWeighted,
        // boolean-like -> BooleanWeighted,
        // otherwise -> EnumWeighted.
        var keys = map.Children.Keys.OfType<YamlScalarNode>().Select(k => k.Value ?? string.Empty).ToList();

        var numericKeys = new List<int>();
        var specials = new Dictionary<string, int>();
        var weights = new Dictionary<string, int>();

        foreach (var k in keys)
        {
            var valNode = map.Children[new YamlScalarNode(k)];
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

        if (numericKeys.Count > 0)
        {
            return new RandomizerOption
            {
                KeyPath = $"{gameName}.{key}",
                DisplayName = key,
                Type = OptionType.NumericWeighted,
                Weights = weights,
                Specials = specials,
                Min = numericKeys.Min(),
                Max = numericKeys.Max(),
                DefaultNumeric = numericKeys.FirstOrDefault(),
                SelectedNumber = numericKeys.FirstOrDefault()
            };
        }

        if (IsBooleanWeights(keys))
        {
            return new RandomizerOption
            {
                KeyPath = $"{gameName}.{key}",
                DisplayName = key,
                Type = OptionType.BooleanWeighted,
                Weights = weights,
                SelectedValue = weights.OrderByDescending(kv => kv.Value).First().Key // default to highest weight
            };
        }

        return new RandomizerOption
        {
            KeyPath = $"{gameName}.{key}",
            DisplayName = key,
            Type = OptionType.EnumWeighted,
            Weights = weights,
            SelectedValue = weights.OrderByDescending(kv => kv.Value).First().Key
        };
    }

    private static int? TryParseInt(YamlNode node)
    {
        if (node is YamlScalarNode s && int.TryParse(s.Value, out var i)) return i;
        return null;
    }

    private static bool IsBooleanWeights(IEnumerable<string> keys)
    {
        var set = new HashSet<string>(keys.Select(k => k.Trim().ToLowerInvariant()));
        return set.SetEquals(new[] { "true", "false" }) || set.SetEquals(new[] { "'true'", "'false'" }) ||
               set.Contains("true") && set.Contains("false");
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
}
