using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using System.Text;

namespace Echoes.Generator.Tests.Utils;

internal sealed class TestAdditionalFile(string path, string text) : AdditionalText
{
    public override string Path { get; } = path;
    public override SourceText GetText(System.Threading.CancellationToken cancellationToken = default)
        => SourceText.From(text, Encoding.UTF8);
}