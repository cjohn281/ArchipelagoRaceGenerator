using System.Reflection;
using System.Windows;

namespace RaceConfig.GUI
{
    public partial class AboutWindow : Window
    {
        public string AppName { get; }
        public string Version { get; }
        public string Copyright { get; }
        public string Description { get; }

        public AboutWindow()
        {
            InitializeComponent();

            var asm = Assembly.GetExecutingAssembly();
            AppName = asm.GetName().Name ?? "Archipelago Race Generator";
            var ver = asm.GetName().Version;
            Version = $"Version: {ver?.ToString() ?? "1.0.0.0"}";
            Copyright = "© 2025";
            Description = "Generates Archipelago multiworld YAML files for team races based on game templates and selected options.";

            DataContext = this;
        }
    }
}