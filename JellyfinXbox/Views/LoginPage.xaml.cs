using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using JellyfinClient.Models;
using JellyfinXbox.Services;
using JellyfinXbox.ViewModels;
using JellyfinXbox.Views;

namespace JellyfinXbox.Views;

public sealed partial class LoginPage : Page
{
    public LoginViewModel ViewModel { get; }
    private readonly NavigationService _nav;

    public LoginPage()
    {
        ViewModel = App.Services.GetRequiredService<LoginViewModel>();
        _nav = App.Services.GetRequiredService<NavigationService>();
        InitializeComponent();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.ShowPasswordField) && ViewModel.ShowPasswordField)
        {
            _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                UsernameBox.Focus(FocusState.Programmatic);
            });
        }
    }

    private void ServerUrlBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            ViewModel.ConnectCommand.Execute(null);
    }

    private void PasswordBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
            ViewModel.LoginCommand.Execute(null);
    }

    private void UserListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is User user)
            ViewModel.SelectUserCommand.Execute(user);
    }

    private void QuickConnect_Click(object sender, RoutedEventArgs e)
    {
        // Navigate to Quick Connect (shell will handle it)
        _nav.NavigateTo(typeof(QuickConnectPage));
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        ViewModel.Dispose();
    }
}
