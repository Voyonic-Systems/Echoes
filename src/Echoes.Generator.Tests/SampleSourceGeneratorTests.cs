using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Echoes.Generator.Tests;

public class GeneratorTests
{
    private const string TranslationFileText =
@"
[echoes_config]
generated_namespace = ""Echoes.SampleApp.Translations""
generated_class_name = ""Strings""

[translations]
hello_world = 'Hello World'
greeting = 'Hello {0}, how are you?'
";

    [Fact]
    public void GenerateClassesBasedOnDDDRegistry()
    {
        // Create an instance of the source generator.
        var generator = new Generator();

        IEnumerable<AdditionalText> additionalTexts = new[]
            {
                // Add the additional file separately from the compilation.
                new Utils.TestAdditionalFile("./Strings.toml", TranslationFileText)
            } as IEnumerable<AdditionalText>;

        // Source generators should be tested using 'GeneratorDriver'.
        var driver = CSharpGeneratorDriver.Create([generator.AsSourceGenerator()], additionalTexts, CSharpParseOptions.Default);

        // To run generators, we can use an empty compilation.
        var compilation = CSharpCompilation.Create(nameof(GeneratorTests));

        // Run generators. Don't forget to use the new compilation rather than the previous one.
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var newCompilation, out _);

        // Retrieve all files in the compilation.
        var generatedFiles = newCompilation.SyntaxTrees
            .Select(t => Path.GetFileName(t.FilePath))
            .ToArray();

        // In this case, it is enough to check the file name.
        Assert.Equivalent(new[]
        {
            "User.g.cs",
            "Document.g.cs",
            "Customer.g.cs"
        }, generatedFiles);
    }
}