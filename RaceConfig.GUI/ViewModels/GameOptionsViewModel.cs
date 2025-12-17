using RaceConfig.Core.Templates;
using RaceConfig.GUI.MVVM;
using System.Collections.ObjectModel;
using System.IO;
using WinForms = System.Windows.Forms;

namespace RaceConfig.GUI.ViewModels
{

    public sealed class TemplateItem
    {
        public required string FullPath { get; init; }
        public required string DisplayName { get; init; }
    }

    internal class GameOptionsViewModel : ViewModelBase
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
                _selectedTemplateFile = value;
                OnPropertyChanged();
                if (!string.IsNullOrWhiteSpace(_selectedTemplateFile) && File.Exists(_selectedTemplateFile))
                    LoadTemplate(_selectedTemplateFile);
            }
        }

        private GameTemplate? _template;
        public GameTemplate? Template
        {
            get => _template;
            set
            {
                _template = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Options));
            }
        }

        public ObservableCollection<RandomizerOption> Options => new(Template?.Options ?? new());

        private string _playerName = "Runner01";
        public string PlayerName
        {
            get => _playerName;
            set
            {
                _playerName = value;
                OnPropertyChanged();
            }
        }

        public RelayCommand ExportCommand => new RelayCommand(execute => ExportYaml(), canExecute => SelectedTemplateFile is not null);
        public RelayCommand BrowseTemplatesPathCommand => new RelayCommand(execute => BrowseTemplatesPath());

        public GameOptionsViewModel()
        {
            // Load last path or empty; DiscoverTemplates will no-op if path is invalid
            TemplatesPath = !string.IsNullOrWhiteSpace(_config.TemplatesPath) ? _config.TemplatesPath : string.Empty;
        }

        private string ResolveDefaultTemplatesPath()
        {
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
                TemplatesPath = dlg.SelectedPath;
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

                string[] files = Directory.GetFiles(TemplatesPath, "*.yaml", SearchOption.TopDirectoryOnly);
                foreach (string f in files)
                    TemplateFiles.Add(new TemplateItem { FullPath = f, DisplayName = Path.GetFileName(f) });

                OnPropertyChanged(nameof(TemplateFiles));
                SelectedTemplateFile = TemplateFiles.FirstOrDefault()?.FullPath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load templates:\n{ex.Message}", "Template Picker");
            }
}

        private void LoadTemplate(string path) => Template = TemplateParser.ParseFromFile(path);

        private void ExportYaml()
        {
            if (Template is null)
            {
                MessageBox.Show("No template loaded.", "Export YAML");
                return;
            }

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Player YAML",
                Filter = "Yaml Files (*.yaml)|*.yaml",
                FileName = $"{PlayerName}.yaml"
            };

            if (sfd.ShowDialog() == true)
            {
                //var yaml = YamlGenerator.GeneratePlayerYaml(Template, PlayerName);
                //File.WriteAllText(sfd.FileName, yaml);
            }
        }
    }
}
