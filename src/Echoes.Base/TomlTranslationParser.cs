using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Tommy;

namespace Echoes.Common;

public static class TomlTranslationParser
{
    // Configuration constants
    public const string ConfigSectionName = "echoes_config";
    private const string ConfigClassNameKey = "generated_class_name";
    private const string ConfigNamespaceKey = "generated_namespace";
    private const string TranslationsSectionName = "translations";

    public record TomlConfig
    {
        public string GeneratedClassName { get; }
        public string GeneratedNamespace { get; }

        public TomlConfig
        (
            string generatedClassName,
            string generatedNamespace
        )
        {
            GeneratedClassName = generatedClassName;
            GeneratedNamespace = generatedNamespace;
        }
    }

    public record TranslationEntry
    {
        public string Key { get; }
        public string FullPath { get; }

        public TranslationEntry
        (
            string key,
            string fullPath
        )
        {
            Key = key;
            FullPath = fullPath;
        }
    }

    public record TranslationGroup
    {
        public string Name { get; }
        public Dictionary<string, TranslationEntry> Entries { get; }
        public Dictionary<string, TranslationGroup> SubGroups { get; }

        public TranslationGroup
        (
            string name
        )
        {
            Name = name;
            Entries = new Dictionary<string, TranslationEntry>();
            SubGroups = new Dictionary<string, TranslationGroup>();
        }
    }

    /// <summary>
    /// Parses all translation entries from TOML content into a flat dictionary with dotted keys
    /// </summary>
    public static ImmutableDictionary<string, string> ParseTranslations(string tomlContent)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, string>();

        ProcessTomlRoot
        (
            tomlContent,
            (path, value) => builder.Add(path, value),
            null
        );

        return builder.ToImmutable();
    }

    /// <summary>
    /// Parses the echoes_config section from TOML content (for generator)
    /// </summary>    
    public static TomlConfig? ParseConfig(string tomlContent)
    {
        using var reader = new StringReader(tomlContent);
        using var parser = new TOMLParser(reader);
        var root = parser.Parse();

        if (!root.RawTable.TryGetValue(ConfigSectionName, out var echoesConfig))
            return null;

        if (!echoesConfig.IsTable)
            return null;

        if (!echoesConfig.AsTable.RawTable.TryGetValue(ConfigClassNameKey, out var generatedClassName))
            return null;

        if (!generatedClassName.IsString)
            return null;

        if (!echoesConfig.AsTable.RawTable.TryGetValue(ConfigNamespaceKey, out var generatedNamespace))
            return null;

        if (!generatedNamespace.IsString)
            return null;

        return new TomlConfig(generatedClassName.AsString, generatedNamespace.AsString);
    }

    /// <summary>
    /// Builds a hierarchical TranslationGroup structure from TOML content (for generator)
    /// </summary>
    public static TranslationGroup BuildTranslationStructure(string tomlContent)
    {
        var rootGroup = new TranslationGroup("");

        ProcessTomlRoot
        (
            tomlContent,
            null,
            rootGroup
        );

        return rootGroup;
    }

    /// <summary>
    /// Processes the root of the TOML document, handling sections and populating either a flat dictionary or hierarchical structure
    /// </summary>
    /// <param name="tomlContent">Content of the toml file</param>
    /// <param name="flatHandler">Used for creating the flat dictionary, can be null for the generator</param>
    /// <param name="rootGroup">Used for creating the hierarchical structure, can be null for creating the flat dictionary</param>
    private static void ProcessTomlRoot
    (
        string tomlContent,
        System.Action<string, string>? flatHandler,
        TranslationGroup? rootGroup
    )
    {
        using var reader = new StringReader(tomlContent);
        using var parser = new TOMLParser(reader);
        var root = parser.Parse();

        // Process all sections except echoes_config
        foreach (var section in root.RawTable.Where(kvp => kvp.Key != ConfigSectionName))
        {
            var sectionKey = section.Key;
            var sectionContent = section.Value;

            if (sectionContent.IsTable)
            {
                // Special handling for [translations] - process directly at root level
                if (sectionKey == TranslationsSectionName)
                {
                    ProcessSection(sectionContent.AsTable, "", flatHandler, rootGroup);
                }
                else
                {
                    // Other sections become prefixed entries
                    ProcessSection(sectionContent.AsTable, sectionKey, flatHandler, rootGroup);
                }
            }
            else if (sectionContent.IsString)
            {
                flatHandler?.Invoke(sectionKey, sectionContent.AsString);

                if (rootGroup != null)
                    AddToGroup(sectionKey, rootGroup);
            }
        }
    }

    private static void ProcessSection(
        TomlTable table,
        string prefix,
        System.Action<string, string>? flatHandler,
        TranslationGroup? rootGroup)
    {
        foreach (var item in table.RawTable)
        {
            var key = item.Key;
            var value = item.Value;
            var fullPath = string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";

            if (value.IsString)
            {
                // Add to flat dictionary if handler provided
                flatHandler?.Invoke(fullPath, value.AsString);

                // Add to hierarchical structure if root group provided
                if (rootGroup != null)
                    AddToGroup(fullPath, rootGroup);
            }
            else if (value.IsTable)
            {
                // Recursively process nested tables
                ProcessSection(value.AsTable, fullPath, flatHandler, rootGroup);
            }
        }
    }

    private static void AddToGroup(string fullPath, TranslationGroup rootGroup)
    {
        var parts = fullPath.Split('.');
        var currentGroup = rootGroup;

        // Navigate/create the group hierarchy
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var groupName = parts[i];

            if (!currentGroup.SubGroups.ContainsKey(groupName))
            {
                currentGroup.SubGroups[groupName] = new TranslationGroup(groupName);
            }

            currentGroup = currentGroup.SubGroups[groupName];
        }

        // Add the entry to the final group
        var entryKey = parts[parts.Length - 1];
        currentGroup.Entries[entryKey] = new TranslationEntry(entryKey, fullPath);
    }
}