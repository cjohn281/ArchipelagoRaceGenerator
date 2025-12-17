using RaceConfig.GUI.MVVM;

namespace RaceConfig.GUI.ViewModels
{

    internal class MainWindowViewModel : ViewModelBase
    {
        public TeamsViewModel TeamsVM { get; set; }

        public GameOptionsViewModel GameOptionsVM { get; set; }

        public FinalizeSettingsViewModel FinalizeVM { get; set; }

        public RelayCommand TeamsViewCommand => new RelayCommand(execute =>
        {
            CurrentView = TeamsVM;
        });

        public RelayCommand GameOptionsViewCommand => new RelayCommand(execute =>
        {
            CurrentView = GameOptionsVM;
        });

        public RelayCommand FinalizeSettingsViewCommand => new RelayCommand(execute =>
        {
            CurrentView = FinalizeVM;
        });


        private object _currentView;

		public object CurrentView
		{
			get { return _currentView; }
			set {
				_currentView = value;
				OnPropertyChanged();
			}
		}

		public MainWindowViewModel()
		{
			GameOptionsVM = new GameOptionsViewModel();
			TeamsVM = new TeamsViewModel();
            FinalizeVM = new FinalizeSettingsViewModel();

            CurrentView = GameOptionsVM;

        }



    }
}
