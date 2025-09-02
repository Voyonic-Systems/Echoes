using System;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using Tommy;

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
            ReadResource(assembly, embeddedResourceKey)
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
                specificResource = ReadResource(_assembly, specificFileName);
            }

            // Try to load the language-only culture file (e.g., de)
            ImmutableDictionary<string, string>? languageResource = null;
            if (!string.IsNullOrEmpty(culture.TwoLetterISOLanguageName) &&
                culture.TwoLetterISOLanguageName != culture.Name) // Only if different from specific
            {
                var languageFileName = $"{fileName}_{culture.TwoLetterISOLanguageName}.toml";
                languageResource = ReadResource(_assembly, languageFileName);
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

    private static ImmutableDictionary<string, string>? ReadResource(Assembly assembly, string file)
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
        var root = TOML.Parse(reader);

        var immutableDict = ImmutableDictionary.CreateBuilder<string, string>();

        // Process all sections except echoes_config
        foreach (var section in root.RawTable)
        {
            if (section.Key == "echoes_config")
                continue;

            if (section.Value.IsTable)
            {
                // Special handling for [translations] - process directly at root level
                if (section.Key == "translations")
                {
                    ProcessTable(section.Value.AsTable, "", immutableDict);
                }
                else
                {
                    // Other sections become prefixed entries
                    ProcessTable(section.Value.AsTable, section.Key, immutableDict);
                }
            }
            else if (section.Value.IsString)
            {
                // Top-level string values (though this would be unusual)
                immutableDict.Add(section.Key, section.Value.AsString);
            }
        }

        return immutableDict.ToImmutable();
    }

    private static void ProcessTable(TomlTable table, string prefix, ImmutableDictionary<string, string>.Builder builder)
    {
        foreach (var item in table.RawTable)
        {
            var key = item.Key;
            var value = item.Value;
            var fullPath = string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";

            if (value.IsString)
            {
                // Add the string value with its full dotted path as the key
                builder.Add(fullPath, value.AsString);
            }
            else if (value.IsTable)
            {
                // Recursively process nested tables
                ProcessTable(value.AsTable, fullPath, builder);
            }
            // Note: Arrays and other types are ignored for now
        }
    }
}