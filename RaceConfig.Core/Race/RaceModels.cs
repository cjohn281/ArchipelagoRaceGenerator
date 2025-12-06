using RaceConfig.Core.Templates;
using System.Collections.Generic;

namespace RaceConfig.Core.Race;

public sealed class Racer
{
    public required string Name { get; init; }
    public required string Game { get; init; } // matches template GameName
    public Dictionary<string, string>? Tags { get; init; } // optional metadata

    public sealed class Team
    {
        public required string Name { get; init; }
        public List<Racer> Racers { get; } = new();
    }

    public sealed class RacePlan
    {
        public List<Team> Teams { get; } = new();
        public List<Racer> UnassignedRacers { get; } = new();
        public Dictionary<string, GameTemplate> TemplatesByGame { get; } = new();
    }

}
