using System;
using Avalonia;
using Avalonia.Markup.Xaml;

namespace Echoes;

public sealed class Translate : MarkupExtension
{
     private readonly TranslationUnit _unit;

    public Translate(TranslationUnit unit)
    {
        _unit = unit;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        return _unit.Value.ToBinding();
    }
}