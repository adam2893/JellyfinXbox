using JellyfinClient.Models;
using JellyfinClient.Services;
using JellyfinXbox.Services;
using JellyfinXbox.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace JellyfinXbox.Views;

public sealed partial class LibraryPage : Page
{
    private readonly NavigationService _nav;

    public LibraryViewModel ViewModel { get; }

    public LibraryPage()
    {
        _nav = App.Services.GetRequiredService<NavigationService>();
        ViewModel = App.Services.GetRequiredService<LibraryViewModel>();
        InitializeComponent();
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is (string libraryId, string name))
        {
            ViewModel.LibraryName = name;
            await ViewModel.LoadAsync(libraryId);
        }
        else if (e.Parameter is ValueTuple<string, string> tuple)
        {
            ViewModel.LibraryName = tuple.Item2;
            await ViewModel.LoadAsync(tuple.Item1);
        }
    }

    private void Item_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is BaseItemDto item)
        {
            if (item.Type == "BoxSet" || item.Type == "Playlist" || item.Type == "Folder" || item.IsFolder == true)
            {
                _nav.NavigateTo(typeof(LibraryPage), (item.Id, item.Name ?? "Collection"));
            }
            else
            {
                _nav.NavigateTo(typeof(MediaDetailPage), item.Id);
            }
        }
    }

    private void Back_Click(object sender, RoutedEventArgs e) => _nav.GoBack();
    private void FilterAll_Click(object sender, RoutedEventArgs e) => _ = ViewModel.LoadAsync(ViewModel.LibraryId);
    private void FilterUnwatched_Click(object sender, RoutedEventArgs e) => _ = ViewModel.LoadAsync(ViewModel.LibraryId, "IsUnplayed");
    private void FilterFavorites_Click(object sender, RoutedEventArgs e) => _ = ViewModel.LoadAsync(ViewModel.LibraryId, "IsFavorite");
    private void FilterCollections_Click(object sender, RoutedEventArgs e) => _ = ViewModel.LoadCollectionsAsync();
    private void FilterPlaylists_Click(object sender, RoutedEventArgs e) => _ = ViewModel.LoadPlaylistsAsync();
}
