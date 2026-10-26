using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyfinClient.Models;
using JellyfinClient.Services;
using JellyfinXbox.Services;
using JellyfinXbox.Views;

namespace JellyfinXbox.ViewModels;

public partial class MediaDetailViewModel : ObservableObject
{
    private readonly JellyfinApiClient _api;
    private readonly NavigationService _nav;

    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial bool IsFavorite { get; set; }
    [ObservableProperty] public partial bool IsUpdatingFavorite { get; set; }
    [ObservableProperty] public partial string FavoriteLabel { get; set; } = "Add to favorites";
    [ObservableProperty] public partial string StatusMessage { get; set; } = "";
    [ObservableProperty] public partial bool HasError { get; set; }
    [ObservableProperty] public partial string ResumeLabel { get; set; } = "Play";
    [ObservableProperty] public partial bool IsSeries { get; set; }
    [ObservableProperty] public partial bool HasSimilarItems { get; set; }
    [ObservableProperty] public partial bool HasTrailer { get; set; }
    [ObservableProperty] public partial BaseItemDto? Item { get; set; }
    [ObservableProperty] public partial string Title { get; set; }
    [ObservableProperty] public partial string Subtitle { get; set; }
    [ObservableProperty] public partial string? Description { get; set; }
    [ObservableProperty] public partial string? BackdropUrl { get; set; }
    [ObservableProperty] public partial string? PosterUrl { get; set; }
    [ObservableProperty] public partial string? CommunityRating { get; set; }
    [ObservableProperty] public partial string? Runtime { get; set; }
    [ObservableProperty] public partial string? OfficialRating { get; set; }
    [ObservableProperty] public partial int SelectedSeasonIndex { get; set; }

    private string? _trailerItemId;
    private string? _loadedItemId;
    private long _loadGeneration;

    public ObservableCollection<BaseItemDto> Seasons { get; } = new();
    public ObservableCollection<BaseItemDto> Episodes { get; } = new();
    public ObservableCollection<BaseItemPerson> Cast { get; } = new();
    public ObservableCollection<string> Genres { get; } = new();
    public ObservableCollection<BaseItemDto> SimilarItems { get; } = new();

    public MediaDetailViewModel(JellyfinApiClient api, NavigationService nav)
    {
        _api = api;
        _nav = nav;
        Title = "";
        Subtitle = "";
    }

    public async Task LoadAsync(string itemId)
    {
        var generation = Interlocked.Increment(ref _loadGeneration);
        IsLoading = true;
        StatusMessage = "";
        HasError = false;
        _loadedItemId = itemId;
        Item = null;
        Title = "";
        Subtitle = "";
        Description = null;
        BackdropUrl = null;
        PosterUrl = null;
        CommunityRating = null;
        Runtime = null;
        OfficialRating = null;
        IsFavorite = false;
        FavoriteLabel = "Add to favorites";
        ResumeLabel = "Play";
        HasTrailer = false;
        _trailerItemId = null;
        IsSeries = false;
        Seasons.Clear();
        Episodes.Clear();
        Cast.Clear();
        Genres.Clear();
        SimilarItems.Clear();
        HasSimilarItems = false;

        var (item, error) = await _api.GetItemResultAsync(itemId);
        if (generation != _loadGeneration) return;
        if (item == null)
        {
            StatusMessage = error == null ? "This item is unavailable. Try again later." : $"We couldn't load this item. Check your connection and try again.";
            HasError = true;
            IsLoading = false;
            return;
        }

        try
        {
        Item = item;
        item.BackdropImageTags ??= new();
        item.Genres ??= new();
        item.People ??= new();
        item.RemoteTrailers ??= new();
        item.UserData ??= new();
        IsFavorite = item.UserData?.IsFavorite == true;
        FavoriteLabel = IsFavorite ? "Remove from favorites" : "Add to favorites";
        var ticks = item.UserData?.PlaybackPositionTicks ?? 0;
        ResumeLabel = ticks > 0 ? $"Resume at {TimeSpan.FromTicks(ticks):h\\:mm\\:ss}" : "Play";
        Title = item.Name;
        Description = item.Overview;

        if (item.ProductionYear.HasValue)
            Subtitle = item.ProductionYear.Value.ToString();
        if (!string.IsNullOrEmpty(item.OfficialRating))
            OfficialRating = item.OfficialRating;

        var rt = _api.GetRuntime(item);
        Runtime = _api.FormatRuntime(rt);
        CommunityRating = item.CommunityRating?.ToString("F1");

        if (item.CommunityRating.HasValue)
            CommunityRating = $"★ {item.CommunityRating.Value:F1}";
        else CommunityRating = null;

        // Images
        PosterUrl = _api.GetItemImageUrl(item, "Primary", 720);
        var backdropId = item.BackdropImageTags?.Count > 0 ? item.Id : item.ParentBackdropItemId;
        if (!string.IsNullOrEmpty(backdropId))
            BackdropUrl = _api.GetItemImageUrl(item, "Backdrop", 1920);

        // Genres
        Genres.Clear();
        foreach (var g in item.Genres) Genres.Add(g);

        // Cast
        Cast.Clear();
        foreach (var p in item.People.Where(p => p.Type == "Actor").Take(12))
            Cast.Add(p);

        // Series handling
        if (item.Type == "Series")
        {
            IsSeries = true;
            await LoadSeasonsAsync(item.Id);
            if (generation != _loadGeneration) return;
        }
        else if (item.Type == "Episode" && !string.IsNullOrEmpty(item.SeriesId))
        {
            Subtitle = $"S{item.ParentIndexNumber:D2}E{item.IndexNumber:D2}";
            if (!string.IsNullOrEmpty(item.SeriesName))
                Title = item.SeriesName;
        }

        // Trailers
        var localTrailers = await _api.GetLocalTrailersAsync(item.Id);
        if (generation != _loadGeneration) return;
        if (localTrailers.Count > 0)
        {
            _trailerItemId = localTrailers[0].Id;
            HasTrailer = true;
        }
        else if (item.RemoteTrailers?.Count > 0)
        {
            _trailerItemId = null;
            HasTrailer = true;
        }
        else
        {
            _trailerItemId = null;
            HasTrailer = false;
        }

        // Similar items
        SimilarItems.Clear();
        var similar = await _api.GetSimilarItemsAsync(item.Id, 12);
        if (generation != _loadGeneration) return;
        foreach (var sim in similar.Items) SimilarItems.Add(sim);
        HasSimilarItems = SimilarItems.Count > 0;

        }
        catch (Exception)
        {
            if (generation == _loadGeneration)
            {
                StatusMessage = "Some details could not be loaded. Try again.";
                HasError = true;
            }
        }
        finally
        {
            if (generation == _loadGeneration)
                IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RetryAsync()
    {
        if (!string.IsNullOrEmpty(_loadedItemId)) await LoadAsync(_loadedItemId);
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync()
    {
        if (Item == null || IsUpdatingFavorite) return;
        IsUpdatingFavorite = true;
        StatusMessage = "";
        try
        {
            await _api.ToggleFavoriteAsync(Item.Id, IsFavorite);
            IsFavorite = !IsFavorite;
            if (Item.UserData != null) Item.UserData.IsFavorite = IsFavorite;
            FavoriteLabel = IsFavorite ? "Remove from favorites" : "Add to favorites";
            StatusMessage = IsFavorite ? "Added to favorites." : "Removed from favorites.";
        }
        catch (Exception ex) { StatusMessage = $"Couldn't update favorites: {ex.Message}"; }
        finally { IsUpdatingFavorite = false; }
    }

    private async Task LoadSeasonsAsync(string seriesId)
    {
        var result = await _api.GetSeasonsAsync(seriesId);
        Seasons.Clear();
        foreach (var s in result.Items) Seasons.Add(s);

        if (Seasons.Count > 0)
        {
            SelectedSeasonIndex = -1;
            SelectedSeasonIndex = 0;
        }
    }

    partial void OnSelectedSeasonIndexChanged(int value)
    {
        if (value >= 0 && Seasons.Count > value && Item != null)
            _ = LoadEpisodesAsync(Item.Id, Seasons[value].Id);
    }

    private async Task LoadEpisodesAsync(string seriesId, string seasonId)
    {
        Episodes.Clear();
        var result = await _api.GetEpisodesAsync(seriesId, seasonId);
        foreach (var ep in result.Items) Episodes.Add(ep);
    }

    [RelayCommand]
    private void NavigateToItem(BaseItemDto item)
    {
        _nav.NavigateTo(typeof(MediaDetailPage), item.Id);
    }

    [RelayCommand]
    private void PlayItem(BaseItemDto? item)
    {
        if (item == null) return;
        if (item.Type == "Series" && Episodes.Count > 0)
        {
            _nav.NavigateTo(typeof(PlayerPage), Episodes[0].Id);
            return;
        }
        _nav.NavigateTo(typeof(PlayerPage), item.Id);
    }

    [RelayCommand]
    private void PlayTrailer()
    {
        if (!string.IsNullOrEmpty(_trailerItemId))
        {
            _nav.NavigateTo(typeof(PlayerPage), _trailerItemId);
        }
        else if (Item?.RemoteTrailers?.Count > 0)
        {
            var trailer = Item.RemoteTrailers[0];
            if (!string.IsNullOrEmpty(trailer.Url))
            {
                _ = Windows.System.Launcher.LaunchUriAsync(new Uri(trailer.Url));
            }
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        _nav.GoBack();
    }
}
