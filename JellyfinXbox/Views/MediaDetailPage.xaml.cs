using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using JellyfinXbox.ViewModels;

namespace JellyfinXbox.Views;

public sealed partial class MediaDetailPage : Page
{
    public MediaDetailViewModel ViewModel { get; }

    public MediaDetailPage()
    {
        ViewModel = App.Services.GetRequiredService<MediaDetailViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter is string itemId)
        {
            await ViewModel.LoadAsync(itemId);

            // Load images
            var api = App.Services.GetRequiredService<JellyfinClient.Services.JellyfinApiClient>();

            if (!string.IsNullOrEmpty(ViewModel.BackdropUrl))
                BackdropImage.Source = new Windows.UI.Xaml.Media.Imaging.BitmapImage(
                    new Uri($"{api.ServerUrl}{ViewModel.BackdropUrl}"));

            if (!string.IsNullOrEmpty(ViewModel.PosterUrl))
                PosterImage.Source = new Windows.UI.Xaml.Media.Imaging.BitmapImage(
                    new Uri($"{api.ServerUrl}{ViewModel.PosterUrl}"));
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e) => ViewModel.GoBackCommand.Execute(null);
}
