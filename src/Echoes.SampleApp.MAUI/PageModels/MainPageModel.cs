using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Globalization;
using System.Windows.Input;


namespace Echoes.SampleApp.MAUI.PageModels
{
    public partial class MainPageModel : ObservableObject
    {
        public ICommand SetCulture { get; }

        public MainPageModel()
        {
            SetCulture = new RelayCommand<string>(SetCultureCommand);
        }

        private void SetCultureCommand(string? parameter)
        {
            switch (parameter)
            {
                case "english":
                    TranslationProvider.SetCulture(CultureInfo.GetCultureInfo("en-US"));
                    break;

                case "german":
                    TranslationProvider.SetCulture(CultureInfo.GetCultureInfo("de-DE"));
                    break;

                case "chinese":
                    TranslationProvider.SetCulture(CultureInfo.GetCultureInfo("zh-CN"));
                    break;
            }
        }
    }
}