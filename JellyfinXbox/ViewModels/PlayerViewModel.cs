using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyfinClient.Models;
using JellyfinClient.Services;
using JellyfinXbox.Services;
using Windows.System;
using Windows.UI.Xaml; // DispatcherTimer
using Windows.Media.Core; // MediaSource
using Windows.Media.Playback;

namespace JellyfinXbox.ViewModels;

public partial class PlayerViewModel : ObservableObject, IDisposable
{
    private readonly JellyfinApiClient _api;
    private readonly NavigationService _nav;
    private readonly TrackSelectionViewModel _trackSelection;
    private readonly DispatcherQueue _dispatcher;

    private MediaPlayer? _player;
    private Timer? _reportTimer;
    private string? _currentItemId;
    private MediaSourceInfo? _currentMediaSource;
    private bool _triedTranscodeFallback;

    [ObservableProperty] public partial bool IsPlaying { get; set; }
    [ObservableProperty] public partial bool IsBuffering { get; set; }
    [ObservableProperty] public partial bool HasVideo { get; set; }
    [ObservableProperty] public partial string Title { get; set; }
    [ObservableProperty] public partial string Subtitle { get; set; }
    [ObservableProperty] public partial TimeSpan Position { get; set; }
    [ObservableProperty] public partial TimeSpan Duration { get; set; }
    [ObservableProperty] public partial string PositionDisplay { get; set; }
    [ObservableProperty] public partial string DurationDisplay { get; set; }
    [ObservableProperty] public partial string BackdropUrl { get; set; }
    [ObservableProperty] public partial double BufferingProgress { get; set; }
    [ObservableProperty] public partial string CurrentVideoCodec { get; set; }
    [ObservableProperty] public partial string CurrentAudioCodec { get; set; }
    [ObservableProperty] public partial string CurrentResolution { get; set; }
    [ObservableProperty] public partial bool IsHdr { get; set; }
    [ObservableProperty] public partial bool IsDirectPlay { get; set; }

    /// <summary>Seek slider position in seconds (TwoWay-bound in XAML).</summary>
    [ObservableProperty] public partial double SliderSeconds { get; set; }

    public TrackSelectionViewModel TrackSelection => _trackSelection;

    public PlayerViewModel(JellyfinApiClient api, NavigationService nav)
    {
        _api = api;
        _nav = nav;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _trackSelection = new TrackSelectionViewModel(api);
        _trackSelection.OnTracksChanged += OnTracksChanged;
        PositionDisplay = "0:00";   // was field initializer
        DurationDisplay = "0:00";   // was field initializer
    }

    public MediaPlayer? Player => _player;

    public async Task LoadAsync(string itemId)
    {
        var item = await _api.GetItemAsync(itemId);
        if (item == null) return;

        _currentItemId = itemId;
        _triedTranscodeFallback = false;
        Title = item.Name;

        if (item.Type == "Episode")
        {
            Subtitle = !string.IsNullOrEmpty(item.SeriesName)
                ? $"S{item.ParentIndexNumber:D2}E{item.IndexNumber:D2} · {item.SeriesName}"
                : $"S{item.ParentIndexNumber:D2}E{item.IndexNumber:D2}";
        }
        else if (item.ProductionYear.HasValue)
        {
            Subtitle = item.ProductionYear.Value.ToString();
        }

        var backdropId = item.BackdropImageTags.Count > 0 ? item.Id : item.ParentBackdropItemId;
        if (!string.IsNullOrEmpty(backdropId))
            BackdropUrl = $"{_api.ServerUrl}{_api.GetBackdropUrl(backdropId, maxWidth: 1920)}";

        // Use settings bitrate
        var maxBitrate = _api.Settings.MaxStreamingBitrate;
        IsBuffering = true;
        var playbackInfo = await _api.GetPlaybackInfoAsync(itemId, maxBitrate);

        if (playbackInfo?.MediaSources == null || playbackInfo.MediaSources.Count == 0)
        {
            IsBuffering = false;
            return;
        }

        var source = SelectMediaSource(playbackInfo.MediaSources);
        _currentMediaSource = source;

        var mediaUrl = BuildMediaUrl(itemId, source);
        Debug.WriteLine($"[Player] Playing: {mediaUrl}");
        Debug.WriteLine($"[Player] Source: {source.Name}, DirectPlay: {source.SupportsDirectPlay}, Container: {source.Container}");

        InitializePlayer();
        _player!.Source = MediaSource.CreateFromUri(new Uri(mediaUrl));

        // Load track info for selection UI
        _trackSelection.LoadTracks(source);

        _ = _api.ReportPlaybackStartAsync(itemId);

        _reportTimer = new Timer(async _ => await ReportProgress(), null, 5000, 10000);
    }

    private MediaSourceInfo SelectMediaSource(List<MediaSourceInfo> sources)
    {
        var directPlay = sources
            .Where(s => s.SupportsDirectPlay)
            .OrderByDescending(s =>
            {
                var video = s.MediaStreams.FirstOrDefault(ms => ms.Type == "Video");
                return video?.Codec switch
                {
                    "av1" => 3, "hevc" or "h265" => 2, "h264" or "avc" => 1, _ => 0
                };
            })
            .FirstOrDefault();

        if (directPlay != null)
        {
            IsDirectPlay = true;
            UpdateCodecInfo(directPlay);
            return directPlay;
        }

        var transcode = sources.OrderByDescending(s => s.SupportsTranscoding).FirstOrDefault() ?? sources.First();
        IsDirectPlay = false;
        UpdateCodecInfo(transcode);
        return transcode;
    }

    private void UpdateCodecInfo(MediaSourceInfo source)
    {
        var video = source.MediaStreams.FirstOrDefault(ms => ms.Type == "Video");
        var audio = source.MediaStreams.FirstOrDefault(ms => ms.Type == "Audio");

        if (video != null)
        {
            CurrentVideoCodec = video.Codec?.ToUpperInvariant() ?? "Unknown";
            if (video.Width.HasValue && video.Height.HasValue)
                CurrentResolution = $"{video.Width.Value}x{video.Height.Value}";
            var vr = video.VideoRangeType ?? video.VideoRange;
            IsHdr = !string.IsNullOrEmpty(vr) && vr.Contains("HDR", StringComparison.OrdinalIgnoreCase);
        }

        if (audio != null)
        {
            CurrentAudioCodec = audio.Codec?.ToUpperInvariant() ?? "Unknown";
        }
    }

    private string BuildMediaUrl(string itemId, MediaSourceInfo source)
    {
        var baseUrl = _api.ServerUrl;

        if (source.SupportsDirectPlay && !string.IsNullOrEmpty(source.DirectStreamUrl))
            return $"{baseUrl}{source.DirectStreamUrl}&api_key={_api.AccessToken}";

        if (source.SupportsDirectStream)
            return $"{baseUrl}/Videos/{itemId}/stream?static=true&MediaSourceId={source.Id}&api_key={_api.AccessToken}";

        if (!string.IsNullOrEmpty(source.TranscodingUrl))
            return $"{baseUrl}{source.TranscodingUrl}&api_key={_api.AccessToken}";

        return $"{baseUrl}/Videos/{itemId}/stream?static=true&MediaSourceId={source.Id}&api_key={_api.AccessToken}";
    }

    /// <summary>
    /// Switch audio/subtitle tracks — rebuilds the playback URL with new track indices.
    /// </summary>
    private void OnTracksChanged(MediaStream? audio, MediaStream? subtitle)
    {
        if (_player == null || _currentMediaSource == null || _currentItemId == null) return;

        var itemId = _currentItemId;
        var source = _currentMediaSource;
        var currentPosition = _player.PlaybackSession.Position;

        // Build new URL with selected tracks
        var newUrl = _api.BuildTranscodeUrlWithTracks(
            itemId, source,
            audio?.Index,
            subtitle?.Index);

        Debug.WriteLine($"[Player] Switching tracks — Audio: {audio?.DisplayTitle ?? "default"}, Sub: {subtitle?.DisplayTitle ?? "off"}");

        // Re-create source with track selection
        _player.Source = MediaSource.CreateFromUri(new Uri(newUrl));

        // Seek back to current position
        var seekTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        seekTimer.Tick += (s, e) =>
        {
            seekTimer.Stop();
            try
            {
                _player.PlaybackSession.Position = currentPosition;
            }
            catch { }
        };
        seekTimer.Start();
    }

    private void InitializePlayer()
    {
        if (_player != null)
        {
            _player.MediaOpened -= Player_MediaOpened;
            _player.PlaybackSession.PlaybackStateChanged -= Playback_PlaybackStateChanged;
            _player.PlaybackSession.PositionChanged -= Playback_PositionChanged;
            _player.MediaEnded -= Player_MediaEnded;
            _player.MediaFailed -= Player_MediaFailed;
            _player.Dispose();
        }

        _player = new MediaPlayer();
        _player.AutoPlay = true;
        _player.AudioCategory = MediaPlayerAudioCategory.Movie;
        _player.CommandManager.IsEnabled = false;

        _player.MediaOpened += Player_MediaOpened;
        _player.PlaybackSession.PlaybackStateChanged += Playback_PlaybackStateChanged;
        _player.PlaybackSession.PositionChanged += Playback_PositionChanged;
        _player.MediaEnded += Player_MediaEnded;
        _player.MediaFailed += Player_MediaFailed;
    }

    private void Player_MediaOpened(MediaPlayer sender, object args)
    {
        _dispatcher.TryEnqueue(() =>
        {
            Duration = sender.PlaybackSession.NaturalDuration;
            DurationDisplay = FormatTime(Duration);
            HasVideo = sender.PlaybackSession.NaturalVideoHeight > 0;
            IsBuffering = false;
            Debug.WriteLine($"[Player] Opened. Duration: {Duration}, Video: {sender.PlaybackSession.NaturalVideoWidth}x{sender.PlaybackSession.NaturalVideoHeight}");
        });
    }

    private void Playback_PlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        _dispatcher.TryEnqueue(() =>
        {
            var state = sender.PlaybackState;
            IsPlaying = state == MediaPlaybackState.Playing;
            IsBuffering = state == MediaPlaybackState.Buffering;
        });
    }

    private void Playback_PositionChanged(MediaPlaybackSession sender, object args)
    {
        _dispatcher.TryEnqueue(() =>
        {
            Position = sender.Position;
            PositionDisplay = FormatTime(Position);
            BufferingProgress = sender.BufferingProgress * 100;

            // Don't overwrite the slider while the user is dragging it,
            // otherwise the thumb yanks back under their finger.
            if (!_isUserSeeking)
                SliderSeconds = sender.Position.TotalSeconds;
        });
    }

    // Dragging gate: enabled by the slider's pointer handlers in the page.
    private bool _isUserSeeking;
    public void BeginSeek() => _isUserSeeking = true;
    public void EndSeek() => _isUserSeeking = false;

    // User dragged the slider (TwoWay binding) — seek the player.
    partial void OnSliderSecondsChanged(double value)
    {
        if (_isUserSeeking)
            SeekTo(TimeSpan.FromSeconds(value));
    }

    private async void Player_MediaEnded(MediaPlayer sender, object args)
    {
        Debug.WriteLine("[Player] Media ended.");
        IsPlaying = false;

        if (_currentItemId != null)
        {
            await _api.ReportPlaybackStoppedAsync(_currentItemId, Duration.Ticks);
            await _api.MarkPlayedAsync(_currentItemId);
        }

        _reportTimer?.Dispose();
    }

    private async void Player_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        Debug.WriteLine($"[Player] Failed: {args.Error}, Extended: {args.ExtendedErrorCode}");
        IsBuffering = false;
        IsPlaying = false;

        if (_currentItemId == null || _currentMediaSource == null) return;

        // A direct-play/direct-stream source failed (e.g. codec the Xbox
        // can't decode). Retry once with the server's transcode URL before
        // giving up, so unsupported media still plays instead of dying.
        if (!_triedTranscodeFallback && !string.IsNullOrEmpty(_currentMediaSource.TranscodingUrl))
        {
            _triedTranscodeFallback = true;
            IsDirectPlay = false;
            IsBuffering = true;
            Debug.WriteLine("[Player] Direct play failed - retrying with transcode");

            var url = $"{_api.ServerUrl}{_currentMediaSource.TranscodingUrl}&api_key={_api.AccessToken}";
            _player!.Source = MediaSource.CreateFromUri(new Uri(url));
            return;
        }

        await _api.ReportPlaybackStoppedAsync(_currentItemId, Position.Ticks);
    }

    private async Task ReportProgress()
    {
        if (_currentItemId != null && IsPlaying)
            await _api.ReportPlaybackProgressAsync(_currentItemId, Position.Ticks, !IsPlaying);
    }

    #region Transport Controls

    [RelayCommand] private void Play() => _player?.Play();
    [RelayCommand] private void Pause() => _player?.Pause();

    [RelayCommand]
    private void TogglePlayPause()
    {
        if (_player == null) return;
        if (_player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing)
            _player.Pause();
        else
            _player.Play();
    }

    [RelayCommand]
    private void SeekForward() => _player!.PlaybackSession.Position += TimeSpan.FromSeconds(10);

    [RelayCommand]
    private void SeekBackward() => _player!.PlaybackSession.Position -= TimeSpan.FromSeconds(10);

    [RelayCommand]
    private void SeekTo(TimeSpan position)
    {
        if (_player != null) _player.PlaybackSession.Position = position;
    }

    [RelayCommand]
    private void Stop()
    {
        if (_currentItemId != null)
            _ = _api.ReportPlaybackStoppedAsync(_currentItemId, Position.Ticks);
        _player?.Pause();
        _reportTimer?.Dispose();
        _nav.GoBack();
    }

    #endregion

    private static string FormatTime(TimeSpan ts)
    {
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        return $"{ts.Minutes}:{ts.Seconds:D2}";
    }

    public void Dispose()
    {
        _reportTimer?.Dispose();
        if (_currentItemId != null && IsPlaying)
            _ = _api.ReportPlaybackStoppedAsync(_currentItemId, Position.Ticks);
        _player?.Dispose();
    }
}
