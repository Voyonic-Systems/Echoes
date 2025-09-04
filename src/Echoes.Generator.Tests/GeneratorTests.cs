using Echoes.Generator.Tests.Utils;
using Microsoft.CodeAnalysis;
using System.Linq;
using Xunit;

namespace Echoes.Generator.Tests
{
    public class GeneratorTests
    {
        private const string InvariantToml = """
        [echoes_config]
        generated_namespace = "Echoes.SampleApp.Translations"
        generated_class_name = "Strings"
        
        [translations]
        title = "Testing Echoes"
        dialog.ok = "Ok"
        dialog.cancel = "Cancel"
        
        [nested.level1.level2]
        nestedstr1 = "Nesting"
        """;

        // Language file has NO [echoes_config] on purpose
        private const string DeToml = """
        [translations]
        title = "Echoes testen"
        dialog.cancel = "Abbrechen"
        additional_not_in_invariant = "Nur in de"
        """;


        [Fact]
        public void Generates_Single_File_With_Expected_Content()
        {
            // Arrange: one invariant TOML file (has [echoes_config])
            AdditionalText[] files =
            [
                new TestAdditionalFile("Translations.toml", InvariantToml)
            ];

            // Act
            var (_, outputs) = GeneratorTestHost.RunMany(files, new Generator());

            // Assert: exactly one generated file with expected name
            Assert.Single(outputs);
            var (fileName, text) = outputs.Single();
            Assert.Equal("Translations.g.cs", fileName);

            // namespace + class
            Assert.Contains("namespace Echoes.SampleApp.Translations", text);
            Assert.Contains("public static class Strings", text);

            // Backing fields
            Assert.Contains("private static readonly string _file = @\"Translations.toml\";", text);
            Assert.Contains("private static readonly Assembly _assembly = typeof(Strings).Assembly;", text);

            // Assert: root property
            Assert.Contains("public static TranslationUnit title => new TranslationUnit(_assembly, _file, \"title\");", text);

            // Assert: nested classes for dotted/table paths
            Assert.Contains("public static class dialog", text);
            Assert.Contains("public static TranslationUnit ok => new TranslationUnit(_assembly, _file, \"dialog.ok\");", text);
            Assert.Contains("public static TranslationUnit cancel => new TranslationUnit(_assembly, _file, \"dialog.cancel\");", text);

            Assert.Contains("public static class nested", text);
            Assert.Contains("public static class level1", text);
            Assert.Contains("public static class level2", text);
            Assert.Contains("public static TranslationUnit nestedstr1 => new TranslationUnit(_assembly, _file, \"nested.level1.level2.nestedstr1\");", text);
        }

        [Fact]
        public void Emits_Single_Source_For_Invariant_Ignores_LanguageOnly_File()
        {
            AdditionalText[] files =
            [
                new TestAdditionalFile("Translations.toml", InvariantToml),
            new TestAdditionalFile("Translations_de.toml", DeToml)
            ];

            var (_, outputs) = GeneratorTestHost.RunMany(files, new Generator());

            // Only the invariant file produces a .g.cs
            Assert.Single(outputs);
            var (fileName, text) = outputs.Single();
            Assert.Equal("Translations.g.cs", fileName);

            // namespace + class
            Assert.Contains("namespace Echoes.SampleApp.Translations", text);
            Assert.Contains("public static class Strings", text);

            // Backing fields
            Assert.Contains("private static readonly string _file = @\"Translations.toml\";", text);
            Assert.Contains("private static readonly Assembly _assembly = typeof(Strings).Assembly;", text);

            // Root + nested members
            Assert.Contains("public static TranslationUnit title => new TranslationUnit(_assembly, _file, \"title\");", text);
            Assert.Contains("public static class dialog", text);
            Assert.Contains("public static TranslationUnit ok => new TranslationUnit(_assembly, _file, \"dialog.ok\");", text);
            Assert.Contains("public static TranslationUnit cancel => new TranslationUnit(_assembly, _file, \"dialog.cancel\");", text);

            Assert.Contains("public static class nested", text);
            Assert.Contains("public static class level1", text);
            Assert.Contains("public static class level2", text);
            Assert.Contains("public static TranslationUnit nestedstr1 => new TranslationUnit(_assembly, _file, \"nested.level1.level2.nestedstr1\");", text);

            // Does NOT contain entries only in the de file
            Assert.DoesNotContain("additional_not_in_invariant", text);
        }

        [Fact]
        public void Generated_Source_Has_No_Compilation_Errors()
        {
            AdditionalText[] files =
            [
                new TestAdditionalFile("Translations.toml", InvariantToml)
            ];

            var (compilation, outputs) = GeneratorTestHost.RunMany(files, new Generator());

            Assert.Single(outputs);
            var errors = compilation.GetDiagnostics()
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToArray();

            Assert.Empty(errors);
        }

        [Fact]
        public void Preserves_Relative_Path_In__file_Field()
        {
            const string invariantToml = """
            [echoes_config]
            generated_namespace = "Echoes.SampleApp.Translations"
            generated_class_name = "Strings"

            title = "Testing Echoes"
            """;

            // Note the subfolder in the path
            AdditionalText[] files =
            [
                new TestAdditionalFile("./Locales/Translations.toml", invariantToml)
            ];

            var (_, outputs) = GeneratorTestHost.RunMany(files, new Generator());
            var text = outputs.Single().Value;

            Assert.Contains("private static readonly string _file = @\"./Locales/Translations.toml\";", text);
        }

        [Fact]
        public void Emits_Two_Sources_For_Two_Invariant_Files()
        {
            const string featureA = """
            [echoes_config]
            generated_namespace = "Echoes.SampleApp.FeatureA"
            generated_class_name = "FeatureAStrings"

            title = "A"
            """;

            const string featureB = """
            [echoes_config]
            generated_namespace = "Echoes.SampleApp.FeatureB"
            generated_class_name = "FeatureBStrings"

            title = "B"
            """;

            AdditionalText[] files =
            [
                new TestAdditionalFile("FeatureA.toml", featureA),
                new TestAdditionalFile("FeatureB.toml", featureB)
            ];

            var (_, outputs) = GeneratorTestHost.RunMany(files, new Generator());

            Assert.Equal(2, outputs.Count);
            Assert.Contains("FeatureA.g.cs", outputs.Keys);
            Assert.Contains("FeatureB.g.cs", outputs.Keys);

            var a = outputs["FeatureA.g.cs"];
            Assert.Contains("namespace Echoes.SampleApp.FeatureA", a);
            Assert.Contains("public static class FeatureAStrings", a);

            var b = outputs["FeatureB.g.cs"];
            Assert.Contains("namespace Echoes.SampleApp.FeatureB", b);
            Assert.Contains("public static class FeatureBStrings", b);
        }
    }
}
