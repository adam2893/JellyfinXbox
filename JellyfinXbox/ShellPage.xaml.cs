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
    private bool _loaded;
    private NavigationService _nav = null!;
    private GamepadService _gamepad = null!;

    public ShellPage()
    {
        try
        {
            StartupDiagnostics.Stage("shell.initialize-component");
            InitializeComponent();
            StartupDiagnostics.Stage("shell.service-resolution");
            _nav = App.Services.GetRequiredService<NavigationService>();
            _gamepad = App.Services.GetRequiredService<GamepadService>();
            _nav.Initialize(ContentFrame);
        }
        catch (System.Exception ex) { App.ShowRecovery("SHELL-INIT", ex); throw; }

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        try { OnLoadedCore(); }
        catch (System.Exception ex) { App.ShowRecovery("SHELL-LOADED", ex); }
    }

    private void OnLoadedCore()
    {
        StartupDiagnostics.Stage("shell.loaded");
        // 10-foot UI: global controller shortcuts. D-pad/left-stick focus
        // navigation and A/B activation are handled natively by XAML on
        // Xbox, so only the non-native buttons are mapped here. The shell
        // lives for the app's lifetime, so subscribe once, no unsubscribe.
        StartupDiagnostics.Stage("shell.gamepad-start");
        _gamepad.Start();
        _gamepad.OnMenuPressed += OnMenuPressed;  // ☰ → Settings
        _gamepad.OnViewPressed += OnViewPressed;  // ⊞ → Quick Connect

        StartupDiagnostics.Stage("shell.api-resolution");
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
            StartupDiagnostics.Stage("shell.login-navigation"); _nav.NavigateTo(typeof(LoginPage));
        }
        else
        {
            NavHome.IsChecked = true;
            StartupDiagnostics.Stage("shell.home-navigation"); _nav.NavigateTo(typeof(HomePage));
        }
        StartupDiagnostics.Stage("shell.ready");
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
                "collections" or "playlists" or "favorites" => typeof(LibraryPage),
                "quickconnect" => typeof(QuickConnectPage),
                "settings" => typeof(SettingsPage),
                _ => null
            };

            if (targetType != null)
            {
                if (tag is "collections" or "playlists" or "favorites")
                    _nav.NavigateTo(targetType, tag);
                else _nav.NavigateTo(targetType);
            }
        }
    }

    private void ContentFrame_Navigated(object sender, NavigationEventArgs e)
    {
        bool hideRail = e.SourcePageType == typeof(PlayerPage) ||
                        e.SourcePageType == typeof(LoginPage) ||
                        e.SourcePageType == typeof(MediaDetailPage);
        SideNavRail.Visibility = hideRail ? Visibility.Collapsed : Visibility.Visible;

        NavHome.IsChecked = e.SourcePageType == typeof(HomePage);
            NavSearch.IsChecked = e.SourcePageType == typeof(SearchPage);
            NavCollections.IsChecked = false;
            NavPlaylists.IsChecked = false;
            NavFavorites.IsChecked = false;
        NavQuickConnect.IsChecked = e.SourcePageType == typeof(QuickConnectPage);
        NavSettings.IsChecked = e.SourcePageType == typeof(SettingsPage);
    }
}
