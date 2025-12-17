using System.Collections.Generic;

namespace RaceConfig.Core.Templates;

public sealed class RandomizerOption
{
    public required string KeyPath { get; init; }
    public required string DisplayName { get; init; }
    public required OptionType Type { get; init; }

    public Dictionary<string, int>? Weights { get; init; }
    public string? SelectedValue { get; set; }

    // SelectCustom properties
    public int? Min { get; init; }
    public int? Max { get; init; }
    public int? DefaultNumeric { get; init; }

    
}

