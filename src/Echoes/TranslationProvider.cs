using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace Echoes;

public static class TranslationProvider
{
    private static CultureInfo _culture;
    private static ConcurrentDictionary<string, FileTranslationProvider> _providers;

    public static event EventHandler<CultureInfo>? OnCultureChanged;

    public static CultureInfo Culture => _culture;

    static TranslationProvider()
    {
        _culture = CultureInfo.CurrentUICulture;
        _providers = new ConcurrentDictionary<string, FileTranslationProvider>();
    }

    public static void SetCulture(CultureInfo culture)
    {
        _culture = culture;
        OnCultureChanged?.Invoke(null, _culture);
    }

    public static string ReadTranslation(Assembly assembly, string file, string key, CultureInfo culture)
    {
        if (_providers.TryGetValue(file, out var provider))
        {
            return provider.ReadTranslation(key, culture);
        }
        else
        {
            var newProvider =  new FileTranslationProvider(assembly, file);

            _providers.TryAdd(file, newProvider);

            return newProvider.ReadTranslation(key, culture);
        }
    }
}