using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

using Echoes.Common;

namespace Echoes;

public class FileTranslationProvider
{
    private readonly string _embeddedResourceKey;
    private readonly Assembly _assembly;

    private readonly ImmutableDictionary<string, string> _invariantTranslations;
    private (CultureInfo Culture, ImmutableDictionary<string, string>? SpecificLookup, ImmutableDictionary<string, string>? LanguageLookup)? _translations;

    public static bool LookForFilesOnDisk { get; set; }
    public static string FilesLocation { get; set; } = string.Empty;

    public FileTranslationProvider(Assembly assembly, string embeddedResourceKey)
    {
        _embeddedResourceKey = embeddedResourceKey;
        _assembly = assembly;

        _invariantTranslations =
            ReadResource(assembly, embeddedResourceKey)
            ?? throw new Exception("Embedded resource could not be found. ");

        _translations = null;
    }

    /// <summary>
    /// Scans the assembly and optionally a disk folder for translation files.
    /// </summary>
    /// <param name="assembly">An optional assembly to look for translations</param>
    /// <param name="fileName">The base name of the file without the ".toml" extension to look for</param>
    /// <returns></returns>

    public static IList<string> ListTranslationFiles(Assembly? assembly, string fileName)
    {
        List<string> fileNames = [];

        string resourceName = fileName.Replace("/", ".").Replace(@"\", ".");

        if (assembly is not null)
        {
            var resourceNames = assembly.GetManifestResourceNames();
            var resourcePaths = resourceNames.Where(str => str.Contains(resourceName, StringComparison.OrdinalIgnoreCase));
            string baseName;
            int idx;
            string fileNameMatch = "." + fileName;
            foreach (var resourcePath in resourcePaths)
            {
                // we enumerate language-specific files like "strings_de-AT",
                // and we have to match the generic name, such as "strings", with the name of the resource.
                // But then, we add a specific name to the list so that we can turn it to the CultureInfo object
                idx = resourcePath.LastIndexOf("_");
                if (idx > 0)
                {
                    baseName = resourcePath[..idx];
                    if (baseName.EndsWith(fileNameMatch, StringComparison.OrdinalIgnoreCase))
                    {
                        idx = baseName.LastIndexOf(fileNameMatch, StringComparison.OrdinalIgnoreCase);
                        if (idx < resourcePath.Length -1)
                            fileNames.Add(resourcePath[(idx+1)..]);
                    }
                }
            }
        }

        if (LookForFilesOnDisk)
        {
            string? filePath;
            if (string.IsNullOrEmpty(FilesLocation))
                filePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            else
                filePath = FilesLocation;

            if (!string.IsNullOrEmpty(filePath))
            {
                // Enumerate all .toml files, strip the extension, check if the file's base name matches "fileName", and add all matching filenames to the list
                string fileNameMatch = fileName + "_";
                string name;
                var diskFiles = Directory.EnumerateFiles(filePath, "*.toml", new EnumerationOptions() { RecurseSubdirectories = false, IgnoreInaccessible = true, MatchType = MatchType.Win32 });
                foreach (var diskFile in diskFiles)
                {
                    name = Path.GetFileName(diskFile);
                    if (name.StartsWith(fileNameMatch, StringComparison.OrdinalIgnoreCase))
                        fileNames.Add(name);
                }
            }
        }
        return fileNames;
    }

    /// <summary>
    /// From the list of available language files obtained using <see cref="ListTranslationFiles()"/>, retrieve culture information (needed for language names and to switch application language)
    /// </summary>
    /// <param name="fileNames">The list of names obtained from <see cref="ListTranslationFiles()"/>.</param>
    /// <returns>The list of<see cref="System.Globalization.CultureInfo"/> </returns>
    public static IList<CultureInfo> ListCultures(IList<string> fileNames)
    {
        IList<CultureInfo> result = [];
        string cultureName;
        CultureInfo cultureInfo;
        int idx;
        foreach (var filename in fileNames)
        {
            idx = filename.LastIndexOf('_');
            if (idx >= 0 && idx < filename.Length - 1)
            {
                if (filename.EndsWith(".toml", StringComparison.OrdinalIgnoreCase))
                    cultureName = filename[(idx + 1)..^5];
                else
                    cultureName = filename[(idx + 1)..];
                try
                {
                    // We obtain the culture for the given code.
                    // If it is neutral (no region specified),
                    // we use CreateSpecificCulture method to obtain a culture for the default region.
                    // The mapping to default regions is hardcoded into .NET for all neutral cultures.
                    cultureInfo = new CultureInfo(cultureName);
                    if (cultureInfo.IsNeutralCulture)
                    {
                        cultureInfo = CultureInfo.CreateSpecificCulture(cultureName);
                    }
                    result.Add(cultureInfo);
                    continue;
                }
                catch (CultureNotFoundException)
                {
                    // ignore
                }
            }
        }
        return result;
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
        ImmutableDictionary<string, string>? resourceTranslations = null;
        ImmutableDictionary<string, string>? diskTranslations = null;

        var resourceNames = assembly.GetManifestResourceNames();
        var resourcePath =
            resourceNames
                .FirstOrDefault(str => str.EndsWith(file.Replace("/", ".").Replace(@"\", "."), StringComparison.OrdinalIgnoreCase));

        if (resourcePath is not null)
        {
            using var stream = assembly.GetManifestResourceStream(resourcePath);
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);

                var tomlContent = reader.ReadToEnd();

                // Use the shared parser to get translations as a flat dictionary
                resourceTranslations = TomlTranslationParser.ParseTranslations(tomlContent);
            }
        }

        if (LookForFilesOnDisk)
        {
            // Try to load a file from the disk
            var filePath = FilesLocation;
            if (string.IsNullOrEmpty(filePath))
                filePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(filePath))
            {
                filePath = Path.Combine(filePath, file);
                if (File.Exists(filePath))
                {
                    using var reader = File.OpenText(filePath);
                    if (reader is not null)
                    {
                        var tomlContent = reader.ReadToEnd();

                        // Use the shared parser to get translations as a flat dictionary
                        diskTranslations = TomlTranslationParser.ParseTranslations(tomlContent);
                    }
                }
            }
        }

        if (resourceTranslations is not null && diskTranslations is not null)
        {
            var builder = ImmutableDictionary.CreateBuilder<string, string>();
            builder.AddRange(resourceTranslations);

            foreach (var key in diskTranslations.Keys)
            {
                builder[key] = diskTranslations[key];
            }
            return builder.ToImmutable();
        }
        else
        if (resourceTranslations is not null)
            return resourceTranslations;
        else
        if (diskTranslations is not null)
            return diskTranslations;

        return null;
    }
}