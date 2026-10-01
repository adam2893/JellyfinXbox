using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyfinClient.Models;
using JellyfinClient.Services;
using JellyfinXbox.Services;

namespace JellyfinXbox.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private readonly JellyfinApiClient _api;
    private readonly NavigationService _nav;

    [ObservableProperty] public partial string LibraryName { get; set; }
    [ObservableProperty] public partial string LibraryId { get; set; }
    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial string ErrorMessage { get; set; }
    [ObservableProperty] public partial string SortBy { get; set; }
    [ObservableProperty] public partial bool IsEmpty { get; set; }
    private string _activeMode = "library";
    private string? _activeFilter;

    public ObservableCollection<BaseItemDto> Items { get; } = new();

    public LibraryViewModel(JellyfinApiClient api, NavigationService nav)
    {
        _api = api;
        _nav = nav;
        LibraryName = "";
        LibraryId = "";
        ErrorMessage = "";
        SortBy = "SortName";
    }

    public async Task LoadAsync(string libraryId, string? filter = null)
    {
        _activeMode = "library";
        _activeFilter = filter;
        LibraryId = libraryId;
        IsLoading = true;
        ErrorMessage = "";
        Items.Clear();
        try
        {
            var result = await _api.GetItemsAsync(libraryId, sortBy: SortBy, sortOrder: "Ascending", limit: 100, filters: filter);
            foreach (var item in result.Items) Items.Add(item);
            IsEmpty = Items.Count == 0;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; IsEmpty = false; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private Task RetryAsync() => _activeMode switch
    {
        "favorites" => LoadFavoritesCoreAsync(),
        "collections" => LoadCollectionsAsync(),
        "playlists" => LoadPlaylistsAsync(),
        _ => LoadAsync(LibraryId, _activeFilter)
    };

    public Task LoadFavoritesAsync() => LoadFavoritesCoreAsync();
    private async Task LoadFavoritesCoreAsync()
    {
        _activeMode = "favorites";
        IsLoading = true; ErrorMessage = ""; Items.Clear();
        try { foreach (var item in (await _api.GetFavoriteItemsAsync()).Items) Items.Add(item); IsEmpty = Items.Count == 0; }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    public async Task LoadCollectionsAsync()
    {
        _activeMode = "collections";
        IsLoading = true;
        ErrorMessage = "";
        Items.Clear();
        try { var result = await _api.GetCollectionsAsync(limit: 100); foreach (var item in result.Items) Items.Add(item); IsEmpty = Items.Count == 0; }
        catch (Exception ex) { ErrorMessage = ex.Message; IsEmpty = false; }
        finally { IsLoading = false; }
    }

    public async Task LoadPlaylistsAsync()
    {
        _activeMode = "playlists";
        IsLoading = true;
        ErrorMessage = "";
        Items.Clear();
        try { var result = await _api.GetPlaylistsAsync(limit: 100); foreach (var item in result.Items) Items.Add(item); IsEmpty = Items.Count == 0; }
        catch (Exception ex) { ErrorMessage = ex.Message; IsEmpty = false; }
        finally { IsLoading = false; }
    }
}
