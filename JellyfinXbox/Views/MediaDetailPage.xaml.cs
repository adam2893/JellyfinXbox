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
            BackdropImage.Source = CreateBitmap(ViewModel.BackdropUrl);
            PosterImage.Source = CreateBitmap(ViewModel.PosterUrl);
        }
    }

    private static Windows.UI.Xaml.Media.Imaging.BitmapImage? CreateBitmap(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        try
        {
            return new Windows.UI.Xaml.Media.Imaging.BitmapImage(uri);
        }
        catch
        {
            return null;
        }
    }

    private void Episode_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is JellyfinClient.Models.BaseItemDto episode)
            ViewModel.PlayItemCommand.Execute(episode);
    }

    private void SimilarItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: JellyfinClient.Models.BaseItemDto item })
        {
            ViewModel.NavigateToItemCommand.Execute(item);
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e) => ViewModel.GoBackCommand.Execute(null);
}
