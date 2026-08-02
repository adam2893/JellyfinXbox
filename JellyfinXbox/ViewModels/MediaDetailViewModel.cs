using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyfinClient.Models;
using JellyfinClient.Services;
using JellyfinXbox.Views;

namespace JellyfinXbox.ViewModels;

public partial class MediaDetailViewModel : ObservableObject
{
    private readonly JellyfinApiClient _api;
    private readonly NavigationService _nav;

    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isSeries;
    [ObservableProperty] private BaseItemDto? _item;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _subtitle = "";
    [ObservableProperty] private string? _description;
    [ObservableProperty] private string? _backdropUrl;
    [ObservableProperty] private string? _posterUrl;
    [ObservableProperty] private string? _communityRating;
    [ObservableProperty] private string? _runtime;
    [ObservableProperty] private string? _officialRating;
    [ObservableProperty] private int _selectedSeasonIndex;

    public ObservableCollection<BaseItemDto> Seasons { get; } = new();
    public ObservableCollection<BaseItemDto> Episodes { get; } = new();
    public ObservableCollection<BaseItemPerson> Cast { get; } = new();
    public ObservableCollection<string> Genres { get; } = new();

    public MediaDetailViewModel(JellyfinApiClient api, NavigationService nav)
    {
        _api = api;
        _nav = nav;
    }

    public async Task LoadAsync(string itemId)
    {
        IsLoading = true;

        var item = await _api.GetItemAsync(itemId);
        if (item == null)
        {
            IsLoading = false;
            return;
        }

        Item = item;
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
        PosterUrl = _api.GetImageUrl(item.Id, "Primary");
        var backdropId = item.BackdropImageTags.Count > 0 ? item.Id : item.ParentBackdropItemId;
        if (!string.IsNullOrEmpty(backdropId))
            BackdropUrl = _api.GetBackdropUrl(backdropId, maxWidth: 1920);

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
        }
        else if (item.Type == "Episode" && !string.IsNullOrEmpty(item.SeriesId))
        {
            Subtitle = $"S{item.ParentIndexNumber:D2}E{item.IndexNumber:D2}";
            if (!string.IsNullOrEmpty(item.SeriesName))
                Title = item.SeriesName;
        }

        IsLoading = false;
    }

    private async Task LoadSeasonsAsync(string seriesId)
    {
        var result = await _api.GetSeasonsAsync(seriesId);
        Seasons.Clear();
        foreach (var s in result.Items) Seasons.Add(s);

        if (Seasons.Count > 0)
            SelectedSeasonIndex = 0;
    }

    partial void OnSelectedSeasonIndexChanged(int value)
    {
        if (Seasons.Count > value && Item != null)
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
    private void PlayItem(BaseItemDto item)
    {
        _nav.NavigateTo(typeof(PlayerPage), item.Id);
    }

    [RelayCommand]
    private void GoBack()
    {
        _nav.GoBack();
    }
}
