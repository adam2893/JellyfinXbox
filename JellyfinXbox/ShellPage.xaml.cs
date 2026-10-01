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

        // Remote control casting ("Play To Xbox")
        api.OnPlayCommand += data =>
        {
            if (data.ItemIds != null && data.ItemIds.Count > 0)
            {
                _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    if (ContentFrame.CurrentSourcePageType != typeof(PlayerPage))
                    {
                        var firstId = data.ItemIds[0];
                        long? startPos = data.StartPositionTicks;
                        _nav.NavigateTo(typeof(PlayerPage), (firstId, startPos));
                    }
                });
            }
        };

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

    private void OnMenuPressed()
    {
        if (ContentFrame.CurrentSourcePageType == typeof(PlayerPage) ||
            ContentFrame.CurrentSourcePageType == typeof(LoginPage))
            return;

        if (ContentFrame.CurrentSourcePageType != typeof(SettingsPage))
            NavButton_Click(NavSettings, null!);
    }

    private void OnViewPressed()
    {
        if (ContentFrame.CurrentSourcePageType == typeof(PlayerPage) ||
            ContentFrame.CurrentSourcePageType == typeof(LoginPage))
            return;

        if (ContentFrame.CurrentSourcePageType != typeof(QuickConnectPage))
            NavButton_Click(NavQuickConnect, null!);
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb)
        {
            var tag = rb.Tag?.ToString();
            Type? targetType = tag switch
            {
                "home" => typeof(HomePage),
                "search" => typeof(SearchPage),
                "quickconnect" => typeof(QuickConnectPage),
                "settings" => typeof(SettingsPage),
                _ => null
            };

            if (targetType != null && ContentFrame.CurrentSourcePageType != targetType)
            {
                _nav.NavigateTo(targetType);
            }
        }
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        bool hideRail = e.SourcePageType == typeof(PlayerPage) || e.SourcePageType == typeof(LoginPage);
        SideNavRail.Visibility = hideRail ? Visibility.Collapsed : Visibility.Visible;

        NavHome.IsChecked = e.SourcePageType == typeof(HomePage);
        NavSearch.IsChecked = e.SourcePageType == typeof(SearchPage);
        NavQuickConnect.IsChecked = e.SourcePageType == typeof(QuickConnectPage);
        NavSettings.IsChecked = e.SourcePageType == typeof(SettingsPage);
    }
}
