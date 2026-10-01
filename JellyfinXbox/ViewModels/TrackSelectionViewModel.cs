using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyfinClient.Models;
using JellyfinClient.Services;
using Windows.Media.Playback;

namespace JellyfinXbox.ViewModels;

public partial class TrackSelectionViewModel : ObservableObject
{
    private readonly JellyfinApiClient _api;

    [ObservableProperty] public partial bool IsVisible { get; set; }
    [ObservableProperty] public partial MediaStream? SelectedAudioTrack { get; set; }
    [ObservableProperty] public partial MediaStream? SelectedSubtitleTrack { get; set; }
    [ObservableProperty] public partial bool SubtitlesEnabled { get; set; }

    public ObservableCollection<MediaStream> AudioTracks { get; } = new();
    public ObservableCollection<MediaStream> SubtitleTracks { get; } = new();

    /// <summary>Tracks loaded, available for selection.</summary>
    public bool HasAudioTracks => AudioTracks.Count > 1;
    public bool HasSubtitleTracks => SubtitleTracks.Count > 0;

    // Callback when user changes tracks — PlayerViewModel subscribes
    public event Action<MediaStream?, MediaStream?>? OnTracksChanged;

    private MediaSourceInfo? _currentSource;

    private bool _isLoadingTracks;

    public TrackSelectionViewModel(JellyfinApiClient api)
    {
        _api = api;
        SubtitlesEnabled = true; // was field initializer
    }

    public void LoadTracks(MediaSourceInfo source)
    {
        _isLoadingTracks = true;
        try
        {
            _currentSource = source;

            AudioTracks.Clear();
            SubtitleTracks.Clear();

            var audioStreams = JellyfinApiClient.GetAudioStreams(source);
            foreach (var a in audioStreams) AudioTracks.Add(a);

            var subStreams = JellyfinApiClient.GetSubtitleStreams(source);
            // Add "Off" option
            SubtitleTracks.Add(new MediaStream
            {
                Index = -1,
                Type = "Subtitle",
                DisplayTitle = "Off",
                Language = "off",
                Codec = "off",
                IsDefault = false
            });
            foreach (var s in subStreams) SubtitleTracks.Add(s);

            // Pre-select defaults
            SelectedAudioTrack = AudioTracks.FirstOrDefault(a => a.IsDefault) ?? AudioTracks.FirstOrDefault();
            var defaultSub = SubtitleTracks.FirstOrDefault(s => s.Index != -1 && s.IsDefault);
            SelectedSubtitleTrack = defaultSub ?? SubtitleTracks.FirstOrDefault();
            SubtitlesEnabled = defaultSub != null && SelectedSubtitleTrack?.Index != -1;
        }
        finally
        {
            _isLoadingTracks = false;
        }
    }

    partial void OnSelectedAudioTrackChanged(MediaStream? value)
    {
        if (_isLoadingTracks) return;
        RaiseTracksChanged();
    }

    partial void OnSelectedSubtitleTrackChanged(MediaStream? value)
    {
        if (_isLoadingTracks) return;
        SubtitlesEnabled = value != null && value.Index != -1;
        RaiseTracksChanged();
    }

    private void RaiseTracksChanged()
    {
        if (_isLoadingTracks) return;
        var sub = SubtitlesEnabled ? SelectedSubtitleTrack : SubtitleTracks.FirstOrDefault(s => s.Index == -1);
        OnTracksChanged?.Invoke(SelectedAudioTrack, sub);
    }

    [RelayCommand]
    private void ToggleSubtitles()
    {
        SubtitlesEnabled = !SubtitlesEnabled;
        if (SubtitlesEnabled)
        {
            SelectedSubtitleTrack = SubtitleTracks.FirstOrDefault(s => s.Index != -1) ?? SelectedSubtitleTrack;
        }
        else
        {
            SelectedSubtitleTrack = SubtitleTracks.FirstOrDefault(s => s.Index == -1);
        }
    }

    [ObservableProperty] public partial bool IsSearchingSubtitles { get; set; }
    [ObservableProperty] public partial string SubtitleSearchStatus { get; set; } = "";
    public ObservableCollection<RemoteSubtitleInfo> RemoteSubtitles { get; } = new();

    private string? _currentItemId;

    public void SetItemId(string itemId) => _currentItemId = itemId;

    [RelayCommand]
    public async Task SearchRemoteSubtitlesAsync(string? language = null)
    {
        if (string.IsNullOrEmpty(_currentItemId)) return;
        IsSearchingSubtitles = true;
        SubtitleSearchStatus = "Searching OpenSubtitles...";
        RemoteSubtitles.Clear();
        try
        {
            var lang = !string.IsNullOrEmpty(language) ? language : (!string.IsNullOrEmpty(_api.Settings.PreferredSubtitleLanguage) ? _api.Settings.PreferredSubtitleLanguage : "eng");
            var results = await _api.SearchRemoteSubtitlesAsync(_currentItemId, lang);
            foreach (var r in results) RemoteSubtitles.Add(r);
            SubtitleSearchStatus = RemoteSubtitles.Count > 0 ? $"Found {RemoteSubtitles.Count} subtitle(s)" : "No subtitles found";
        }
        catch
        {
            SubtitleSearchStatus = "Search failed";
        }
        finally
        {
            IsSearchingSubtitles = false;
        }
    }

    [RelayCommand]
    public async Task DownloadSubtitleAsync(RemoteSubtitleInfo sub)
    {
        if (sub == null || string.IsNullOrEmpty(_currentItemId)) return;
        SubtitleSearchStatus = $"Downloading {sub.Name}...";
        var success = await _api.DownloadRemoteSubtitlesAsync(_currentItemId, sub.Id);
        if (success)
        {
            SubtitleSearchStatus = "Downloaded! Refreshing tracks...";
            RemoteSubtitles.Remove(sub);
            // Refresh playback info & tracks
            var pb = await _api.GetPlaybackInfoAsync(_currentItemId);
            if (pb?.MediaSources != null && pb.MediaSources.Count > 0)
            {
                var src = pb.MediaSources.FirstOrDefault(s => s.Id == _currentSource?.Id) ?? pb.MediaSources[0];
                LoadTracks(src);
                var newSub = SubtitleTracks.LastOrDefault(s => s.Index != -1);
                if (newSub != null)
                {
                    SelectedSubtitleTrack = newSub;
                }
            }
        }
        else
        {
            SubtitleSearchStatus = "Download failed";
        }
    }

    [RelayCommand]
    private void Show() => IsVisible = true;

    [RelayCommand]
    private void Hide() => IsVisible = false;

    [RelayCommand]
    private void ToggleVisibility() => IsVisible = !IsVisible;
}
