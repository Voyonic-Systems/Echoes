using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

using Echoes;

using Xunit;

namespace Echoes.IntegrationTests
{
    public class TranslationIntegrationTests
    {
        [Fact]
        public void Generated_Code_Compiles_And_Loads()
        {
            var generatedType = Type.GetType("Echoes.IntegrationTests.TestTranslations.TestStrings, Echoes.IntegrationTests.TestData");
            Assert.NotNull(generatedType);

            var fileField = generatedType.GetField("_file", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(fileField);

            var assemblyField = generatedType.GetField("_assembly", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(assemblyField);
        }

        [Fact]
        public void Simple_Translation_Retrieval_Works()
        {
            TranslationProvider.SetCulture(new CultureInfo("en"));

            var title = TestTranslations.TestStrings.app_title.CurrentValue;
            Assert.Equal("Echoes Sample App", title);
        }

        [Fact]
        public void Nested_Translation_Structure_Works()
        {
            TranslationProvider.SetCulture(new CultureInfo("en"));

            var ok = TestTranslations.TestStrings.dialog.ok;
            var cancel = TestTranslations.TestStrings.dialog.cancel;

            Assert.Equal("dialog.ok", ok.Key);
            Assert.Equal("OK", ok.CurrentValue);
            Assert.Equal("dialog.cancel", cancel.Key);
            Assert.Equal("Cancel", cancel.CurrentValue);
        }

        [Theory]
        [InlineData("en", "Welcome to Echoes!")]
        [InlineData("de", "Willkommen bei Echoes!")]
        [InlineData("de-AT", "Servus bei Echoes!")]
        public void Locale_Fallback_Chain_Works(string cultureName, string expectedWelcome)
        {
            TranslationProvider.SetCulture(new CultureInfo(cultureName));

            var welcome = TestTranslations.TestStrings.welcome.CurrentValue;

            Assert.Equal(expectedWelcome, welcome);
        }

        [Fact]
        public void Missing_Translation_Falls_Back_To_Less_Specific()
        {
            TranslationProvider.SetCulture(new CultureInfo("de-AT"));

            var germanOnly = TestTranslations.TestStrings.german_general.CurrentValue;

            Assert.Equal("Dieser Text hat eine deutsche Übersetzung, aber keine österreichische Variante", germanOnly);
        }

        [Fact]
        public void Missing_Translation_Falls_Back_To_Invariant()
        {
            TranslationProvider.SetCulture(new CultureInfo("de"));

            var invariantOnly = TestTranslations.TestStrings.invariant_only.CurrentValue;

            Assert.Equal("This text only exists in English", invariantOnly);
        }

        [Fact]
        public void Deeply_Nested_Translations_Work()
        {
            TranslationProvider.SetCulture(new CultureInfo("en"));

            var openValue = TestTranslations.TestStrings.menu.m_file.open.CurrentValue;

            Assert.Equal("Open", openValue);
            Assert.Equal("menu.m_file.open", TestTranslations.TestStrings.menu.m_file.open.Key);
        }

        [Fact]
        public void Translation_Unit_Properties_Are_Correct()
        {
            var unit = TestTranslations.TestStrings.dialog.ok;

            Assert.Equal("dialog.ok", unit.Key);
            Assert.Equal(@"TestTranslations\Strings.toml", unit.SourceFile);
        }

        [Fact]
        public void Culture_Switch_Updates_Values()
        {
            var unit = TestTranslations.TestStrings.app_title;

            TranslationProvider.SetCulture(new CultureInfo("en"));
            var englishValue = unit.CurrentValue;
            Assert.Equal("Echoes Sample App", englishValue);

            TranslationProvider.SetCulture(new CultureInfo("de"));
            var germanValue = unit.CurrentValue;
            Assert.Equal("Echoes Beispiel-App", germanValue);

            TranslationProvider.SetCulture(new CultureInfo("en"));
            var backToEnglish = unit.CurrentValue;
            Assert.Equal("Echoes Sample App", backToEnglish);
        }

        [Fact]
        public void Nonexistent_Culture_Falls_Back_To_Invariant()
        {
            TranslationProvider.SetCulture(new CultureInfo("fr-FR"));

            var title = TestTranslations.TestStrings.app_title.CurrentValue;

            Assert.Equal("Echoes Sample App", title);
        }

        [Fact]
        public void All_Generated_Properties_Are_TranslationUnits()
        {
            var stringsType = typeof(TestTranslations.TestStrings);

            var appTitleProp = stringsType.GetProperty("app_title", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(appTitleProp);
            Assert.Equal(typeof(TranslationUnit), appTitleProp.PropertyType);

            var dialogType = stringsType.GetNestedType("dialog", BindingFlags.Public);
            Assert.NotNull(dialogType);

            var okProp = dialogType.GetProperty("ok", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(okProp);
            Assert.Equal(typeof(TranslationUnit), okProp.PropertyType);
        }

        [Theory]
        [InlineData("en", "dialog.ok", "OK")]
        [InlineData("de", "dialog.ok", "OK")]
        [InlineData("de", "dialog.cancel", "Abbrechen")]
        [InlineData("de-AT", "dialog.ok", "OK")]
        [InlineData("de-AT", "dialog.cancel", "Abbrechen")]
        public void Translation_Keys_Resolve_Correctly(string culture, string expectedKey, string expectedValue)
        {
            TranslationProvider.SetCulture(new CultureInfo(culture));

            var parts = expectedKey.Split('.');
            object current = typeof(TestTranslations.TestStrings);

            foreach (var part in parts)
            {
                if (current is Type currentType)
                {
                    var nested = currentType.GetNestedType(part, BindingFlags.Public);
                    if (nested != null)
                    {
                        current = nested;
                    }
                    else
                    {
                        var prop = currentType.GetProperty(part, BindingFlags.Public | BindingFlags.Static);
                        Assert.NotNull(prop);
                        current = prop.GetValue(null);
                    }
                }
            }

            Assert.IsType<TranslationUnit>(current);
            var unit = (TranslationUnit)current;
            Assert.Equal(expectedValue, unit.CurrentValue);
        }

        [Fact]
        public void Complex_Nested_Structure_Generates_Correctly()
        {
            var menuType = typeof(TestTranslations.TestStrings).GetNestedType("menu", BindingFlags.Public);
            Assert.NotNull(menuType);

            var fileType = menuType.GetNestedType("m_file", BindingFlags.Public);
            Assert.NotNull(fileType);

            var openProp = fileType.GetProperty("open", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(openProp);
            Assert.Equal(typeof(TranslationUnit), openProp.PropertyType);

            var saveProp = fileType.GetProperty("save", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(saveProp);
            Assert.Equal(typeof(TranslationUnit), saveProp.PropertyType);
        }

        [Fact]
        public void Files_Are_Enumerated_On_Disk()
        {
            FileTranslationProvider.LookForFilesOnDisk = true;
            FileTranslationProvider.FilesLocation = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "TestTranslations");
            IList<string> fileNames = FileTranslationProvider.ListTranslationFiles(null, "strings");
            Assert.False(string.IsNullOrEmpty(fileNames.First(s => s.Contains("strings_de-AT.toml", StringComparison.OrdinalIgnoreCase))));
            Assert.False(string.IsNullOrEmpty(fileNames.First(s => s.Contains("strings_de.toml", StringComparison.OrdinalIgnoreCase))));
            Assert.False(string.IsNullOrEmpty(fileNames.First(s => s.Contains("strings_sk.toml", StringComparison.OrdinalIgnoreCase))));
        }

        [Fact]
        public void Files_Are_Enumerated_In_Resources()
        {
            FileTranslationProvider.LookForFilesOnDisk = false;
            IList<string> fileNames = FileTranslationProvider.ListTranslationFiles(typeof(TestTranslations.TestStrings).Assembly, "strings");
            Assert.False(string.IsNullOrEmpty(fileNames.First(s => s.Contains("strings_de-AT.toml", StringComparison.OrdinalIgnoreCase))));
            Assert.False(string.IsNullOrEmpty(fileNames.First(s => s.Contains("strings_de.toml", StringComparison.OrdinalIgnoreCase))));
            // Slovak file is only copied and should not get into resources
            Assert.True(string.IsNullOrEmpty(fileNames.First(s => s.Contains("strings_sk.toml", StringComparison.OrdinalIgnoreCase))));
        }

        [Fact]
        public void CultureInfos_Are_Enumerated()
        {
            FileTranslationProvider.LookForFilesOnDisk = true;
            FileTranslationProvider.FilesLocation = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "TestTranslations");
            IList<string> fileNames = FileTranslationProvider.ListTranslationFiles(null, "strings");
            IList<CultureInfo> cultureInfos = FileTranslationProvider.ListCultures(fileNames);
            Assert.NotNull(cultureInfos.FirstOrDefault(c => c.Name.Equals("de-AT", StringComparison.OrdinalIgnoreCase)));
            Assert.NotNull(cultureInfos.FirstOrDefault(c => c.Name.Equals("de-DE", StringComparison.OrdinalIgnoreCase)));
            Assert.NotNull(cultureInfos.FirstOrDefault(c => c.Name.Equals("sk-SK", StringComparison.OrdinalIgnoreCase)));
        }

        [Fact]
        public void Culture_Is_Loaded_From_File()
        {
            FileTranslationProvider.LookForFilesOnDisk = true;
            FileTranslationProvider.FilesLocation = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "TestTranslations");

            var unit = TestTranslations.TestStrings.app_title;

            TranslationProvider.SetCulture(new CultureInfo("sk"));
            var slovakValue = unit.CurrentValue;
            Assert.Equal("Echoes appka-Príklad", slovakValue);
        }

        [Fact]
        public void Translation_Is_Updated_From_File()
        {
            FileTranslationProvider.LookForFilesOnDisk = true;
            FileTranslationProvider.FilesLocation = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, @"..\..\..");

            var unit = TestTranslations.TestStrings.greeting;

            TranslationProvider.SetCulture(new CultureInfo("de"));
            var germanValue = unit.CurrentValue;
            Assert.StartsWith("Servus!", germanValue);
        }
    }
}
