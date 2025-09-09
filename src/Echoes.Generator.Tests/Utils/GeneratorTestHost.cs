using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Echoes.Generator.Tests.Utils
{
    internal static class GeneratorTestHost
    {
        public static (Compilation Compilation, IReadOnlyDictionary<string, string> Files)
        RunMany(IEnumerable<AdditionalText> additionalTexts, Generator generator)
        {
            var parseOptions = CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest);

            // Framework references (System.*, netstandard, etc.)
            var references = GetFrameworkReferences().ToList();

            var syntaxTrees = new List<SyntaxTree>();
            var compilation = CSharpCompilation.Create(
                assemblyName: "Tests",
                syntaxTrees: syntaxTrees,
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var driver = CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], additionalTexts, parseOptions);
            driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);

            var map = updated.SyntaxTrees
                .Where(t => t.FilePath.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(t => Path.GetFileName(t.FilePath), t => t.ToString());

            return (updated, map);
        }

        private static IEnumerable<MetadataReference> GetFrameworkReferences()
        {
            // Load ALL Trusted Platform Assemblies to avoid missing facades (System.Runtime, netstandard, etc.)
            var tpa = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
            if (!string.IsNullOrEmpty(tpa))
            {
                foreach (var path in tpa.Split(Path.PathSeparator))
                {
                    // If you prefer to filter, keep all; it's simplest and reliable for tests.
                    yield return MetadataReference.CreateFromFile(path);
                }
                yield break;
            }
        }
    }
}
