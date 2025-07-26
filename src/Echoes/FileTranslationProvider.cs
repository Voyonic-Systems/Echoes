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
    private (CultureInfo Culture, ImmutableDictionary<string, string> Lookup)? _translations;

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

        var lookup = _translations?.Lookup;
        var lookupCulture = _translations?.Culture;

        if (lookup == null || (!lookupCulture?.Equals(culture) ?? false))
        {
            var fileName = Path.GetFileNameWithoutExtension(_embeddedResourceKey);
            var fullName = fileName + "_" + culture.Name + ".toml";

            ImmutableDictionary<string, string>? resource = ReadResource(_assembly, fullName);
            if (resource == null)
            {
                fullName = fileName + "_" + culture.TwoLetterISOLanguageName + ".toml";
                resource = ReadResource(_assembly, fullName);
            }
            var fullMatch = resource ?? ImmutableDictionary<string, string>.Empty;
            _translations = (culture, fullMatch);

            lookup = fullMatch;
        }

        if (lookup!.TryGetValue(key, out var result))
        {
            return result;
        }
        else if (_invariantTranslations.TryGetValue(key, out var invariantResult))
        {
            return invariantResult;
        }
        else
        {
            // NOTE: This should never happen!
            return "TRANSLATION NOT FOUND: " + key;
        }
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

        if (root.RawTable.TryGetValue("translations", out var translations))
        {
            var immutableDict = ImmutableDictionary.CreateBuilder<string, string>();

            if (translations?.AsTable is TomlTable tomlTable)
            {
                foreach (var pair in tomlTable.RawTable)
                {
                    if (pair.Value?.IsString == true)
                    {
                        immutableDict.Add(pair.Key, pair.Value.AsString!);
                    }
                }
            }

            return immutableDict.ToImmutable();
        }
        else
        {
            var immutableDict = ImmutableDictionary.CreateBuilder<string, string>();

            foreach (var pair in root.RawTable)
            {
                if (pair.Value?.IsString == true)
                {
                    immutableDict.Add(pair.Key, pair.Value.AsString!);
                }
            }

            return immutableDict.ToImmutable();
        }
    }
}