using System;
using System.IO;
using System.Text.Json;

namespace RaceConfig.GUI.Config;

public sealed class AppConfig
{
    public string? TemplatesPath { get; set; }
}

public static class AppConfigStore
{
    private static readonly string AppFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ArchipelagoRaceGenerator");
    private static readonly string ConfigFilePath = Path.Combine(AppFolder, "AppConfig.json");

    public static AppConfig Load()
    {
        try
        {
            Directory.CreateDirectory(AppFolder);
            if (File.Exists(ConfigFilePath))
            {
                var json = File.ReadAllText(ConfigFilePath);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json);
                if (cfg is not null) return cfg;
            }
        }
        catch
        {
            // ignore and return defaults
        }
        return new AppConfig();
    }

    public static void Save(AppConfig config)
    {
        Directory.CreateDirectory(AppFolder);
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigFilePath, json);
    }
}