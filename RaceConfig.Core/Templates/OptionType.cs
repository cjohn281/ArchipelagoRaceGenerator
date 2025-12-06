namespace RaceConfig.Core.Templates;
public enum OptionType
{
    EnumWeighted,       // map<string, int> weights
    BooleanWeighted,    // 'true'/'false' keys with weights
    NumericWeighted,    // numeric keys + random/random-low/high specials
    List,
    Dictionary,
    PassThrough         // simple scalar values
}