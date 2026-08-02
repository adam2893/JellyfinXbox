using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using JellyfinClient.Services;
using JellyfinXbox.Services;
using JellyfinXbox.Views;

namespace JellyfinXbox;

public sealed partial class ShellPage : Page
{
    private readonly NavigationService _nav;
    private readonly GamepadService _gamepad;

    public ShellPage()
    {
        InitializeComponent();
        _nav = App.Services.GetRequiredService<NavigationService>();
        _gamepad = App.Services.GetRequiredService<GamepadService>();
        _nav.Initialize(ContentFrame);

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 10-foot UI: global controller shortcuts. D-pad/left-stick focus
        // navigation and A/B activation are handled natively by XAML on
        // Xbox, so only the non-native buttons are mapped here. The shell
        // lives for the app's lifetime, so subscribe once, no unsubscribe.
        _gamepad.Start();
        _gamepad.OnMenuPressed += OnMenuPressed;  // ☰ → Settings
        _gamepad.OnViewPressed += OnViewPressed;  // ⊞ → Quick Connect

        var api = App.Services.GetRequiredService<JellyfinApiClient>();

        if (!api.IsAuthenticated)
        {
            _nav.NavigateTo(typeof(LoginPage));
        }
        else
        {
            NavHome.IsChecked = true;
            _nav.NavigateTo(typeof(HomePage));
        }
    }

    private void OnMenuPressed() => NavButton_Click(NavSettings, null);
    private void OnViewPressed() => NavButton_Click(NavQuickConnect, null);

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb)
        {
            var tag = rb.Tag?.ToString();
            switch (tag)
            {
                case "home":
                    _nav.NavigateTo(typeof(HomePage));
                    break;
                case "search":
                    _nav.NavigateTo(typeof(SearchPage));
                    break;
                case "quickconnect":
                    _nav.NavigateTo(typeof(QuickConnectPage));
                    break;
                case "settings":
                    _nav.NavigateTo(typeof(SettingsPage));
                    break;
            }
        }
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        if (e.SourcePageType != typeof(HomePage) &&
            e.SourcePageType != typeof(SearchPage) &&
            e.SourcePageType != typeof(QuickConnectPage) &&
            e.SourcePageType != typeof(SettingsPage))
        {
            NavHome.IsChecked = false;
            NavSearch.IsChecked = false;
            NavQuickConnect.IsChecked = false;
            NavSettings.IsChecked = false;
        }
    }
}
