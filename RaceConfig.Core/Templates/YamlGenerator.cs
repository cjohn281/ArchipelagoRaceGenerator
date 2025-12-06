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
                    case OptionType.EnumWeighted:
                    case OptionType.BooleanWeighted:
                        // Set selected value’s weight high, others low/zero
                        var currentMap = (YamlMappingNode)gameMap.Children[new YamlScalarNode(optKey)];
                        foreach (var entry in currentMap.Children.ToList())
                        {
                            var key = ((YamlScalarNode)entry.Key).Value ?? string.Empty;
                            var newWeight = (opt.SelectedValue == key) ? 50 : 0; // simple rule; can be customized
                            currentMap.Children[entry.Key] = new YamlScalarNode(newWeight.ToString());
                        }
                        break;

                    case OptionType.NumericWeighted:
                        var numMap = (YamlMappingNode)gameMap.Children[new YamlScalarNode(optKey)];
                        foreach (var entry in numMap.Children.ToList())
                        {
                            var key = ((YamlScalarNode)entry.Key).Value ?? string.Empty;
                            var newWeight = 0;

                            if (int.TryParse(key, out var n))
                            {
                                newWeight = (opt.SelectedNumber.HasValue && opt.SelectedNumber.Value == n) ? 50 : 0;
                            }
                            else if (opt.UseRandom && opt.Specials?.ContainsKey(key) == true)
                            {
                                // prefer "random" keys when UseRandom is set
                                newWeight = 50;
                            }

                            numMap.Children[entry.Key] = new YamlScalarNode(newWeight.ToString());
                        }
                        break;

                    case OptionType.List:
                        gameMap.Children[new YamlScalarNode(optKey)] =
                            new YamlSequenceNode((opt.SelectedList ?? opt.DefaultList ?? new()).Select(s => new YamlScalarNode(s)));
                        break;

                    case OptionType.Dictionary:
                        var dict = new YamlMappingNode();
                        foreach (var kv in (opt.SelectedDictionary ?? opt.DefaultDictionary ?? new()))
                        {
                            dict.Add(kv.Key, kv.Value);
                        }
                        gameMap.Children[new YamlScalarNode(optKey)] = dict;
                        break;

                    case OptionType.PassThrough:
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
}