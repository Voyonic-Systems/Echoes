using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using Tommy;

namespace Echoes.Generator;

[Generator]
public class Generator : IIncrementalGenerator
{
    public record InvariantLanguageFile
    {
        public string ProjectRelativeTomlFilePath { get; }
        public string GeneratorNamespace { get; }
        public string GeneratorClassName { get; }
        public ImmutableArray<string> Units { get; }

        public InvariantLanguageFile
        (
            string projectRelativeTomlFilePath,
            string generatorNamespace,
            string generatorClassName,
            ImmutableArray<string> units
        )
        {
            ProjectRelativeTomlFilePath = projectRelativeTomlFilePath;
            GeneratorNamespace = generatorNamespace;
            GeneratorClassName = generatorClassName;
            Units = units;
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
            var content = GenerateKeysFileText(text, projectDir, context);

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

    private static InvariantLanguageFile? ParseTomlFiles(AdditionalText translationFile, string projectDir, IncrementalGeneratorInitializationContext context)
    {
        var keys = new List<string>();

        var text = translationFile.GetText()?.ToString() ?? string.Empty;
        var reader = new StringReader(text);
        var parser = new TOMLParser(reader);
        var root = parser.Parse();

        if (!root.RawTable.TryGetValue("echoes_config", out var echoesConfig) || echoesConfig == null)
            return null;

        if (!echoesConfig.IsTable)
            return null;

        if (echoesConfig.AsTable?.RawTable.TryGetValue("generated_class_name", out var generatedClassName) != true || generatedClassName == null)
            return null;

        if (!generatedClassName.IsString)
            return null;

        if (!echoesConfig.AsTable.RawTable.TryGetValue("generated_namespace", out var generatedNamespace) || generatedNamespace == null)
            return null;

        if (!generatedNamespace.IsString)
            return null;

        var sourceFile = translationFile.Path;

        var trimmedSourceFile = sourceFile;

        if (sourceFile.StartsWith(projectDir))
        {
            trimmedSourceFile = sourceFile.Substring(projectDir.Length);
        }

        if (!root.RawTable.TryGetValue("translations", out var translations))
            return null;

        if (translations?.AsTable != null)
        {
            foreach (var pair in translations.AsTable.RawTable)
            {
                if (pair.Value?.IsString == true)
                {
                    keys.Add(pair.Key);
                }
            }
        }

        var units = keys.ToImmutableArray();

        return new InvariantLanguageFile(
            trimmedSourceFile,
            generatedNamespace.AsString!,
            generatedClassName.AsString!,
            units
        );
    }

    private static string GenerateKeysFileText(AdditionalText translationFile, string projectDir, IncrementalGeneratorInitializationContext context)
    {
        var file = ParseTomlFiles(translationFile, projectDir, context);

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

        foreach (var key in file.Units)
        {
            sb.AppendLine($"\tpublic static TranslationUnit {key} => new TranslationUnit(_assembly, _file, \"{key}\");");
        }

        sb.AppendLine("}");

        return sb.ToString();
    }
}