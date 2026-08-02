using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyfinClient.Models;
using JellyfinClient.Services;
using JellyfinXbox.Views;

namespace JellyfinXbox.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly JellyfinApiClient _api;
    private readonly NavigationService _nav;

    [ObservableProperty] private string _query = "";
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private bool _hasResults;

    public ObservableCollection<BaseItemDto> Results { get; } = new();

    public SearchViewModel(JellyfinApiClient api, NavigationService nav)
    {
        _api = api;
        _nav = nav;
    }

    partial void OnQueryChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 2)
        {
            Results.Clear();
            HasResults = false;
            return;
        }

        SearchCommand.Execute(value);
    }

    [RelayCommand]
    private async Task SearchAsync(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        IsSearching = true;
        var result = await _api.SearchAsync(query);
        Results.Clear();
        foreach (var item in result.Items) Results.Add(item);
        HasResults = Results.Count > 0;
        IsSearching = false;
    }

    [RelayCommand]
    private void NavigateToItem(BaseItemDto item) =>
        _nav.NavigateTo(typeof(MediaDetailPage), item.Id);
}
