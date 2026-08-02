using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyfinClient.Models;
using JellyfinClient.Services;
using JellyfinXbox.Services;
using JellyfinXbox.Views;
using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace JellyfinXbox.Views;

public sealed partial class LibraryPage : Page
{
    private readonly JellyfinApiClient _api;
    private readonly NavigationService _nav;

    public LibraryViewModel ViewModel { get; }

    public LibraryPage()
    {
        _api = App.Services.GetRequiredService<JellyfinApiClient>();
        _nav = App.Services.GetRequiredService<NavigationService>();
        ViewModel = new LibraryViewModel(_api, _nav);
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
    }

    private void Item_Click(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is BaseItemDto item)
            _nav.NavigateTo(typeof(MediaDetailPage), item.Id);
    }

    private void Back_Click(object sender, RoutedEventArgs e) => _nav.GoBack();
    private void FilterAll_Click(object sender, RoutedEventArgs e) => _ = ViewModel.LoadAsync(ViewModel.LibraryId);
    private void FilterUnwatched_Click(object sender, RoutedEventArgs e) => _ = ViewModel.LoadAsync(ViewModel.LibraryId, "IsUnplayed");
    private void FilterFavorites_Click(object sender, RoutedEventArgs e) => _ = ViewModel.LoadAsync(ViewModel.LibraryId, "IsFavorite");
}

public partial class LibraryViewModel : ObservableObject
{
    private readonly JellyfinApiClient _api;
    private readonly NavigationService _nav;

    [ObservableProperty] public partial string LibraryName { get; set; } = "";
    [ObservableProperty] public partial string LibraryId { get; set; } = "";
    [ObservableProperty] public partial bool IsLoading { get; set; }

    public ObservableCollection<BaseItemDto> Items { get; } = new();

    public LibraryViewModel(JellyfinApiClient api, NavigationService nav)
    {
        _api = api;
        _nav = nav;
    }

    public async Task LoadAsync(string libraryId, string? filter = null)
    {
        LibraryId = libraryId;
        IsLoading = true;
        Items.Clear();

        var result = await _api.GetItemsAsync(libraryId, sortBy: "SortName", sortOrder: "Ascending", limit: 100, filters: filter);
        foreach (var item in result.Items) Items.Add(item);

        IsLoading = false;
    }
}
