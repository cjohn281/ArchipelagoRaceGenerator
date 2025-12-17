using RaceConfig.Core.Templates;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaceConfig.Core.Race
{
    internal class Race
    {
        public int numTeams { get; init; }
        public List<string> Players { get; init; } = new();
        public List<GameTemplate> GameTemplates { get; init; } = new();
    }
}
