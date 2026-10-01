using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyfinClient.Models;
using JellyfinClient.Services;
using JellyfinXbox.Services;
using JellyfinXbox.Views;

namespace JellyfinXbox.ViewModels;

public partial class SearchViewModel : ObservableObject, IDisposable
{
    private readonly JellyfinApiClient _api;
    private readonly NavigationService _nav;
    private CancellationTokenSource? _searchCts;

    [ObservableProperty] public partial string Query { get; set; }
    [ObservableProperty] public partial bool IsSearching { get; set; }
    [ObservableProperty] public partial bool HasResults { get; set; }

    public ObservableCollection<BaseItemDto> Results { get; } = new();

    public SearchViewModel(JellyfinApiClient api, NavigationService nav)
    {
        _api = api;
        _nav = nav;
        Query = "";
    }

    partial void OnQueryChanged(string value)
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();

        if (string.IsNullOrWhiteSpace(value) || value.Length < 2)
        {
            Results.Clear();
            HasResults = false;
            IsSearching = false;
            return;
        }

        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;
        _ = DebouncedSearchAsync(value, token);
    }

    private async Task DebouncedSearchAsync(string query, CancellationToken token)
    {
        try
        {
            await Task.Delay(350, token);
            if (token.IsCancellationRequested) return;

            IsSearching = true;
            var result = await _api.SearchAsync(query);
            if (token.IsCancellationRequested) return;

            Results.Clear();
            foreach (var item in result.Items) Results.Add(item);
            HasResults = Results.Count > 0;
        }
        catch (OperationCanceledException)
        {
            // Cancelled due to new keystroke
        }
        finally
        {
            if (!token.IsCancellationRequested)
                IsSearching = false;
        }
    }

    [RelayCommand]
    private async Task SearchAsync(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        try
        {
            IsSearching = true;
            var result = await _api.SearchAsync(query);
            if (token.IsCancellationRequested) return;

            Results.Clear();
            foreach (var item in result.Items) Results.Add(item);
            HasResults = Results.Count > 0;
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (!token.IsCancellationRequested)
                IsSearching = false;
        }
    }

    [RelayCommand]
    private void NavigateToItem(BaseItemDto item) =>
        _nav.NavigateTo(typeof(MediaDetailPage), item.Id);

    public void Dispose()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
    }
}
