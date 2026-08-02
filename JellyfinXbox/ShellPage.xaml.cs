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

    public ShellPage()
    {
        InitializeComponent();
        _nav = App.Services.GetRequiredService<NavigationService>();
        _nav.Initialize(ContentFrame);

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
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
