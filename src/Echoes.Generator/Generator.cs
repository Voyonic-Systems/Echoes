using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Tommy;

namespace Echoes.Generator;

[Generator]
public class Generator : IIncrementalGenerator
{
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

    public record InvariantLanguageFile
    {
        public string ProjectRelativeTomlFilePath { get; }
        public string GeneratorNamespace { get; }
        public string GeneratorClassName { get; }
        public TranslationGroup RootGroup { get; }

        public InvariantLanguageFile
        (
            string projectRelativeTomlFilePath,
            string generatorNamespace,
            string generatorClassName,
            TranslationGroup rootGroup
        )
        {
            ProjectRelativeTomlFilePath = projectRelativeTomlFilePath;
            GeneratorNamespace = generatorNamespace;
            GeneratorClassName = generatorClassName;
            RootGroup = rootGroup;
        }
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // This will provide all additional files as input to your pipeline
        var additionalFiles = context.AdditionalTextsProvider;

        IncrementalValueProvider<string> projectDirProvider = context.AnalyzerConfigOptionsProvider
           .Select((provider, cancellationToken) =>
           {
               // Try to get the value from MSBuild properties.
               provider.GlobalOptions.TryGetValue("build_property.projectdir", out var projectDir);
               return projectDir ?? string.Empty;
           });

        IncrementalValuesProvider<AdditionalText> translationFiles = context.AdditionalTextsProvider.Where(text => IsFileRelevant(text));

        var combinedProvider = translationFiles.Combine(projectDirProvider);

        IncrementalValuesProvider<(string fileName, string content)> fileContents = combinedProvider.Select((source, cancellationToken) =>
        {
            (AdditionalText text, string projectDir) = source;

            var fileName = Path.GetFileNameWithoutExtension(text.Path);
            var content = GenerateKeysFileText(text, projectDir);

            return (fileName, content);
        });

        context.RegisterSourceOutput(fileContents, static (spc, data) =>
        {
            var (fileName, sourceCode) = data;
            spc.AddSource($"{fileName}.g.cs", sourceCode);
        });
    }

    private static bool IsFileRelevant(AdditionalText? additionalFile)
    {
        if (additionalFile == null)
            return false;

        if (!additionalFile.Path.EndsWith(".toml"))
            return false;

        var text = additionalFile.GetText();

        if (text == null)
            return false;

        var stringText = text.ToString();

        return stringText.Contains("[echoes_config]");
    }

    private static InvariantLanguageFile? ParseTomlFiles(AdditionalText translationFile, string projectDir)
    {
        var text = translationFile.GetText()?.ToString() ?? string.Empty;
        var reader = new StringReader(text);
        var parser = new TOMLParser(reader);
        var root = parser.Parse();

        if (!root.RawTable.TryGetValue("echoes_config", out var echoesConfig))
            return null;

        if (!echoesConfig.IsTable)
            return null;

        if (!echoesConfig.AsTable.RawTable.TryGetValue("generated_class_name", out var generatedClassName))
            return null;

        if (!generatedClassName.IsString)
            return null;

        if (!echoesConfig.AsTable.RawTable.TryGetValue("generated_namespace", out var generatedNamespace))
            return null;

        if (!generatedNamespace.IsString)
            return null;

        var sourceFile = translationFile.Path;
        var trimmedSourceFile = sourceFile;

        if (sourceFile.StartsWith(projectDir))
        {
            trimmedSourceFile = sourceFile.Substring(projectDir.Length);
        }

        // Build the nested structure
        var rootGroup = BuildTranslationStructure(root);

        return new InvariantLanguageFile(
            trimmedSourceFile,
            generatedNamespace.AsString,
            generatedClassName.AsString,
            rootGroup
        );
    }

    private static TranslationGroup BuildTranslationStructure(TomlTable root)
    {
        var rootGroup = new TranslationGroup("");

        // Process all sections except echoes_config
        foreach (var section in root.RawTable.Where(kvp => kvp.Key != "echoes_config"))
        {
            var sectionKey = section.Key;
            var sectionContent = section.Value;

            if (sectionContent.IsTable)
            {
                // Special handling for [translations] - put entries directly in root
                if (sectionKey == "translations")
                {
                    ProcessSection(sectionContent.AsTable, "", rootGroup);
                }
                else
                {
                    // Other sections become nested classes
                    ProcessSection(sectionContent.AsTable, sectionKey, rootGroup);
                }
            }
        }
        return rootGroup;
    }

    private static void ProcessSection(TomlTable table, string prefix, TranslationGroup rootGroup)
    {
        foreach (var item in table.RawTable)
        {
            var key = item.Key;
            var content = item.Value;
            var fullPath = string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";

            if (content.IsString)
            {   
                AddTranslationEntry(fullPath, rootGroup);
            }
            else if (content.IsTable)
            {
                // Recursively process nested tables
                ProcessSection(content.AsTable, fullPath, rootGroup);
            }
        }
    }

    private static void AddTranslationEntry(string fullPath, TranslationGroup rootGroup)
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

    private static string GenerateKeysFileText(AdditionalText translationFile, string projectDir)
    {
        var file = ParseTomlFiles(translationFile, projectDir);

        if (file == null)
            throw new Exception("Failed to parse translation file");

        var sb = new StringBuilder();

        sb.AppendLine($"using Echoes;");
        sb.AppendLine($"using System;");
        sb.AppendLine($"using System.Reflection;");
        sb.AppendLine($"");
        sb.AppendLine($"namespace {file.GeneratorNamespace};");
        sb.AppendLine("");
        sb.AppendLine($"// {file.ProjectRelativeTomlFilePath}");
        sb.AppendLine($"// <auto-generated/>");
        sb.AppendLine($"public static class {file.GeneratorClassName}");
        sb.AppendLine("{");

        sb.AppendLine($"\tprivate static readonly string _file = @\"{file.ProjectRelativeTomlFilePath}\";");
        sb.AppendLine($"\tprivate static readonly Assembly _assembly = typeof({file.GeneratorClassName}).Assembly;");

        GenerateGroupContent(sb, file.RootGroup, 1);

        sb.AppendLine("}");

        return sb.ToString();
    }

    private static void GenerateGroupContent(StringBuilder sb, TranslationGroup group, int indentLevel)
    {
        var indent = new string('\t', indentLevel);

        // Generate entries for this group
        foreach (var entry in group.Entries.Values.OrderBy(e => e.Key))
        {
            sb.AppendLine($"{indent}public static TranslationUnit {entry.Key} => new TranslationUnit(_assembly, _file, \"{entry.FullPath}\");");
        }

        // Generate nested classes (sorted for consistent output)
        foreach (var subGroup in group.SubGroups.Values.OrderBy(g => g.Name))
        {
            sb.AppendLine($"{indent}public static class {subGroup.Name}");
            sb.AppendLine($"{indent}{{");

            GenerateGroupContent(sb, subGroup, indentLevel + 1);

            sb.AppendLine($"{indent}}}");
        }
    }
}