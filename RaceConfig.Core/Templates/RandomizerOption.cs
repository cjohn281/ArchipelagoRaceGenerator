using System.Collections.Generic;

namespace RaceConfig.Core.Templates;
public sealed class RandomizerOption
{
    public required string KeyPath { get; init; }
    public required string DisplayName { get; init; }
    public required OptionType Type { get; init; }

    // Common metadata
    public string? Hint { get; init; }

    // Enum/Boolean weighted options
    public Dictionary<string, int>? Weights { get; init; }

    // Numeric weighted options
    public int? Min { get; init; }
    public int? Max { get; init; }
    public Dictionary<string, int>? Specials { get; init; }
    public int? DefaultNumeric { get; init; }

    // List/Dictionary
    public List<string>? DefaultList { get; init; }
    public Dictionary<string, string>? DefaultDictionary { get; init; }

    // User selection (UI binds)
    public string? SelectedValue { get; set; }      // for Enum/Boolean
    public int? SelectedNumber { get; set; }        // for Numeric
    public bool UseRandom { get; set; }             // for Numeric to choose special random keys
    public List<string>? SelectedList { get; set; }
    public Dictionary<string, string>? SelectedDictionary { get; set; }
}

