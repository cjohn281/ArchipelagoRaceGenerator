using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using RaceConfig.Core.Templates;

namespace RaceConfig.GUI.ViewModels;

public class GameOptionsViewModel : INotifyPropertyChanged
{
    public ObservableCollection<string> TemplateFiles { get; private set; } = new();
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

    public ObservableCollection<RandomizerOption> Options
        => new(Template?.Options ?? new());

    private string _playerName = "Runner01";
    public string PlayerName
    {
        get => _playerName;
        set { _playerName = value; OnPropertyChanged(); }
    }

    public IRelayCommand ExportCommand { get; }

    public GameOptionsViewModel()
    {
        ExportCommand = new RelayCommand(ExportYaml);
        DiscoverTemplates();
    }

    private void DiscoverTemplates()
    {
        var candidateDirs = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Templates"),
            Path.Combine(Directory.GetCurrentDirectory(), "Templates")
        };
        var dir = candidateDirs.FirstOrDefault(Directory.Exists);
        if (dir is null)
        {
            MessageBox.Show("Templates folder not found next to the app.", "Template Picker");
            return;
        }
        TemplateFiles = new ObservableCollection<string>(Directory.GetFiles(dir, "*.yaml"));
        OnPropertyChanged(nameof(TemplateFiles));
        SelectedTemplateFile = TemplateFiles.FirstOrDefault();
    }

    private void LoadTemplate(string path)
    {
        Template = TemplateParser.ParseFromFile(path);
    }

    private void ExportYaml()
    {
        if (Template is null)
        {
            MessageBox.Show("Template is not loaded.", "Export");
            return;
        }

        var sfd = new SaveFileDialog
        {
            Title = "Export Player YAML",
            Filter = "YAML Files (*.yaml)|*.yaml",
            FileName = $"{PlayerName}.yaml"
        };

        if (sfd.ShowDialog() == true)
        {
            var yaml = YamlGenerator.GeneratePlayerYaml(Template, PlayerName);
            File.WriteAllText(sfd.FileName, yaml);
            MessageBox.Show($"Exported: {sfd.FileName}", "Export");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}