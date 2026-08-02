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

    [ObservableProperty] private bool _isVisible;
    [ObservableProperty] private MediaStream? _selectedAudioTrack;
    [ObservableProperty] private MediaStream? _selectedSubtitleTrack;
    [ObservableProperty] private bool _subtitlesEnabled = true;

    public ObservableCollection<MediaStream> AudioTracks { get; } = new();
    public ObservableCollection<MediaStream> SubtitleTracks { get; } = new();

    /// <summary>Tracks loaded, available for selection.</summary>
    public bool HasAudioTracks => AudioTracks.Count > 1;
    public bool HasSubtitleTracks => SubtitleTracks.Count > 0;

    // Callback when user changes tracks — PlayerViewModel subscribes
    public event Action<MediaStream?, MediaStream?>? OnTracksChanged;

    private MediaSourceInfo? _currentSource;

    public TrackSelectionViewModel(JellyfinApiClient api)
    {
        _api = api;
    }

    public void LoadTracks(MediaSourceInfo source)
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

    partial void OnSelectedAudioTrackChanged(MediaStream? value)
    {
        RaiseTracksChanged();
    }

    partial void OnSelectedSubtitleTrackChanged(MediaStream? value)
    {
        SubtitlesEnabled = value != null && value.Index != -1;
        RaiseTracksChanged();
    }

    private void RaiseTracksChanged()
    {
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

    [RelayCommand]
    private void Show() => IsVisible = true;

    [RelayCommand]
    private void Hide() => IsVisible = false;

    [RelayCommand]
    private void ToggleVisibility() => IsVisible = !IsVisible;
}
