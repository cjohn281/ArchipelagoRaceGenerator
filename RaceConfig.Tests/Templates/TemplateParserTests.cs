using System.Collections.Generic;
using System.IO;
using System.Linq;
using RaceConfig.Core.Templates;
using Xunit;
using YamlDotNet.RepresentationModel;

namespace RaceConfig.Tests.Templates;

public class TemplateParserTests
{
    [Fact]
    public void ParseFromFile_ParsesBasicYamlFile()
    {
        // Arrange
        var yaml = @"
            game: TestGame
            description: A test game
            name: Player1
            requires:
              version: 1.0
            TestGame:
              Option1:
                true: 10
                false: 5
              Option2:
                1: 100
                2: 200
                random: 50
              Option3:
                - A
                - B
              Option4: Value4
        ";

        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, yaml);

        // Act
        var result = TemplateParser.ParseFromFile(tempFile);

        // Assert
        Assert.Equal("TestGame", result.GameName);
        Assert.Equal("A test game", result.Description);
        Assert.Equal("1.0", result.RequiredVersion);
        Assert.Equal("Player1", result.DefaultPlayerName);
        Assert.Equal(yaml, result.RawYaml);
        Assert.Equal(4, result.Options.Count);

        var option1 = result.Options.First(o => o.DisplayName == "Option1");
        Assert.Equal(OptionType.BooleanWeighted, option1.Type);
        Assert.Equal(10, option1.Weights["true"]);
        Assert.Equal(5, option1.Weights["false"]);

        var option2 = result.Options.First(o => o.DisplayName == "Option2");
        Assert.Equal(OptionType.NumericWeighted, option2.Type);
        Assert.Equal(100, option2.Weights["1"]);
        Assert.Equal(200, option2.Weights["2"]);
        Assert.Equal(50, option2.Specials["random"]);
        Assert.Equal(1, option2.Min);
        Assert.Equal(2, option2.Max);

        var option3 = result.Options.First(o => o.DisplayName == "Option3");
        Assert.Equal(OptionType.List, option3.Type);
        Assert.Contains("A", option3.DefaultList);
        Assert.Contains("B", option3.DefaultList);

        var option4 = result.Options.First(o => o.DisplayName == "Option4");
        Assert.Equal(OptionType.PassThrough, option4.Type);
        Assert.Equal("Value4", option4.SelectedValue);

        File.Delete(tempFile);
    }

    [Fact]
    public void ClassifyMappingOption_NumericWeighted()
    {
        var yaml = @"
            1: 10
            2: 20
            random: 5
        ";

        var map = ParseMappingNode(yaml);
        var option = InvokeClassifyMappingOption("Game", "Numeric", map);

        Assert.Equal(OptionType.NumericWeighted, option.Type);
        Assert.Equal(10, option.Weights["1"]);
        Assert.Equal(20, option.Weights["2"]);
        Assert.Equal(5, option.Specials["random"]);
        Assert.Equal(1, option.Min);
        Assert.Equal(2, option.Max);
    }

    [Fact]
    public void ClassifyMappingOption_BooleanWeighted()
    {
        var yaml = @"
            true: 7
            false: 3
        ";

        var map = ParseMappingNode(yaml);
        var option = InvokeClassifyMappingOption("Game", "Bool", map);

        Assert.Equal(OptionType.BooleanWeighted, option.Type);
        Assert.Equal(7, option.Weights["true"]);
        Assert.Equal(3, option.Weights["false"]);
        Assert.Equal("true", option.SelectedValue);
    }

    [Fact]
    public void ClassifyMappingOption_EnumWeighted()
    {
        var yaml = @"
            Easy: 1
            Hard: 2
        ";
        var map = ParseMappingNode(yaml);
        var option = InvokeClassifyMappingOption("Game", "Enum", map);

        Assert.Equal(OptionType.EnumWeighted, option.Type);
        Assert.Equal(1, option.Weights["Easy"]);
        Assert.Equal(2, option.Weights["Hard"]);
        Assert.Equal("Hard", option.SelectedValue);
    }

    [Theory]
    [InlineData("42", 42)]
    [InlineData("notanumber", null)]
    [InlineData(null, null)]
    public void TryParseInt_ReturnsExpected(string value, int? expected)
    {
        var node = value == null ? null : new YamlScalarNode(value);
        var result = typeof(TemplateParser)
            .GetMethod("TryParseInt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Invoke(null, new object[] { node });
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(new[] { "true", "false" }, true)]
    [InlineData(new[] { "True", "False" }, true)]
    [InlineData(new[] { "'true'", "'false'" }, true)]
    [InlineData(new[] { "yes", "no" }, false)]
    [InlineData(new[] { "true", "maybe" }, false)]
    public void IsBooleanWeights_ReturnsExpected(string[] keys, bool expected)
    {
        var result = typeof(TemplateParser)
            .GetMethod("IsBooleanWeights", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Invoke(null, new object[] { keys });
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("random", true)]
    [InlineData("random-low", true)]
    [InlineData("random_high", true)]
    [InlineData("disabled", true)]
    [InlineData("normal", true)]
    [InlineData("extreme", true)]
    [InlineData("other", false)]
    public void IsSpecialRandomKey_ReturnsExpected(string key, bool expected)
    {
        var result = typeof(TemplateParser)
            .GetMethod("IsSpecialRandomKey", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Invoke(null, new object[] { key });
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetScalar_ReturnsValueIfExists()
    {
        var yaml = @"
            foo: bar
        ";
        var map = ParseMappingNode(yaml);
        var value = TemplateParser.GetScalar(map, "foo");
        Assert.Equal("bar", value);
    }

    [Fact]
    public void GetScalar_ReturnsNullIfNotExists()
    {
        var yaml = @"
            foo: bar
        ";
        var map = ParseMappingNode(yaml);
        var value = TemplateParser.GetScalar(map, "baz");
        Assert.Null(value);
    }

    // Helper methods for private method testing
    private static YamlMappingNode ParseMappingNode(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader($"root:\n{yaml.Replace("\n", "\n  ")}"));
        return (YamlMappingNode)((YamlMappingNode)stream.Documents[0].RootNode).Children[new YamlScalarNode("root")];
    }

    private static RandomizerOption InvokeClassifyMappingOption(string gameName, string key, YamlMappingNode map)
    {
        var method = typeof(TemplateParser).GetMethod("ClassifyMappingOption", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        return (RandomizerOption)method.Invoke(null, new object[] { gameName, key, map });
    }
}