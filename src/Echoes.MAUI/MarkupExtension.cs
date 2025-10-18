namespace Echoes;

[ContentProperty(nameof(Unit))]
[AcceptEmptyServiceProvider]
public sealed class Translate : BindableObject, IMarkupExtension<BindingBase>
{
    // BindableProperty for the Unit
    public static readonly BindableProperty UnitProperty =
        BindableProperty.Create(nameof(Unit), typeof(TranslationUnit), typeof(Translate), null, propertyChanged: OnUnitChanged);

    public TranslationUnit Unit
    {
        get => (TranslationUnit)GetValue(UnitProperty);
        set => SetValue(UnitProperty, value);
    }

    // BindableProperty for the Value (the translated text)
    public static readonly BindableProperty ValueProperty =
        BindableProperty.Create(nameof(Value), typeof(string), typeof(Translate), string.Empty);

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    // Constructor
    public Translate()
    {
        // Subscribe to the external service culture change event.
        // This subscription should be here to ensure it happens for every instance.
        TranslationProvider.OnCultureChanged += TranslationProvider_OnCultureChanged;
    }

    // Event handler for the external service
    private void TranslationProvider_OnCultureChanged(object? sender, System.Globalization.CultureInfo e)
    {
        // Update the Value property on the UI thread.
        // This is the key to triggering the binding update.
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Unit != null)
                Value = Unit.CurrentValue;
        });
    }

    // The callback for when the Unit property changes
    private static void OnUnitChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is Translate extension && newValue is TranslationUnit newUnit)
        {
            // Initialize the Value property with the current translated string.
            extension.Value = newUnit.CurrentValue;
        }
    }

    BindingBase IMarkupExtension<BindingBase>.ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding(nameof(Value), source: this);
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
    {
        return new Binding(nameof(Value), source: this);
    }
}