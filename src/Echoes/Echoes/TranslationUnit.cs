using System;
using System.Reactive.Subjects;
using System.Reflection;

namespace Echoes;

public class TranslationUnit
{
    private BehaviorSubject<string?> _value;

    public IObservable<string?> Value => _value;

    public string SourceFile { get; }
    public string Key { get; }

    public TranslationUnit(Assembly assembly, string sourceFile, string key)
    {
        SourceFile = sourceFile;
        Key = key;

        _value = new BehaviorSubject<string?>(TranslationProvider.ReadTranslation(assembly, sourceFile, key, TranslationProvider.Culture));

        TranslationProvider.OnCultureChanged += (sender, info) =>
        {
            _value.OnNext(TranslationProvider.ReadTranslation(assembly, sourceFile, key, info));
        };
    }
}
