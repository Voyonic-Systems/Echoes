<p align="center">
  <img src="/img/icon.webp" width="100"/>
  <h1 align="center">Echoes</h1>
  <p align="center">
    Simple type safe translations for Avalonia and MAUI (and anything else .NET)
  </p>
</p>

<p align="center">
  <img src="/img/editor-demo.png" width="80%"/>
</p>

### Features
- Change language at runtime (obviously - but hard with ResX)
- Translation keys are generated at compile time. Missing keys (from the invariant) will show up as compiler errors.
- [Avalonia Markup extension](https://docs.avaloniaui.net/docs/concepts/markupextensions) and [MAUI Markup extension](https://learn.microsoft.com/en-us/dotnet/maui/xaml/fundamentals/markup-extensions) for simple usage in design-time
- Simple translation file format based on [TOML](https://toml.io/en/)
- Multiple translation files, so you can split translations by feature, ..
- Supports [ISO 639-1 (en, de)](https://en.wikipedia.org/wiki/ISO_639-1) and [RRC 5646 (en-US, en-GB, de-DE)](https://www.rfc-editor.org/rfc/rfc5646.html) translation identifiers
- Autocomplete of translation keys
  <img width="952" height="151" alt="Screenshot 2025-08-05 at 10 03 21" src="https://github.com/user-attachments/assets/98d8aa66-50bc-4778-928d-b93d1da579ae" />


### Getting Started

It's best to take a look at the [Avalonia Sample Project](https://github.com/Voyonic-Systems/Echoes/tree/main/src/Echoes.SampleApp) or [MAUI Sample Project](https://github.com/Voyonic-Systems/Echoes/tree/main/src/Echoes.SampleApp.MAUI)

Add references to the following packages:
```xml
<PackageReference Include="Echoes" Version=".."/>
<PackageReference Include="Echoes.Generator" Version=".."/>
```

For Avalonia XAML integration, add this reference:
```xml
<PackageReference Include="Echoes.Avalonia" Version=".."/>
```

For MAUI XAML integration, add this reference:
```xml
<PackageReference Include="Echoes.MAUI" Version=".."/>
```

Specify translations files (Embedded Resources, Source Generator)
```xml
<ItemGroup>
    <!-- Include all .toml files as embedded resources (so we can dynamically load them at runtime) -->
    <EmbeddedResource Include="**\*.toml" />

    <!-- Specify invariant files that are fed into the generator (Echoes.Generator) -->
    <AdditionalFiles Include="Translations\Strings.toml" />
</ItemGroup>
```

> [!CAUTION] 
> You currently have to place your translation (.toml) files and the generated code in a **separate project**. This is because Avalonia also generates
> code using their XAML compiler. In order for the xaml compiler to see your translations you need to put them in a different project. Otherwise you'll get a
> compiler error.


### Translation Files
Translations are loaded from `.toml` files. The invariant file is **special** as its structure includes configuration data. 
Language files are identified by `_{lang}.toml` or `_{lang-culture}.toml`  postfix. 

```
Strings.toml
Strings_de.toml
Strings_es.toml
Strings_de-AT.toml
```

You can split translations in multiple toml files. 

```
FeatureA.toml
FeatureA_de.toml
FeatureA_es.toml

FeatureB.toml
FeatureB_de.toml
FeatureB_es.toml
```

### File Format
#### Example: Strings.toml
```toml
[echoes_config]
generated_class_name = "Strings"
generated_namespace = "Echoes.SampleApp.Translations"

[translations]
hello_world = 'Hello World'
greeting = 'Hello {0}, how are you?'
```

#### Example: Strings_de.toml
```toml
hello_world = 'Hallo Welt'
greeting = 'Hallo {0}, wie geht es dir?'
```

### Is this library stable?
No, it's currently in preview. See the version number.

### Why is it named "Echoes"?
The library is named after the Pink Floyd song [Echoes](https://en.wikipedia.org/wiki/Echoes_(Pink_Floyd_song)).
