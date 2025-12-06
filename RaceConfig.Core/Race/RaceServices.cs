using System.Collections.Generic;
using System.IO;
using System.Linq;
using RaceConfig.Core.Templates;
using static RaceConfig.Core.Race.Racer;

namespace RaceConfig.Core.Race;

public static class RaceServices
{
    public static void AssignRoundRobin(IList<Team> teams, IList<Racer> racers)
    {
        if (teams.Count == 0) return;
        var i = 0;
        foreach (var r in racers)
        {
            teams[i % teams.Count].Racers.Add(r);
            i++;
        }
    }

    public static void ExportPlayerYamls(string outputDir, IEnumerable<Team> teams, IDictionary<string, GameTemplate> templates)
    {
        Directory.CreateDirectory(outputDir);

        foreach (var team in teams)
        {
            var teamDir = Path.Combine(outputDir, Sanitize(team.Name));
            Directory.CreateDirectory(teamDir);

            foreach (var racer in team.Racers)
            {
                if (!templates.TryGetValue(racer.Game, out var template)) continue;

                // Option selections should already be set on template.Options per player/game
                var yaml = YamlGenerator.GeneratePlayerYaml(template, racer.Name);
                var file = Path.Combine(teamDir, $"{Sanitize(racer.Name)}.yaml");
                File.WriteAllText(file, yaml);
            }
        }
    }

    public static string Sanitize(string name)
        => string.Concat(name.Where(c => !Path.GetInvalidFileNameChars().Contains(c))).Trim();
}


