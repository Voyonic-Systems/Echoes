using Echoes.Common;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Echoes;

public class FileTranslationProvider
{
    private readonly string _embeddedResourceKey;
    private readonly Assembly _assembly;

    private readonly ImmutableDictionary<string, string> _invariantTranslations;
    private (CultureInfo Culture, ImmutableDictionary<string, string>? SpecificLookup, ImmutableDictionary<string, string>? LanguageLookup)? _translations;

    public FileTranslationProvider(Assembly assembly, string embeddedResourceKey)
    {
        _embeddedResourceKey = embeddedResourceKey;
        _assembly = assembly;

        _invariantTranslations =
            ReadResource(assembly, embeddedResourceKey)?.ToImmutableDictionary()
            ?? throw new Exception("Embedded resource could not be found. ");

        _translations = null;
    }

    public string ReadTranslation(string key, CultureInfo culture)
    {
        if (culture == null)
            throw new ArgumentNullException(nameof(culture));

        var cachedCulture = _translations?.Culture;

        // Check if we need to reload translations for a different culture
        if (_translations == null || (!cachedCulture?.Equals(culture) ?? false))
        {
            var fileName = Path.GetFileNameWithoutExtension(_embeddedResourceKey);

            // Try to load the most specific culture file (e.g., de-AT)
            ImmutableDictionary<string, string>? specificResource = null;
            if (!string.IsNullOrEmpty(culture.Name))
            {
                var specificFileName = $"{fileName}_{culture.Name}.toml";
                specificResource = ReadResource(_assembly, specificFileName)?.ToImmutableDictionary();
            }

            // Try to load the language-only culture file (e.g., de)
            ImmutableDictionary<string, string>? languageResource = null;
            if (!string.IsNullOrEmpty(culture.TwoLetterISOLanguageName) &&
                culture.TwoLetterISOLanguageName != culture.Name) // Only if different from specific
            {
                var languageFileName = $"{fileName}_{culture.TwoLetterISOLanguageName}.toml";
                languageResource = ReadResource(_assembly, languageFileName)?.ToImmutableDictionary();
            }

            // Store both lookups
            _translations = (
                culture,
                specificResource,
                languageResource
            );
        }

        // Try to find the translation in order of specificity
        // 1. Most specific locale (e.g., de-AT)
        if (_translations?.SpecificLookup?.TryGetValue(key, out var specificResult) == true)
        {
            return specificResult;
        }

        // 2. Language-only locale (e.g., de)
        if (_translations?.LanguageLookup?.TryGetValue(key, out var languageResult) == true)
        {
            return languageResult;
        }

        // 3. Invariant culture (fallback)
        if (_invariantTranslations.TryGetValue(key, out var invariantResult))
        {
            return invariantResult;
        }

        // This should never happen
        return "TRANSLATION NOT FOUND: " + key;
    }

    private static IReadOnlyDictionary<string, string>? ReadResource(Assembly assembly, string file)
    {
        var resourceNames = assembly.GetManifestResourceNames();
        var resourcePath =
            resourceNames
                .FirstOrDefault(str => str.EndsWith(file.Replace("/", ".").Replace(@"\", "."), StringComparison.OrdinalIgnoreCase));

        if (resourcePath == null)
            return null;

        using var stream = assembly.GetManifestResourceStream(resourcePath);
        if (stream == null)
            return null;

        using var reader = new StreamReader(stream);
        var tomlContent = reader.ReadToEnd();

        // Use the shared parser to get translations as a flat dictionary
        return TomlTranslationParser.ParseTranslations(tomlContent);
    }
}