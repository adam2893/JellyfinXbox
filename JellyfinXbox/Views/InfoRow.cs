using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace JellyfinXbox.Views;

/// <summary>
/// Read-only key/value row used on the Settings page server info section.
/// Styled via the <c>InfoRow</c> ControlTemplate defined in SettingsPage.xaml.
/// </summary>
public class InfoRow : Control
{
    public static readonly DependencyProperty LabelProperty =
        DependencyProperty.Register(nameof(Label), typeof(string), typeof(InfoRow), new PropertyMetadata(""));

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(InfoRow), new PropertyMetadata(""));

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public InfoRow()
    {
        DefaultStyleKey = typeof(InfoRow);
    }
}
