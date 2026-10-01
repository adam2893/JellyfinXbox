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

    public ObservableCollection<BaseItemDto> Items { get; } = new();

    public LibraryViewModel(JellyfinApiClient api, NavigationService nav)
    {
        _api = api;
        _nav = nav;
        LibraryName = "";
        LibraryId = "";
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

    public async Task LoadCollectionsAsync()
    {
        IsLoading = true;
        Items.Clear();

        var result = await _api.GetCollectionsAsync(limit: 100);
        foreach (var item in result.Items) Items.Add(item);

        IsLoading = false;
    }

    public async Task LoadPlaylistsAsync()
    {
        IsLoading = true;
        Items.Clear();

        var result = await _api.GetPlaylistsAsync(limit: 100);
        foreach (var item in result.Items) Items.Add(item);

        IsLoading = false;
    }
}
