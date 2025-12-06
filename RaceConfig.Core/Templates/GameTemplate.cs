using System.Collections.Generic;

namespace RaceConfig.Core.Templates;

public sealed class GameTemplate
{
    public required string GameName { get; init; }
    public string? Description { get; init; }
    public string? RequiredVersion { get; init; }

    // Raw YAML to preserve comments/structure for regeneration
    public required string RawYaml { get; init; }

    // Options discovered under the "GameName:" block
    public required List<RandomizerOption> Options { get; init; }

    // Top-level fields (name/description can be replaced per player)
    public string? DefaultPlayerName { get; init; }

    //Convenience lookup
    public RandomizerOption? GetOption(string keyPath) => Options.Find(o => o.KeyPath == keyPath);
}
