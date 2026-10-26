using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using JellyfinXbox.Services;
using JellyfinXbox.ViewModels;

namespace JellyfinXbox.Views;

public sealed partial class MediaDetailPage : Page
{
    private bool _isActive;
    private string? _itemId;

    public MediaDetailViewModel ViewModel { get; }

    public MediaDetailPage()
    {
        ViewModel = App.Services.GetRequiredService<MediaDetailViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _isActive = true;
        _itemId = e.Parameter as string;

        if (!string.IsNullOrWhiteSpace(_itemId))
        {
            try
            {
                await ViewModel.LoadAsync(_itemId);
            }
            catch (Exception ex)
            {
                StartupDiagnostics.Exception("media-detail.load", ex);
                ViewModel.StatusMessage = "We couldn't open this item. Try again.";
                ViewModel.HasError = true;
            }

            if (_isActive && string.Equals(_itemId, e.Parameter as string, StringComparison.Ordinal))
            {
                // Load only after the data request has completed, and only if
                // this page is still the active destination.
                BackdropImage.Source = CreateBitmap(ViewModel.BackdropUrl);
                PosterImage.Source = CreateBitmap(ViewModel.PosterUrl);
            }
        }
        else
        {
            ViewModel.StatusMessage = "This item could not be opened.";
            ViewModel.HasError = true;
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _isActive = false;
        ViewModel.InvalidateLoad();
        BackdropImage.Source = null;
        PosterImage.Source = null;
        base.OnNavigatedFrom(e);
    }

    private static Windows.UI.Xaml.Media.Imaging.BitmapImage? CreateBitmap(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        try
        {
            return new Windows.UI.Xaml.Media.Imaging.BitmapImage { UriSource = uri };
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
