using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace RaceConfig.GUI
{
    public sealed class AppConfig
    {
        public string? TemplatesPath { get; set; }
    }

    public static class AppConfigStore
    {
        private static readonly string AppFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArchipelagoRaceGenerator");
        private static readonly string ConfigFilePath = Path.Combine(AppFolder, "AppConfig.json");

        public static AppConfig Load()
        {
            try
            {
                Directory.CreateDirectory(AppFolder);
                if (File.Exists(ConfigFilePath))
                {
                    string json = File.ReadAllText(ConfigFilePath);
                    var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                    if (cfg is not null) return cfg;
                }
            }
            catch
            {
                // Ignore errors and return default config
            }
            return new AppConfig();
        }

        public static void Save(AppConfig config)
        {
            Directory.CreateDirectory(AppFolder);
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigFilePath, json);
        }
    }
}
