using System;
using Avalonia;
using Avalonia.Markup.Xaml;

namespace Echoes;

public sealed class Translate : MarkupExtension
{
    public required TranslationUnit Unit { get; init; }

    public Translate() { }

    public Translate(TranslationUnit unit)
    {
        Unit = unit;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return Unit.Value.ToBinding();
    }
}
