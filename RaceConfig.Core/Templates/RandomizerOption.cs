using System.Collections.Generic;

namespace RaceConfig.Core.Templates;

public sealed class RandomizerOption
{
    public required string KeyPath { get; init; }
    public required string DisplayName { get; init; }
    public required OptionType Type { get; init; }

    public string? Hint { get; init; }

    public Dictionary<string, int>? Weights { get; init; }  // enum/boolean/non-numeric keys
    public Dictionary<string, int>? Specials { get; init; } // random/random-low/high/etc.

    public int? Min { get; init; }             // numeric range lower bound
    public int? Max { get; init; }             // numeric range upper bound
    public int? DefaultNumeric { get; init; }  // first numeric key observed

    public string? SelectedValue { get; set; }
    public int? SelectedNumber { get; set; }
    public bool UseRandom { get; set; }

    public List<string>? DefaultList { get; init; }
    public List<string>? SelectedList { get; set; }

    public Dictionary<string, string>? DefaultDictionary { get; init; }
    public Dictionary<string, string>? SelectedDictionary { get; set; }
}

