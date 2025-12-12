using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using RaceConfig.Core.Templates;
using RaceConfig.GUI.Config;
using WinForms = System.Windows.Forms;

namespace RaceConfig.GUI.ViewModels;

public sealed class TemplateItem
{
    public required string FullPath { get; init; }
    public required string Name { get; init; }
}

public class GameOptionsViewModel : INotifyPropertyChanged
{
    private readonly AppConfig _config = AppConfigStore.Load();

    private string? _templatesPath;
    public string? TemplatesPath
    {
        get => _templatesPath;
        set
        {
            if (_templatesPath == value) return;
            _templatesPath = value;
            OnPropertyChanged();
            _config.TemplatesPath = value;
            AppConfigStore.Save(_config);
            DiscoverTemplates();
        }
    }

    public ObservableCollection<TemplateItem> TemplateFiles { get; private set; } = new();
    private string? _selectedTemplateFile;
    public string? SelectedTemplateFile
    {
        get => _selectedTemplateFile;
        set
        {
            _selectedTemplateFile = value; OnPropertyChanged();
            if (!string.IsNullOrWhiteSpace(_selectedTemplateFile) && File.Exists(_selectedTemplateFile))
                LoadTemplate(_selectedTemplateFile);
        }
    }

    private GameTemplate? _template;
    public GameTemplate? Template
    {
        get => _template;
        set { _template = value; OnPropertyChanged(); OnPropertyChanged(nameof(Options)); }
    }

    public ObservableCollection<RandomizerOption> Options => new(Template?.Options ?? new());

    private string _playerName = "Runner01";
    public string PlayerName
    {
        get => _playerName;
        set { _playerName = value; OnPropertyChanged(); }
    }

    public IRelayCommand ExportCommand { get; }
    public IRelayCommand BrowseTemplatesPathCommand { get; }

    public GameOptionsViewModel()
    {
        ExportCommand = new RelayCommand(ExportYaml);
        BrowseTemplatesPathCommand = new RelayCommand(BrowseTemplatesPath);

        // Load last path or empty; DiscoverTemplates will no-op if path is invalid
        TemplatesPath = !string.IsNullOrWhiteSpace(_config.TemplatesPath) ? _config.TemplatesPath : "";
    }

    private string ResolveDefaultTemplatesPath()
    {
        // Return empty to force user selection, or keep probing typical locations if desired
        return string.IsNullOrWhiteSpace(_config.TemplatesPath) ? "" : _config.TemplatesPath;
    }

    private void BrowseTemplatesPath()
    {
        using var dlg = new WinForms.FolderBrowserDialog
        {
            Description = "Select folder containing YAML templates",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
            SelectedPath = string.IsNullOrWhiteSpace(TemplatesPath) ? AppContext.BaseDirectory : TemplatesPath
        };
        if (dlg.ShowDialog() == WinForms.DialogResult.OK && Directory.Exists(dlg.SelectedPath))
        {
            TemplatesPath = dlg.SelectedPath;
        }
    }

    private void DiscoverTemplates()
    {
        try
        {
            TemplateFiles.Clear();

            if (string.IsNullOrWhiteSpace(TemplatesPath) || !Directory.Exists(TemplatesPath))
            {
                OnPropertyChanged(nameof(TemplateFiles));
                return;
            }

            var files = Directory.GetFiles(TemplatesPath, "*.yaml", SearchOption.TopDirectoryOnly);
            foreach (var f in files)
                TemplateFiles.Add(new TemplateItem { FullPath = f, Name = Path.GetFileName(f) });

            OnPropertyChanged(nameof(TemplateFiles));
            SelectedTemplateFile = TemplateFiles.FirstOrDefault()?.FullPath;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to load templates:\n{ex.Message}", "Template Picker");
        }
    }

    private void LoadTemplate(string path) => Template = TemplateParser.ParseFromFile(path);

    private void ExportYaml()
    {
        if (Template is null)
        {
            System.Windows.MessageBox.Show("Template is not loaded.", "Export");
            return;
        }

        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Player YAML",
            Filter = "YAML Files (*.yaml)|*.yaml",
            FileName = $"{PlayerName}.yaml"
        };

        if (sfd.ShowDialog() == true)
        {
            var yaml = YamlGenerator.GeneratePlayerYaml(Template, PlayerName);
            File.WriteAllText(sfd.FileName, yaml);
            //System.Windows.MessageBox.Show($"Exported: {sfd.FileName}", "Export");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}