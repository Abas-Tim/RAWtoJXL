using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ARWtoJXL.WPF.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool _useSubfolder;

        [ObservableProperty]
        private string _subfolderName;

        [ObservableProperty]
        private int _qualityPreset;

        [ObservableProperty]
        private bool _isSaving;

        public SettingsViewModel()
        {
            var saved = SettingsService.Load();
            UseSubfolder = saved.UseSubfolder;
            SubfolderName = saved.SubfolderName;
            QualityPreset = saved.QualityPreset;
        }

        public ICommand SaveCommand => new RelayCommand(() =>
        {
            SettingsService.Save(new AppSettings
            {
                UseSubfolder = UseSubfolder,
                SubfolderName = SubfolderName,
                QualityPreset = QualityPreset
            });
            IsSaving = false;
        });

        public ICommand CancelCommand => new RelayCommand(() => IsSaving = false);
    }
}
