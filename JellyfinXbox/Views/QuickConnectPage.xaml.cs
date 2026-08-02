using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using JellyfinXbox.ViewModels;

namespace JellyfinXbox.Views;

public sealed partial class QuickConnectPage : Page
{
    public QuickConnectViewModel ViewModel { get; }

    public QuickConnectPage()
    {
        ViewModel = App.Services.GetRequiredService<QuickConnectViewModel>();
        InitializeComponent();

        var dispatcher = Windows.System.DispatcherQueue.GetForCurrentThread();

        ViewModel.PropertyChanged += (s, e) =>
        {
            dispatcher?.TryEnqueue(() =>
            {
                bool isActive = ViewModel.IsActive;
                bool isSuccess = ViewModel.IsSuccess;
                bool isError = ViewModel.IsError;

                StartPanel.Visibility = (!isActive && !isSuccess && !isError) ? Visibility.Visible : Visibility.Collapsed;
                ConnectingPanel.Visibility = isActive ? Visibility.Visible : Visibility.Collapsed;
                SuccessPanel.Visibility = isSuccess ? Visibility.Visible : Visibility.Collapsed;
                ErrorPanel.Visibility = isError ? Visibility.Visible : Visibility.Collapsed;
            });
        };
    }

    private void Back_Click(object sender, RoutedEventArgs e) => ViewModel.GoBackCommand.Execute(null);
}
