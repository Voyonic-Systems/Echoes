using System.Globalization;

namespace Echoes.SampleApp.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public void SetCultureCommand(object parameter)
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