using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Echoes.SampleApp;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private string _name;

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string CurrentCulture => TranslationProvider.Culture.Name;

    public void SetCultureCommand(object parameter)
    {
        switch (parameter)
        {
            case "en":
                TranslationProvider.SetCulture(CultureInfo.GetCultureInfo("en-US"));
                break;

            case "de":
                TranslationProvider.SetCulture(CultureInfo.GetCultureInfo("de"));
                break;

            case "de-AT":
                TranslationProvider.SetCulture(CultureInfo.GetCultureInfo("de-AT"));
                break;

            case "zh":
                TranslationProvider.SetCulture(CultureInfo.GetCultureInfo("zh-CN"));
                break;
        }
        OnPropertyChanged(nameof(CurrentCulture));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}