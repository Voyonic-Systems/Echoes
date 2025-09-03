using Echoes.Common;
using System.Linq;
using Tommy;
using Xunit;

namespace Echoes.Generator.Tests
{
    public class ParserTests
    {
        [Fact]
        public void ParseTranslations_Basic()
        {
            const string toml = """
            [translations]
            title = "Testing Echoes"
            """;

            var flat = TomlTranslationParser.ParseTranslations(toml);
            Assert.Equal("Testing Echoes", flat["title"]);

            var tree = TomlTranslationParser.BuildTranslationStructure(toml);
            Assert.True(tree.Entries.ContainsKey("title"));
        }

        [Fact]
        public void ParseTranslations_Basic_WithoutTranslationsTable()
        {
            const string toml = """            
            title = "Testing Echoes"
            """;

            var flat = TomlTranslationParser.ParseTranslations(toml);
            Assert.Equal("Testing Echoes", flat["title"]);

            var tree = TomlTranslationParser.BuildTranslationStructure(toml);
            Assert.True(tree.Entries.ContainsKey("title"));
        }

        [Fact]
        public void ParseTranslations_DottedKeys()
        {
            const string toml = """
            [translations]
            title = "Testing Echoes"
            dialog.ok = "Ok"
            dialog.cancel = "Cancel"
            nested.level1.level2.nestedstr1 = "Nesting"
            """;

            var flat = TomlTranslationParser.ParseTranslations(toml);

            Assert.Equal("Testing Echoes", flat["title"]);
            Assert.Equal("Ok", flat["dialog.ok"]);
            Assert.Equal("Cancel", flat["dialog.cancel"]);
            Assert.Equal("Nesting", flat["nested.level1.level2.nestedstr1"]);

            var tree = TomlTranslationParser.BuildTranslationStructure(toml);
            Assert.True(tree.Entries.ContainsKey("title"));
            Assert.True(tree.SubGroups["dialog"].Entries.ContainsKey("ok"));
            Assert.True(tree.SubGroups["dialog"].Entries.ContainsKey("cancel"));
            Assert.True(tree.SubGroups["nested"].SubGroups["level1"].SubGroups["level2"].Entries.ContainsKey("nestedstr1"));
        }

        [Fact]
        public void ParseTranslations_Tables()
        {
            const string toml = """
            [translations]
            title = "Testing Echoes"

            [dialog]
            ok = "Ok"
            cancel = "Cancel"

            [nested.level1.level2]
            nestedstr1 = "Nesting"
            """;

            var flat = TomlTranslationParser.ParseTranslations(toml);

            Assert.Equal("Testing Echoes", flat["title"]);
            Assert.Equal("Ok", flat["dialog.ok"]);
            Assert.Equal("Cancel", flat["dialog.cancel"]);
            Assert.Equal("Nesting", flat["nested.level1.level2.nestedstr1"]);
        }

        [Fact]
        public void ParseTranslations_MixedForSameGroup_Throws()
        {
            // dotted assigns to dialog.ok, then a table [translations.dialog] is declared → invalid
            const string toml = """
            [translations]
            title = "Testing Echoes"
            dialog.ok = "Ok"

            [translations.dialog]
            cancel = "Cancel"
            """;

            Assert.Throws<TomlParseException>(() => TomlTranslationParser.ParseTranslations(toml));
            Assert.Throws<TomlParseException>(() => TomlTranslationParser.BuildTranslationStructure(toml));
        }

        [Fact]
        public void ParseTranslations_MixedAcrossDifferentGroups_Works()
        {
            const string toml = """
            [translations]
            title = "Testing Echoes"
            dialog.ok = "Ok"
            dialog.cancel = "Cancel"

            [translations.nested.level1.level2]
            nestedstr1 = "Nesting"
            """;

            var flat = TomlTranslationParser.ParseTranslations(toml);

            Assert.Equal("Testing Echoes", flat["title"]);
            Assert.Equal("Ok", flat["dialog.ok"]);
            Assert.Equal("Cancel", flat["dialog.cancel"]);
            Assert.Equal("Nesting", flat["nested.level1.level2.nestedstr1"]);

            var tree = TomlTranslationParser.BuildTranslationStructure(toml);
            Assert.True(tree.Entries.ContainsKey("title"));
            Assert.True(tree.SubGroups["dialog"].Entries.ContainsKey("ok"));
            Assert.True(tree.SubGroups["dialog"].Entries.ContainsKey("cancel"));
            Assert.True(tree.SubGroups["nested"].SubGroups["level1"].SubGroups["level2"].Entries.ContainsKey("nestedstr1"));
        }

        [Fact]
        public void ParseConfig_Valid()
        {
            const string toml = """
            [echoes_config]
            generated_class_name = "FeatureAStrings"
            generated_namespace   = "Echoes.SampleApp.FeatureA"

            [translations]
            title = "Testing Echoes"
            """;

            var cfg = TomlTranslationParser.ParseConfig(toml);
            Assert.NotNull(cfg);
            Assert.Equal("FeatureAStrings", cfg!.GeneratedClassName);
            Assert.Equal("Echoes.SampleApp.FeatureA", cfg.GeneratedNamespace);
        }

        [Fact]
        public void ParseTranslations_Excludes_Config_Section()
        {
            const string toml = """
            [echoes_config]
            generated_class_name = "FeatureAStrings"
            generated_namespace   = "Echoes.SampleApp.FeatureA"

            [translations]
            title = "Testing Echoes"
            """;

            var flat = TomlTranslationParser.ParseTranslations(toml);

            // Only "title" should be present
            Assert.Single(flat);
            Assert.True(flat.ContainsKey("title"));
            Assert.Equal("Testing Echoes", flat["title"]);

            // No config artifacts as keys
            Assert.DoesNotContain(flat.Keys, k => k.Contains("generated_class_name"));
            Assert.DoesNotContain(flat.Keys, k => k.Contains("generated_namespace"));
            Assert.DoesNotContain(flat.Keys, k => k.StartsWith("echoes_config"));
        }

        [Fact]
        public void BuildTranslationStructure_Excludes_Config_Section()
        {
            const string toml = """
            [echoes_config]
            generated_class_name = "FeatureAStrings"
            generated_namespace   = "Echoes.SampleApp.FeatureA"

            [translations]
            title = "Testing Echoes"
            """;

            var tree = TomlTranslationParser.BuildTranslationStructure(toml);

            // Root should only have the "title" entry and no subgroups
            Assert.True(tree.Entries.ContainsKey("title"));
            Assert.Equal("title", tree.Entries.Single().Key);
            Assert.Empty(tree.SubGroups);
        }

        [Fact]
        public void RootVsTranslations_DottedAndTables_WithConfig_Equivalent()
        {
            // Version A: everything under [translations]
            const string a = """
            [translations]
            title = "Testing Echoes"
            dialog.ok = "Ok"
            dialog.cancel = "Cancel"

            [translations.nested.level1.level2]
            nestedstr1 = "Nesting"
            """;

            // Version B: same content at root (no [translations] prefix, note: only works without config section)
            const string b = """
            title = "Testing Echoes"
            dialog.ok = "Ok"
            dialog.cancel = "Cancel"

            [nested.level1.level2]
            nestedstr1 = "Nesting"
            """;

            var flatA = TomlTranslationParser.ParseTranslations(a);
            var flatB = TomlTranslationParser.ParseTranslations(b);

            Assert.Equal(flatA, flatB); // identical key->value map

            // Spot-check structure built from both inputs
            var treeA = TomlTranslationParser.BuildTranslationStructure(a);
            var treeB = TomlTranslationParser.BuildTranslationStructure(b);

            Assert.True(treeA.Entries.ContainsKey("title"));
            Assert.True(treeB.Entries.ContainsKey("title"));

            Assert.True(treeA.SubGroups["dialog"].Entries.ContainsKey("ok"));
            Assert.True(treeB.SubGroups["dialog"].Entries.ContainsKey("ok"));

            Assert.True(treeA.SubGroups["dialog"].Entries.ContainsKey("cancel"));
            Assert.True(treeB.SubGroups["dialog"].Entries.ContainsKey("cancel"));

            Assert.True(treeA.SubGroups["nested"].SubGroups["level1"].SubGroups["level2"].Entries.ContainsKey("nestedstr1"));
            Assert.True(treeB.SubGroups["nested"].SubGroups["level1"].SubGroups["level2"].Entries.ContainsKey("nestedstr1"));
        }

        [Fact]
        public void DottedKeys_AtRoot_Work_Without_Translations_Prefix()
        {
            //note: only works without config section
            const string toml = """
            title = "Testing Echoes"
            dialog.ok = "Ok"
            dialog.cancel = "Cancel"
            nested.level1.level2.nestedstr1 = "Nesting"
            """;

            var flat = TomlTranslationParser.ParseTranslations(toml);

            Assert.Equal("Testing Echoes", flat["title"]);
            Assert.Equal("Ok", flat["dialog.ok"]);
            Assert.Equal("Cancel", flat["dialog.cancel"]);
            Assert.Equal("Nesting", flat["nested.level1.level2.nestedstr1"]);
        }

        [Fact]
        public void RootKeysPlacedAfter_Config_BelongTo_Config_AndAreExcluded()
        {
            // This is valid TOML, but the keys after [echoes_config]
            // are actually INSIDE echoes_config. The parser intentionally
            // ignores echoes_config, so those keys won’t appear in translations.
            const string toml = """
            [echoes_config]
            generated_class_name = "Strings"
            generated_namespace  = "Echoes.SampleApp.Translations"

            title = "Testing Echoes"
            dialog.ok = "Ok"        
            dialog.cancel = "Cancel"
            """;

            var flat = TomlTranslationParser.ParseTranslations(toml);

            // Parser excludes echoes_config entirely → no translation keys found
            Assert.Empty(flat);

            var tree = TomlTranslationParser.BuildTranslationStructure(toml);
            Assert.Empty(tree.Entries);
            Assert.Empty(tree.SubGroups);
        }
    }
}
