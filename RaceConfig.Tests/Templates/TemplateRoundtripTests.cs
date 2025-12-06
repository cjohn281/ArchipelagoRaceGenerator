using System.IO;
using FluentAssertions;
using RaceConfig.Core.Templates;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace RaceConfig.Tests.Templates;

public class TemplateRoundtripTests
{
    [Fact]
    public void Parse_And_Generate_PlayerYaml_Witness()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, // e.g., ...\RaceConfig.Tests\bin\Debug\net8.0\
            "..", "..", "..", "..",   // -> solution root
            "RaceConfig.GUI", "Templates", "The Witness.yaml"));

        File.Exists(path).Should().BeTrue($"Template not found at {path}");

        var template = TemplateParser.ParseFromFile(path);

        template.GameName.Should().Be("The Witness");
        template.RequiredVersion.Should().Be("0.6.4");

        var victory = template.GetOption("The Witness.victory_condition");
        victory.Should().NotBeNull();
        victory!.Type.Should().Be(OptionType.EnumWeighted);
        victory.Weights!.ContainsKey("elevator").Should().BeTrue();

        // Select some values
        victory.SelectedValue = "challenge";

        var lasers = template.GetOption("The Witness.mountain_lasers");
        lasers.Should().NotBeNull();
        lasers!.Type.Should().Be(OptionType.NumericWeighted);
        lasers.SelectedNumber = 7;

        var yamlText = YamlGenerator.GeneratePlayerYaml(template, "Runner01");

        // Parse output and assert structurally
        var stream = new YamlStream();
        stream.Load(new StringReader(yamlText));
        var root = (YamlMappingNode)stream.Documents[0].RootNode;

        // Top-level name
        var nameNode = (YamlScalarNode)root.Children[new YamlScalarNode("name")];
        nameNode.Value.Should().Be("Runner01");

        // Game block
        var gameBlock = (YamlMappingNode)root.Children[new YamlScalarNode("The Witness")];

        // victory_condition exists and selected value has weight 50
        var victoryMap = (YamlMappingNode)gameBlock.Children[new YamlScalarNode("victory_condition")];
        ((YamlScalarNode)victoryMap.Children[new YamlScalarNode("challenge")]).Value.Should().Be("50");

        // mountain_lasers has numeric key 7 with weight 50
        var lasersMap = (YamlMappingNode)gameBlock.Children[new YamlScalarNode("mountain_lasers")];
        ((YamlScalarNode)lasersMap.Children[new YamlScalarNode("7")]).Value.Should().Be("50");
    }
}