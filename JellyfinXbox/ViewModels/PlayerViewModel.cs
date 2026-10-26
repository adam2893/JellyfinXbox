using System.Collections.ObjectModel;
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
    private readonly DiagnosticsLogService _diagnostics;

    private MediaPlayer? _player;
    private Timer? _reportTimer;
    private CancellationTokenSource? _loadCts;
    private string? _currentItemId;
    private MediaSourceInfo? _currentMediaSource;
    private bool _triedTranscodeFallback;
    private long _loadGeneration;
    private bool _upNextDismissed;

    private IntroTimestampsResult? _introTimestamps;
    private TrickplayInfoDto? _trickplayInfo;
    private BaseItemDto? _nextEpisode;
    private DispatcherTimer? _upNextTimer;

    [ObservableProperty] public partial bool IsPlaying { get; set; }
    [ObservableProperty] public partial bool IsBuffering { get; set; }
    [ObservableProperty] public partial bool HasVideo { get; set; }
    [ObservableProperty] public partial string Title { get; set; }
    [ObservableProperty] public partial string Subtitle { get; set; }
    [ObservableProperty] public partial TimeSpan Position { get; set; }
    [ObservableProperty] public partial TimeSpan Duration { get; set; }
    [ObservableProperty] public partial string PositionDisplay { get; set; }
    [ObservableProperty] public partial string DurationDisplay { get; set; }
    [ObservableProperty] public partial string? BackdropUrl { get; set; }
    [ObservableProperty] public partial double BufferingProgress { get; set; }
    [ObservableProperty] public partial string CurrentVideoCodec { get; set; }
    [ObservableProperty] public partial string CurrentAudioCodec { get; set; }
    [ObservableProperty] public partial string CurrentResolution { get; set; }
    [ObservableProperty] public partial bool IsHdr { get; set; }
    [ObservableProperty] public partial bool IsDirectPlay { get; set; }
    [ObservableProperty] public partial bool HasPlaybackError { get; set; }
    [ObservableProperty] public partial string PlaybackErrorMessage { get; set; } = "";
    [ObservableProperty] public partial string PlaybackStatusMessage { get; set; } = "";

    /// <summary>Seek slider position in seconds (TwoWay-bound in XAML).</summary>
    [ObservableProperty] public partial double SliderSeconds { get; set; }

    // Intro & Credits Skipper
    [ObservableProperty] public partial bool ShowSkipIntro { get; set; }
    [ObservableProperty] public partial bool ShowSkipCredits { get; set; }
    [ObservableProperty] public partial bool ShowSkipPrompt { get; set; }
    [ObservableProperty] public partial string SkipPromptText { get; set; } = "Skip Intro";

    // Chapters
    [ObservableProperty] public partial string CurrentChapterName { get; set; } = "";
    public ObservableCollection<ChapterInfo> Chapters { get; } = new();

    // Trickplay scrub preview
    [ObservableProperty] public partial bool ShowTrickplayPreview { get; set; }
    [ObservableProperty] public partial string? TrickplayPreviewUrl { get; set; }
    [ObservableProperty] public partial string TrickplayTimeText { get; set; } = "";

    // Up Next countdown overlay
    [ObservableProperty] public partial bool ShowUpNextOverlay { get; set; }
    [ObservableProperty] public partial int UpNextCountdown { get; set; } = 10;
    [ObservableProperty] public partial string UpNextTitle { get; set; } = "";
    [ObservableProperty] public partial string? UpNextThumbnailUrl { get; set; }

    // Playback Speed
    [ObservableProperty] public partial double PlaybackSpeed { get; set; } = 1.0;
    [ObservableProperty] public partial string PlaybackSpeedDisplay { get; set; } = "1.0×";

    // Audio & Subtitle Sync Delay Offsets (in milliseconds)
    [ObservableProperty] public partial int AudioOffsetMs { get; set; } = 0;
    [ObservableProperty] public partial int SubtitleOffsetMs { get; set; } = 0;
    [ObservableProperty] public partial string AudioOffsetDisplay { get; set; } = "0 ms";
    [ObservableProperty] public partial string SubtitleOffsetDisplay { get; set; } = "0 ms";

    public TrackSelectionViewModel TrackSelection => _trackSelection;

    public PlayerViewModel(JellyfinApiClient api, NavigationService nav, DiagnosticsLogService diagnostics)
    {
        _api = api;
        _nav = nav;
        _diagnostics = diagnostics;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _trackSelection = new TrackSelectionViewModel(api);
        _trackSelection.OnTracksChanged += OnTracksChanged;
        PositionDisplay = "0:00";
        DurationDisplay = "0:00";

        // Listen for Remote Control WebSocket commands
        _api.OnPlaystateCommand += Api_OnPlaystateCommand;
        _api.OnGeneralCommand += Api_OnGeneralCommand;
        _api.OnPlayCommand += Api_OnPlayCommand;
    }

    public event Action<MediaPlayer>? PlayerInitialized;
    public MediaPlayer? Player => _player;
    private TimeSpan? _initialSeekPosition;

    public async Task LoadAsync(string itemId, TimeSpan? startPosition = null)
    {
        itemId = itemId?.Trim() ?? string.Empty;
        _diagnostics.Log("player", "load-start",
            ("itemId", DiagnosticsLogService.RedactId(itemId)),
            ("startTicks", startPosition?.Ticks ?? 0));

        if (string.IsNullOrWhiteSpace(itemId))
        {
            PlaybackErrorMessage = "This item has no valid identifier.";
            PlaybackStatusMessage = "Playback is unavailable.";
            HasPlaybackError = true;
            return;
        }

        var generation = Interlocked.Increment(ref _loadGeneration);
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        var loadCts = new CancellationTokenSource();
        _loadCts = loadCts;
        _reportTimer?.Dispose();
        _reportTimer = null;
        _upNextTimer?.Stop();
        _upNextTimer = null;
        _upNextDismissed = false;
        HasPlaybackError = false;
        PlaybackErrorMessage = "";
        PlaybackStatusMessage = "Loading playback…";
        // Detach the old media instance before awaiting network work so late
        // events from it cannot update the newly requested item's state.
        DisposePlayer();
        try
        {
        var itemResult = await _api.GetItemResultAsync(itemId);
        if (loadCts.IsCancellationRequested || generation != _loadGeneration) return;
        var item = itemResult.Item;
        _diagnostics.Log("player", "item-result",
            ("itemId", DiagnosticsLogService.RedactId(itemId)),
            ("found", item != null),
            ("error", itemResult.Error?.GetType().Name ?? "none"),
            ("server", _api.ServerUrl));
        if (item == null)
        {
            PlaybackErrorMessage = itemResult.Error?.Message ?? "This item could not be found.";
            PlaybackStatusMessage = "Playback is unavailable.";
            HasPlaybackError = true;
            return;
        }
        if (generation != _loadGeneration) return;

        _currentItemId = itemId;
        _triedTranscodeFallback = false;
        Title = item.Name;
        _diagnostics.Log("player", "item-ready",
            ("itemId", DiagnosticsLogService.RedactId(itemId)),
            ("itemType", item.Type),
            ("title", item.Name));

        if (startPosition.HasValue && startPosition.Value > TimeSpan.Zero)
        {
            _initialSeekPosition = startPosition;
        }
        else if (item.UserData?.PlaybackPositionTicks > 0)
        {
            _initialSeekPosition = TimeSpan.FromTicks(item.UserData.PlaybackPositionTicks);
        }
        else
        {
            _initialSeekPosition = null;
        }

        // Reset player feature state
        ShowSkipIntro = false;
        ShowSkipCredits = false;
        ShowSkipPrompt = false;
        ShowUpNextOverlay = false;
        ShowTrickplayPreview = false;
        _nextEpisode = null;
        _introTimestamps = null;
        _trickplayInfo = null;

        // Give itemId to TrackSelection for online subtitle search
        _trackSelection.SetItemId(itemId);

        // Load chapters
        Chapters.Clear();
        CurrentChapterName = "";
        if (item.Chapters != null && item.Chapters.Count > 0)
        {
            foreach (var ch in item.Chapters.Where(ch => ch is not null)) Chapters.Add(ch);
            if (Chapters.Count > 0)
                CurrentChapterName = Chapters[0].Name;
        }

        // Fetch intro timestamps in background
        _ = Task.Run(async () =>
        {
            if (loadCts.IsCancellationRequested) return;
            var result = await _api.GetIntroTimestampsAsync(itemId);
            if (!loadCts.IsCancellationRequested && generation == _loadGeneration) _introTimestamps = result;
        }, loadCts.Token);

        // Fetch trickplay manifest in background
        _ = Task.Run(async () =>
        {
            if (loadCts.IsCancellationRequested) return;
            var result = await _api.GetTrickplayManifestAsync(itemId);
            if (!loadCts.IsCancellationRequested && generation == _loadGeneration) _trickplayInfo = result;
        }, loadCts.Token);

        if (item.Type == "Episode")
        {
            Subtitle = !string.IsNullOrEmpty(item.SeriesName)
                ? $"S{item.ParentIndexNumber:D2}E{item.IndexNumber:D2} · {item.SeriesName}"
                : $"S{item.ParentIndexNumber:D2}E{item.IndexNumber:D2}";

            // Look up next episode in background
            if (!string.IsNullOrEmpty(item.SeriesId))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (loadCts.IsCancellationRequested) return;
                        var eps = await _api.GetEpisodesAsync(item.SeriesId, item.SeasonId ?? "");
                        var idx = eps.Items.FindIndex(e => e.Id == item.Id);
                        if (!loadCts.IsCancellationRequested && generation == _loadGeneration && idx >= 0 && idx < eps.Items.Count - 1)
                        {
                            _nextEpisode = eps.Items[idx + 1];
                            _dispatcher.TryEnqueue(() =>
                            {
                                UpNextTitle = $"S{_nextEpisode.ParentIndexNumber:D2}E{_nextEpisode.IndexNumber:D2} · {_nextEpisode.Name}";
                                UpNextThumbnailUrl = _api.GetItemImageUrl(_nextEpisode, "Primary", 400);
                            });
                        }
                    }
                    catch { }
                }, loadCts.Token);
            }
        }
        else if (item.ProductionYear.HasValue)
        {
            Subtitle = item.ProductionYear.Value.ToString();
        }

        var backdropId = item.BackdropImageTags?.Count > 0 ? item.Id : item.ParentBackdropItemId;
        if (!string.IsNullOrEmpty(backdropId))
            BackdropUrl = _api.GetItemImageUrl(item, "Backdrop", 1920);

        // Use settings bitrate
        var maxBitrate = _api.Settings.MaxStreamingBitrate;
        IsBuffering = true;
        var playbackResult = await _api.GetPlaybackInfoResultAsync(itemId, maxBitrate);
        if (loadCts.IsCancellationRequested || generation != _loadGeneration) return;
        var playbackInfo = playbackResult.PlaybackInfo;
        _diagnostics.Log("player", "playback-info-result",
            ("itemId", DiagnosticsLogService.RedactId(itemId)),
            ("sourceCount", playbackInfo?.MediaSources?.Count ?? 0),
            ("error", playbackResult.Error?.GetType().Name ?? "none"),
            ("bitrate", maxBitrate));

        if (playbackInfo?.MediaSources == null || playbackInfo.MediaSources.Count == 0)
        {
            IsBuffering = false;
            PlaybackErrorMessage = playbackResult.Error?.Message ?? "This item could not be played. Check the server connection and try again.";
            PlaybackStatusMessage = "Playback is unavailable.";
            HasPlaybackError = true;
            return;
        }

        var source = SelectMediaSource(playbackInfo.MediaSources);
        _currentMediaSource = source;

        var mediaUrl = BuildMediaUrl(itemId, source);
        _diagnostics.Log("player", "source-selected",
            ("itemId", DiagnosticsLogService.RedactId(itemId)),
            ("source", source.Name),
            ("directPlay", source.SupportsDirectPlay),
            ("directStream", source.SupportsDirectStream),
            ("transcoding", source.SupportsTranscoding),
            ("container", source.Container),
            ("url", mediaUrl));
        Debug.WriteLine($"[Player] Playing item {itemId} at {RedactPlaybackUrl(mediaUrl)}");
        Debug.WriteLine($"[Player] Source: {source.Name}, DirectPlay: {source.SupportsDirectPlay}, Container: {source.Container}");

        try
        {
            InitializePlayer();
            if (!Uri.TryCreate(mediaUrl, UriKind.Absolute, out var mediaUri) ||
                (mediaUri.Scheme != Uri.UriSchemeHttp && mediaUri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException("The server returned an invalid playback URL.");
            _player!.Source = MediaSource.CreateFromUri(mediaUri);
        }
        catch (Exception ex)
        {
            IsBuffering = false;
            PlaybackErrorMessage = $"Playback setup failed: {ex.Message}";
            HasPlaybackError = true;
            return;
        }

        // Load track info for selection UI
        _trackSelection.LoadTracks(source);

        _ = _api.ReportPlaybackStartAsync(itemId);

        _reportTimer = new Timer(async _ => await ReportProgress(itemId, generation), null, 5000, 10000);
        _diagnostics.Log("player", "load-source-attached", ("itemId", DiagnosticsLogService.RedactId(itemId)));
        }
        catch (OperationCanceledException) when (loadCts.IsCancellationRequested)
        {
            _diagnostics.Log("player", "load-cancelled", ("itemId", DiagnosticsLogService.RedactId(itemId)));
        }
        catch (Exception ex)
        {
            _diagnostics.LogException("player", "load-failed", ex, ("itemId", DiagnosticsLogService.RedactId(itemId)));
            IsBuffering = false;
            IsPlaying = false;
            PlaybackStatusMessage = "Playback setup failed.";
            PlaybackErrorMessage = "Playback setup failed. Open Diagnostics and share the log if this repeats.";
            HasPlaybackError = true;
        }
    }

    private MediaSourceInfo SelectMediaSource(List<MediaSourceInfo> sources)
    {
        var validSources = sources.Where(s => s is not null).ToList();
        if (validSources.Count == 0)
            throw new InvalidOperationException("The server returned no usable media sources.");

        var directPlay = validSources
            .Where(s => s.SupportsDirectPlay)
            .OrderByDescending(s =>
            {
                var video = (s.MediaStreams ?? new()).FirstOrDefault(ms => ms is not null && ms.Type == "Video");
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

        var transcode = validSources.OrderByDescending(s => s.SupportsTranscoding).FirstOrDefault() ?? validSources[0];
        IsDirectPlay = false;
        UpdateCodecInfo(transcode);
        return transcode;
    }

    private void UpdateCodecInfo(MediaSourceInfo source)
    {
        CurrentVideoCodec = "";
        CurrentAudioCodec = "";
        CurrentResolution = "";
        IsHdr = false;
        var streams = source.MediaStreams ?? new();
        var video = streams.FirstOrDefault(ms => ms is not null && ms.Type == "Video");
        var audio = streams.FirstOrDefault(ms => ms is not null && ms.Type == "Audio");

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
        if (source.SupportsDirectPlay && !string.IsNullOrEmpty(source.DirectStreamUrl))
            return JellyfinEndpointBuilder.WithQuery(CombineServerUrl(source.DirectStreamUrl), ("api_key", _api.AccessToken));

        if (source.SupportsDirectStream)
            return JellyfinEndpointBuilder.WithQuery(CombineServerUrl($"Videos/{Uri.EscapeDataString(itemId)}/stream"), ("static", true), ("MediaSourceId", source.Id), ("api_key", _api.AccessToken));

        if (!string.IsNullOrEmpty(source.TranscodingUrl))
            return JellyfinEndpointBuilder.WithQuery(CombineServerUrl(source.TranscodingUrl), ("api_key", _api.AccessToken));

        return JellyfinEndpointBuilder.WithQuery(CombineServerUrl($"Videos/{Uri.EscapeDataString(itemId)}/stream"), ("static", true), ("MediaSourceId", source.Id), ("api_key", _api.AccessToken));
    }

    private string CombineServerUrl(string path) =>
        Uri.TryCreate(path, UriKind.Absolute, out var absolute) &&
        (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps)
            ? absolute.ToString()
            : JellyfinEndpointBuilder.Combine(_api.ServerUrl, path);

    private static string RedactPlaybackUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return "<invalid url>";
        return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
    }

    /// <summary>
    /// Switch audio/subtitle tracks — rebuilds the playback URL with new track indices.
    /// </summary>
    private void OnTracksChanged(MediaStream? audio, MediaStream? subtitle)
    {
        if (_player?.PlaybackSession == null || _currentMediaSource == null || _currentItemId == null) return;

        var itemId = _currentItemId;
        var source = _currentMediaSource;
        var currentPosition = _player.PlaybackSession.Position;

        // Build new URL with selected tracks
        var newUrl = _api.BuildTranscodeUrlWithTracks(
            itemId, source,
            audio?.Index,
            subtitle?.Index);

        _diagnostics.Log("player", "track-switch",
            ("audio", audio?.DisplayTitle ?? "default"),
            ("subtitle", subtitle?.DisplayTitle ?? "off"),
            ("url", newUrl));
        Debug.WriteLine($"[Player] Switching tracks — Audio: {audio?.DisplayTitle ?? "default"}, Sub: {subtitle?.DisplayTitle ?? "off"}");

        // Re-create source with track selection. Keep this guarded because a
        // malformed server URL must become a readable playback error.
        if (!Uri.TryCreate(newUrl, UriKind.Absolute, out var trackUri) ||
            (trackUri.Scheme != Uri.UriSchemeHttp && trackUri.Scheme != Uri.UriSchemeHttps))
        {
            SetPlaybackFailureOnUi("The server returned an invalid track-switch URL.");
            return;
        }

        _dispatcher.TryEnqueue(() =>
        {
            try
            {
                if (_player != null)
                    _player.Source = MediaSource.CreateFromUri(trackUri);
            }
            catch (Exception ex)
            {
                _diagnostics.LogException("player", "track-switch-failed", ex);
                SetPlaybackFailureOnUi("The selected track could not be started.");
            }
        });

        // Seek back to current position
        var seekTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        seekTimer.Tick += (s, e) =>
        {
            seekTimer.Stop();
            try
            {
                if (_player?.PlaybackSession != null)
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

        PlayerInitialized?.Invoke(_player);
    }

    private void Player_MediaOpened(MediaPlayer sender, object args)
    {
        if (!ReferenceEquals(sender, _player)) return;
        _diagnostics.Log("player", "media-opened",
            ("itemId", DiagnosticsLogService.RedactId(_currentItemId)),
            ("video", $"{sender.PlaybackSession.NaturalVideoWidth}x{sender.PlaybackSession.NaturalVideoHeight}"));
        _dispatcher.TryEnqueue(() =>
        {
            Duration = sender.PlaybackSession.NaturalDuration;
            DurationDisplay = FormatTime(Duration);
            HasVideo = sender.PlaybackSession.NaturalVideoHeight > 0;
            IsBuffering = false;
            HasPlaybackError = false;
            PlaybackStatusMessage = "Playing";
            Debug.WriteLine($"[Player] Opened. Duration: {Duration}, Video: {sender.PlaybackSession.NaturalVideoWidth}x{sender.PlaybackSession.NaturalVideoHeight}");

            if (_initialSeekPosition.HasValue && _initialSeekPosition.Value > TimeSpan.Zero)
            {
                var pos = _initialSeekPosition.Value;
                _initialSeekPosition = null;
                try
                {
                    sender.PlaybackSession.Position = pos;
                }
                catch { }
            }
        });
    }

    private void Playback_PlaybackStateChanged(MediaPlaybackSession sender, object args)
    {
        _dispatcher.TryEnqueue(() =>
        {
            var state = sender.PlaybackState;
            _diagnostics.Log("player", "state-changed", ("state", state));
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

            // 1. Intro & Credits Skipper check
            if (_introTimestamps != null && _introTimestamps.Valid)
            {
                var sec = sender.Position.TotalSeconds;
                if (_introTimestamps.IntroEnd > 0 && sec >= _introTimestamps.IntroStart && sec < _introTimestamps.IntroEnd)
                {
                    ShowSkipIntro = true;
                    ShowSkipCredits = false;
                    ShowSkipPrompt = true;
                    SkipPromptText = "Skip Intro";
                }
                else if (_introTimestamps.CreditsEnd > 0 && sec >= _introTimestamps.CreditsStart && sec < _introTimestamps.CreditsEnd)
                {
                    ShowSkipIntro = false;
                    ShowSkipCredits = true;
                    ShowSkipPrompt = true;
                    SkipPromptText = "Skip Credits";

                     if (_nextEpisode != null && !_upNextDismissed && !ShowUpNextOverlay)
                    {
                        TriggerUpNextCountdown();
                    }
                }
                else
                {
                    ShowSkipIntro = false;
                    ShowSkipCredits = false;
                    ShowSkipPrompt = false;
                }
            }

            // 2. Chapter tracking
            if (Chapters.Count > 0)
            {
                var curTicks = sender.Position.Ticks;
                var curCh = Chapters.LastOrDefault(c => c.StartPositionTicks <= curTicks) ?? Chapters[0];
                CurrentChapterName = curCh.Name;
            }

            // 3. Up Next check near end of episode (last 25s)
            if (_nextEpisode != null && !_upNextDismissed && !ShowUpNextOverlay && Duration.TotalSeconds > 60)
            {
                var remaining = (Duration - sender.Position).TotalSeconds;
                if (remaining <= 25 && remaining > 0)
                {
                    TriggerUpNextCountdown();
                }
            }
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
        {
            UpdateTrickplayPreview(value);
            SeekTo(TimeSpan.FromSeconds(value));
        }
    }

    public void UpdateTrickplayPreview(double seconds)
    {
        if (Duration.TotalSeconds <= 0) return;
        TrickplayTimeText = FormatTime(TimeSpan.FromSeconds(seconds));

        if (_trickplayInfo != null && _trickplayInfo.Interval > 0 && !string.IsNullOrEmpty(_currentItemId))
        {
            var thumbIndex = (int)((seconds * 1000) / _trickplayInfo.Interval);
            if (thumbIndex >= 0 && thumbIndex < _trickplayInfo.ThumbnailCount)
            {
                var tilesPerSheet = _trickplayInfo.TileWidth * _trickplayInfo.TileHeight;
                var sheetIndex = tilesPerSheet > 1 ? thumbIndex / tilesPerSheet : thumbIndex;
                TrickplayPreviewUrl = _api.GetTrickplayTileUrl(_currentItemId, _trickplayInfo.Width, sheetIndex);
                ShowTrickplayPreview = true;
                return;
            }
        }
        ShowTrickplayPreview = false;
    }

    public string UpNextCountdownText => $"Play Now ({UpNextCountdown}s)";

    partial void OnUpNextCountdownChanged(int value) => OnPropertyChanged(nameof(UpNextCountdownText));

    private void TriggerUpNextCountdown()
    {
        if (_nextEpisode == null || _upNextDismissed || ShowUpNextOverlay || !_api.Settings.AutoPlayNextEpisode) return;
        ShowUpNextOverlay = true;
        UpNextCountdown = 10;
        _upNextTimer?.Stop();
        _upNextTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _upNextTimer.Tick += (s, e) =>
        {
            UpNextCountdown--;
            if (UpNextCountdown <= 0)
            {
                _upNextTimer.Stop();
                PlayNextCommand.Execute(null);
            }
        };
        _upNextTimer.Start();
    }

    private async void Player_MediaEnded(MediaPlayer sender, object args)
    {
        if (!ReferenceEquals(sender, _player)) return;
        _diagnostics.Log("player", "media-ended", ("itemId", DiagnosticsLogService.RedactId(_currentItemId)));
        Debug.WriteLine("[Player] Media ended.");
        IsPlaying = false;

        if (_currentItemId != null)
        {
            await _api.ReportPlaybackStoppedAsync(_currentItemId, Duration.Ticks);
            await _api.MarkPlayedAsync(_currentItemId);
        }

        _reportTimer?.Dispose();

        _dispatcher.TryEnqueue(() =>
        {
            if (_nextEpisode != null && _api.Settings.AutoPlayNextEpisode && !_upNextDismissed)
            {
                TriggerUpNextCountdown();
            }
        });
    }

    private async void Player_MediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
    {
        if (!ReferenceEquals(sender, _player)) return;
        try
        {
            _diagnostics.Log("player", "media-failed",
                ("itemId", DiagnosticsLogService.RedactId(_currentItemId)),
                ("error", args.Error),
                ("extended", args.ExtendedErrorCode));
            Debug.WriteLine($"[Player] Failed: {args.Error}, Extended: {args.ExtendedErrorCode}");
            SetPlaybackStateOnUi(isBuffering: false, isPlaying: false);

            if (_currentItemId == null || _currentMediaSource == null) return;

            if (!_triedTranscodeFallback && !string.IsNullOrEmpty(_currentMediaSource.TranscodingUrl))
            {
                _triedTranscodeFallback = true;
                IsDirectPlay = false;
                SetPlaybackStateOnUi(isBuffering: true, isPlaying: false);
                Debug.WriteLine("[Player] Direct play failed - retrying with transcode");
                _diagnostics.Log("player", "transcode-fallback");

                var url = _api.BuildTranscodeUrlWithTracks(_currentItemId, _currentMediaSource);
                if (!Uri.TryCreate(url, UriKind.Absolute, out var transcodeUri) ||
                    (transcodeUri.Scheme != Uri.UriSchemeHttp && transcodeUri.Scheme != Uri.UriSchemeHttps))
                {
                    SetPlaybackFailureOnUi("The server returned an invalid transcode URL.");
                    return;
                }

                _dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        if (_player != null)
                            _player.Source = MediaSource.CreateFromUri(transcodeUri);
                    }
                    catch (Exception ex)
                    {
                        _diagnostics.LogException("player", "transcode-attach-failed", ex);
                        SetPlaybackFailureOnUi("The transcode stream could not be started.");
                    }
                });
                return;
            }

            await _api.ReportPlaybackStoppedAsync(_currentItemId, Position.Ticks);
            SetPlaybackFailureOnUi("Playback stopped unexpectedly. Retry or go back and try another source.");
        }
        catch (Exception ex)
        {
            _diagnostics.LogException("player", "media-failed-handler-crashed", ex);
            SetPlaybackFailureOnUi("Playback stopped unexpectedly. Open Diagnostics and share the log.");
        }
    }

    private void SetPlaybackStateOnUi(bool isBuffering, bool isPlaying)
    {
        _dispatcher.TryEnqueue(() =>
        {
            IsBuffering = isBuffering;
            IsPlaying = isPlaying;
        });
    }

    private void SetPlaybackFailureOnUi(string message)
    {
        _dispatcher.TryEnqueue(() =>
        {
            IsBuffering = false;
            IsPlaying = false;
            PlaybackErrorMessage = message;
            PlaybackStatusMessage = message;
            HasPlaybackError = true;
        });
    }

    [RelayCommand]
    private async Task RetryPlayback()
    {
        if (string.IsNullOrEmpty(_currentItemId)) return;
        HasPlaybackError = false;
        await LoadAsync(_currentItemId, Position > TimeSpan.Zero ? Position : null);
    }

    private async Task ReportProgress(string itemId, long generation)
    {
        try
        {
            if (generation == _loadGeneration && _currentItemId == itemId && _player != null)
                await _api.ReportPlaybackProgressAsync(itemId, Position.Ticks, !IsPlaying);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Player] ReportProgress error: {ex.Message}");
        }
    }

    #region Transport Controls & Commands

    [RelayCommand] private void Play() => _player?.Play();
    [RelayCommand] private void Pause() => _player?.Pause();

    [RelayCommand]
    private void TogglePlayPause()
    {
        try
        {
            if (_player?.PlaybackSession == null)
            {
                _diagnostics.Log("player", "toggle-ignored", ("reason", "no-player"));
                return;
            }

            var wasPlaying = _player.PlaybackSession.PlaybackState == MediaPlaybackState.Playing;
            _diagnostics.Log("player", "toggle-play", ("action", wasPlaying ? "pause" : "play"));
            if (wasPlaying) _player.Pause();
            else _player.Play();
        }
        catch (Exception ex)
        {
            _diagnostics.LogException("player", "toggle-play-failed", ex);
            SetPlaybackFailureOnUi("The player could not change state.");
        }
    }

    [RelayCommand]
    private void SeekForward()
    {
        try
        {
            if (_player?.PlaybackSession != null)
            {
                _diagnostics.Log("player", "seek", ("direction", "forward"));
                _player.PlaybackSession.Position = Duration > TimeSpan.Zero && _player.PlaybackSession.Position + TimeSpan.FromSeconds(10) > Duration ? Duration : _player.PlaybackSession.Position + TimeSpan.FromSeconds(10);
            }
        }
        catch (Exception ex) { _diagnostics.LogException("player", "seek-forward-failed", ex); }
    }

    [RelayCommand]
    private void SeekBackward()
    {
        try
        {
            if (_player?.PlaybackSession != null)
            {
                _diagnostics.Log("player", "seek", ("direction", "backward"));
                var newPos = _player.PlaybackSession.Position - TimeSpan.FromSeconds(10);
                _player.PlaybackSession.Position = newPos < TimeSpan.Zero ? TimeSpan.Zero : newPos;
            }
        }
        catch (Exception ex) { _diagnostics.LogException("player", "seek-backward-failed", ex); }
    }

    [RelayCommand]
    private void SeekTo(TimeSpan position)
    {
        try
        {
            if (_player?.PlaybackSession != null)
            {
                _diagnostics.Log("player", "seek-to", ("seconds", position.TotalSeconds));
                _player.PlaybackSession.Position = Duration > TimeSpan.Zero ? (position < TimeSpan.Zero ? TimeSpan.Zero : position > Duration ? Duration : position) : (position < TimeSpan.Zero ? TimeSpan.Zero : position);
            }
        }
        catch (Exception ex) { _diagnostics.LogException("player", "seek-to-failed", ex); }
    }

    [RelayCommand]
    private void Stop()
    {
        _diagnostics.Log("player", "stop");
        if (_currentItemId != null && _player?.PlaybackSession != null)
            _ = _api.ReportPlaybackStoppedAsync(_currentItemId, Position.Ticks);
        _player?.Pause();
        _reportTimer?.Dispose();
        _upNextTimer?.Stop();
        _nav.GoBack();
    }

    [RelayCommand]
    private void SkipIntro()
    {
        if (ShowSkipIntro && _introTimestamps != null && _introTimestamps.IntroEnd > 0)
        {
            SeekTo(TimeSpan.FromSeconds(_introTimestamps.IntroEnd));
            ShowSkipIntro = false;
        }
        else if (ShowSkipCredits && _introTimestamps != null && _introTimestamps.CreditsEnd > 0)
        {
            SeekTo(TimeSpan.FromSeconds(_introTimestamps.CreditsEnd));
            ShowSkipCredits = false;
        }
    }

    [RelayCommand]
    private void NextChapter()
    {
        if (Chapters.Count == 0 || _player?.PlaybackSession == null) return;
        var curTicks = _player.PlaybackSession.Position.Ticks;
        var next = Chapters.FirstOrDefault(c => c.StartPositionTicks > curTicks + 10_000_000);
        if (next != null) SeekTo(next.StartPosition);
    }

    [RelayCommand]
    private void PreviousChapter()
    {
        if (Chapters.Count == 0 || _player?.PlaybackSession == null) return;
        var curTicks = _player.PlaybackSession.Position.Ticks;
        var prev = Chapters.LastOrDefault(c => c.StartPositionTicks < curTicks - 20_000_000);
        if (prev != null) SeekTo(prev.StartPosition);
        else SeekTo(TimeSpan.Zero);
    }

    [RelayCommand]
    private void SelectChapter(ChapterInfo chapter)
    {
        if (chapter != null) SeekTo(chapter.StartPosition);
    }

    [RelayCommand]
    private void CyclePlaybackSpeed()
    {
        double[] speeds = [0.5, 0.75, 1.0, 1.25, 1.5, 2.0];
        var idx = Array.IndexOf(speeds, PlaybackSpeed);
        if (idx < 0) idx = 2; // 1.0 default
        var nextIdx = (idx + 1) % speeds.Length;
        SetPlaybackSpeed(speeds[nextIdx]);
    }

    [RelayCommand]
    private void SetPlaybackSpeed(double speed)
    {
        PlaybackSpeed = speed;
        PlaybackSpeedDisplay = $"{speed:0.##}×";
        try
        {
            if (_player?.PlaybackSession != null)
            {
                _diagnostics.Log("player", "speed-change", ("speed", speed));
                _player.PlaybackSession.PlaybackRate = speed;
            }
        }
        catch (Exception ex) { _diagnostics.LogException("player", "speed-change-failed", ex); }
    }

    [RelayCommand]
    private void AdjustAudioOffset(int deltaMs)
    {
        // MediaPlaybackSession has no audio/subtitle offset API. Keep the existing
        // state for UI compatibility, but do not claim this changes playback.
        AudioOffsetMs = Math.Clamp(AudioOffsetMs + deltaMs, -5000, 5000);
        AudioOffsetDisplay = AudioOffsetMs == 0 ? "0 ms" : $"{(AudioOffsetMs > 0 ? "+" : "")}{AudioOffsetMs} ms";
    }

    [RelayCommand]
    private void AdjustSubtitleOffset(int deltaMs)
    {
        // MediaPlaybackSession has no audio/subtitle offset API. Keep the existing
        // state for UI compatibility, but do not claim this changes playback.
        SubtitleOffsetMs = Math.Clamp(SubtitleOffsetMs + deltaMs, -5000, 5000);
        SubtitleOffsetDisplay = SubtitleOffsetMs == 0 ? "0 ms" : $"{(SubtitleOffsetMs > 0 ? "+" : "")}{SubtitleOffsetMs} ms";
    }

    [RelayCommand]
    private void PlayNext()
    {
        _upNextTimer?.Stop();
        ShowUpNextOverlay = false;
        if (_nextEpisode != null)
        {
            _ = LoadAsync(_nextEpisode.Id);
        }
    }

    [RelayCommand]
    private void CancelUpNext()
    {
        _upNextTimer?.Stop();
        ShowUpNextOverlay = false;
        _upNextDismissed = true;
    }

    #endregion

    #region Remote Control Handlers

    private void Api_OnPlaystateCommand(PlaystateCommandData cmd)
    {
        _dispatcher.TryEnqueue(() =>
        {
            switch (cmd.Command.ToLowerInvariant())
            {
                case "pause":
                    _player?.Pause();
                    break;
                case "unpause":
                case "play":
                    _player?.Play();
                    break;
                case "playpause":
                    TogglePlayPause();
                    break;
                case "stop":
                    Stop();
                    break;
                case "seek":
                    if (cmd.SeekPositionTicks.HasValue)
                        SeekTo(TimeSpan.FromTicks(cmd.SeekPositionTicks.Value));
                    break;
                case "nexttrack":
                    if (_nextEpisode != null) PlayNext();
                    break;
                case "previoustrack":
                    SeekTo(TimeSpan.Zero);
                    break;
            }
        });
    }

    private void Api_OnGeneralCommand(GeneralCommandData cmd)
    {
        _dispatcher.TryEnqueue(() =>
        {
            switch (cmd.Name.ToLowerInvariant())
            {
                case "setvolume":
                    if (cmd.Arguments.TryGetValue("Volume", out var volStr) && double.TryParse(volStr, out var vol))
                        if (_player != null) _player.Volume = Math.Clamp(vol / 100.0, 0.0, 1.0);
                    break;
                case "volumeup":
                    if (_player != null) _player.Volume = Math.Min(1.0, _player.Volume + 0.05);
                    break;
                case "volumedown":
                    if (_player != null) _player.Volume = Math.Max(0.0, _player.Volume - 0.05);
                    break;
                case "mute":
                    if (_player != null) _player.IsMuted = true;
                    break;
                case "unmute":
                    if (_player != null) _player.IsMuted = false;
                    break;
                case "togglemute":
                    if (_player != null) _player.IsMuted = !_player.IsMuted;
                    break;
            }
        });
    }

    private void Api_OnPlayCommand(PlayCommandData cmd)
    {
        if (cmd.ItemIds != null && cmd.ItemIds.Count > 0)
        {
            _dispatcher.TryEnqueue(async () =>
            {
                var startTicks = cmd.StartPositionTicks.HasValue ? TimeSpan.FromTicks(cmd.StartPositionTicks.Value) : (TimeSpan?)null;
                await LoadAsync(cmd.ItemIds[0], startTicks);
            });
        }
    }

    #endregion

    private static string FormatTime(TimeSpan ts)
    {
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        return $"{ts.Minutes}:{ts.Seconds:D2}";
    }

    public void Dispose()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
        Interlocked.Increment(ref _loadGeneration);
        _api.OnPlaystateCommand -= Api_OnPlaystateCommand;
        _api.OnGeneralCommand -= Api_OnGeneralCommand;
        _api.OnPlayCommand -= Api_OnPlayCommand;
        _upNextTimer?.Stop();
        _reportTimer?.Dispose();
        if (_currentItemId != null && IsPlaying)
            _ = _api.ReportPlaybackStoppedAsync(_currentItemId, Position.Ticks);
        DisposePlayer();
    }

    private void DisposePlayer()
    {
        if (_player == null) return;
        _player.MediaOpened -= Player_MediaOpened;
        _player.PlaybackSession.PlaybackStateChanged -= Playback_PlaybackStateChanged;
        _player.PlaybackSession.PositionChanged -= Playback_PositionChanged;
        _player.MediaEnded -= Player_MediaEnded;
        _player.MediaFailed -= Player_MediaFailed;
        _player.Dispose();
        _player = null;
    }
}
