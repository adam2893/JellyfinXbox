using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyfinClient.Models;
using JellyfinClient.Services;
using JellyfinXbox.Services;

namespace JellyfinXbox.ViewModels;

public partial class LibraryViewModel : ObservableObject
{
    private const int PageSize = 60;
    private readonly JellyfinApiClient _api;
    private readonly NavigationService _nav;
    private int _nextIndex;
    private int _generation;
    private string _activeMode = "library";
    private string? _activeFilter;

    [ObservableProperty] public partial string LibraryName { get; set; }
    [ObservableProperty] public partial string LibraryId { get; set; }
    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial string ErrorMessage { get; set; }
    [ObservableProperty] public partial string SortBy { get; set; }
    [ObservableProperty] public partial bool IsEmpty { get; set; }
    [ObservableProperty] public partial bool HasMore { get; set; }

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

    public Task LoadAsync(string libraryId, string? filter = null)
    {
        LibraryId = libraryId;
        _activeMode = "library";
        _activeFilter = filter;
        return ResetAsync();
    }

    public Task LoadFavoritesAsync() { _activeMode = "favorites"; return ResetAsync(); }
    public Task LoadCollectionsAsync() { _activeMode = "collections"; return ResetAsync(); }
    public Task LoadPlaylistsAsync() { _activeMode = "playlists"; return ResetAsync(); }

    private Task ResetAsync()
    {
        ++_generation;
        Items.Clear();
        _nextIndex = 0;
        IsEmpty = false;
        HasMore = true;
        ErrorMessage = "";
        // A previous request may still be in flight; its generation check prevents stale results.
        IsLoading = false;
        return LoadMoreAsync();
    }

    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (IsLoading || !HasMore || !string.IsNullOrEmpty(ErrorMessage)) return;
        var generation = _generation;
        IsLoading = true;
        ErrorMessage = "";
        try
        {
            var result = _activeMode switch
            {
                "favorites" => await _api.GetUserItemsPageAsync(null, true, _nextIndex, PageSize),
                "collections" => await _api.GetUserItemsPageAsync("BoxSet", false, _nextIndex, PageSize),
                "playlists" => await _api.GetUserItemsPageAsync("Playlist", false, _nextIndex, PageSize),
                _ => await _api.GetItemsPageAsync(LibraryId, _nextIndex, PageSize, _activeFilter)
            };
            if (generation != _generation) return;
            foreach (var item in result.Items) Items.Add(item);
            _nextIndex += result.Items.Count;
            HasMore = result.Items.Count > 0 && (result.TotalRecordCount > 0
                ? _nextIndex < result.TotalRecordCount : result.Items.Count == PageSize);
            IsEmpty = Items.Count == 0;
        }
        catch (Exception ex)
        {
            if (generation == _generation) ErrorMessage = $"Couldn't load items: {ex.Message}";
        }
        finally { if (generation == _generation) IsLoading = false; }
    }

    [RelayCommand]
    private Task RetryAsync()
    {
        if (Items.Count == 0) return ResetAsync();
        ErrorMessage = "";
        return LoadMoreAsync();
    }
}
