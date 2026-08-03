using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using JellyfinXbox.Services;
using JellyfinXbox.ViewModels;

namespace JellyfinXbox.Views;

public sealed partial class DebugPage : Page
{
    public DebugViewModel ViewModel { get; }
    private readonly NavigationService _nav;

    public DebugPage()
    {
        ViewModel = App.Services.GetRequiredService<DebugViewModel>();
        _nav = App.Services.GetRequiredService<NavigationService>();
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (ViewModel.Results.Count == 0)
            ViewModel.RunCommand.Execute(null);
    }

    private void Back_Click(object sender, RoutedEventArgs e) => _nav.GoBack();
}
