using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyfinClient.Models;
using JellyfinClient.Services;
using JellyfinXbox.Services;
using JellyfinXbox.Views;

namespace JellyfinXbox.ViewModels;

public partial class HomeViewModel : ObservableObject, IDisposable
{
    private readonly JellyfinApiClient _api;
    private readonly NavigationService _nav;
    private readonly Action _authChangedHandler;

    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial bool HasContinueWatching { get; set; }
    [ObservableProperty] public partial bool HasNextUp { get; set; }
    [ObservableProperty] public partial string DisplayName { get; set; }
    [ObservableProperty] public partial string ErrorMessage { get; set; }

    public ObservableCollection<ViewItem> Libraries { get; } = new();
    public ObservableCollection<BaseItemDto> ContinueWatching { get; } = new();
    public ObservableCollection<BaseItemDto> NextUp { get; } = new();
    public ObservableCollection<BaseItemDto> LatestMovies { get; } = new();
    public ObservableCollection<BaseItemDto> LatestShows { get; } = new();

    public HomeViewModel(JellyfinApiClient api, NavigationService nav)
    {
        _api = api;
        _nav = nav;

        _authChangedHandler = () =>
        {
            if (!_api.IsAuthenticated)
                _nav.NavigateTo(typeof(LoginPage));
        };
        _api.OnAuthenticationChanged += _authChangedHandler;
    }

    [RelayCommand]
    private async Task LoadDataAsync()
    {
        if (!_api.IsAuthenticated) return;

        IsLoading = true;
        ErrorMessage = "";
        DisplayName = _api.CurrentUser?.Name ?? "";

        try
        {
            // Load all sections in parallel
            var resumeTask = _api.GetResumeItemsAsync(12);
            var nextUpTask = _api.GetNextUpAsync(limit: 12);
            var viewsTask = _api.GetViewsAsync();

            await Task.WhenAll(resumeTask, nextUpTask, viewsTask);

            // Continue watching
            ContinueWatching.Clear();
            foreach (var item in resumeTask.Result.Items) ContinueWatching.Add(item);
            HasContinueWatching = ContinueWatching.Count > 0;

            // Next up
            NextUp.Clear();
            foreach (var item in nextUpTask.Result.Items) NextUp.Add(item);
            HasNextUp = NextUp.Count > 0;

            // Libraries
            var views = viewsTask.Result;
            Libraries.Clear();
            foreach (var view in views) Libraries.Add(view);

            // Latest content from libraries
            LatestMovies.Clear();
            LatestShows.Clear();

            var movieLib = views.FirstOrDefault(v => v.CollectionType == "movies");
            var showLib = views.FirstOrDefault(v => v.CollectionType == "tvshows");

            var movieTask = movieLib != null ? _api.GetItemsAsync(movieLib.Id, sortBy: "DateCreated", sortOrder: "Descending", limit: 12, includeItemTypes: "Movie") : Task.FromResult(new ItemsResult());
            var showTask = showLib != null ? _api.GetItemsAsync(showLib.Id, sortBy: "DateCreated", sortOrder: "Descending", limit: 12, includeItemTypes: "Series") : Task.FromResult(new ItemsResult());

            await Task.WhenAll(movieTask, showTask);

            foreach (var item in movieTask.Result.Items) LatestMovies.Add(item);
            foreach (var item in showTask.Result.Items) LatestShows.Add(item);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private Task RetryAsync() => LoadDataAsync();

    [RelayCommand]
    private void NavigateToItem(BaseItemDto item)
    {
        _nav.NavigateTo(typeof(MediaDetailPage), item.Id);
    }

    [RelayCommand]
    private void NavigateToLibrary(ViewItem item)
    {
        _nav.NavigateTo(typeof(LibraryPage), (item.Id, item.Name));
    }

    [RelayCommand]
    private void NavigateToSearch()
    {
        _nav.NavigateTo(typeof(SearchPage));
    }

    public void Dispose()
    {
        _api.OnAuthenticationChanged -= _authChangedHandler;
    }
}
