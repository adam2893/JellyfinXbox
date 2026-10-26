using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using JellyfinClient.Models;
using JellyfinXbox.Services;
using JellyfinXbox.ViewModels;

namespace JellyfinXbox.Views;

public sealed partial class PlayerPage : Page
{
    public PlayerViewModel ViewModel { get; }
    private readonly GamepadService _gamepad;
    private readonly DiagnosticsLogService _diagnostics;
    private DispatcherTimer? _hideTimer;
    private bool _isActive;

    // Stored as fields so += / -= in OnNavigatedTo/From match exactly.
    private readonly Action _viewPressedHandler;
    private readonly Action _aPressedHandler;
    private readonly Action _bPressedHandler;
    private readonly Action _xPressedHandler;
    private readonly Action _yPressedHandler;

    public PlayerPage()
    {
        ViewModel = App.Services.GetRequiredService<PlayerViewModel>();
        _gamepad = App.Services.GetRequiredService<GamepadService>();
        _diagnostics = App.Services.GetRequiredService<DiagnosticsLogService>();
        _viewPressedHandler = () => ToggleTrackPanel_Click(this, null!);
        _aPressedHandler = () =>
        {
            var focused = FocusManager.GetFocusedElement() as DependencyObject;
            if (focused is Button || focused is Slider || focused is ListViewItem)
                return;
            PlayPause_Click(this, null!);
        };
        _bPressedHandler = () =>
        {
            if (ViewModel.TrackSelection.IsVisible)
            {
                ViewModel.TrackSelection.HideCommand.Execute(null);
            }
            else
            {
                Back_Click(this, null!);
            }
        };
        _xPressedHandler = () =>
        {
            _diagnostics.Log("player-ui", "gamepad-x");
            ViewModel.TrackSelection.ToggleSubtitlesCommand.Execute(null);
            ShowTransport();
        };
        _yPressedHandler = () =>
        {
            _diagnostics.Log("player-ui", "gamepad-y");
            if (ViewModel.ShowSkipPrompt)
            {
                ViewModel.SkipIntroCommand.Execute(null);
            }
            else
            {
                ViewModel.CyclePlaybackSpeedCommand.Execute(null);
                ShowTransport();
            }
        };
        InitializeComponent();

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _hideTimer.Tick += (s, e) =>
        {
            TransportOverlay.Opacity = 0;
            _hideTimer.Stop();
        };
    }

    private void OnPlayerInitialized(Windows.Media.Playback.MediaPlayer player)
    {
        if (!_isActive || !ReferenceEquals(ViewModel.Player, player))
            return;

        try
        {
            // Attach synchronously while LoadAsync is still on the XAML/UI
            // thread. This prevents the source from being assigned before the
            // MediaPlayerElement owns the new native MediaPlayer.
            player.IsFullWindow = true;
            PlayerElement.SetMediaPlayer(player);
            _diagnostics.Log("player-ui", "media-player-attached");
        }
        catch (Exception ex)
        {
            _diagnostics.LogException("player-ui", "media-player-attach-failed", ex);
            ViewModel.HasPlaybackError = true;
            ViewModel.PlaybackErrorMessage = "The Xbox media surface could not be initialized.";
        }
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _isActive = true;
        _diagnostics.Log("player-ui", "navigated-to", ("parameterType", e.Parameter?.GetType().Name ?? "none"));

        // Controller shortcuts (D-pad focus + A activation are native):
        //   Right/Left trigger → seek; View → track panel; A → play/pause;
        //   B → stop & back; X → toggle subtitles; Y → skip intro / cycle speed; Menu → keep transport visible.
        _gamepad.Start();
        _gamepad.OnRightTrigger += OnRightTrigger;
        _gamepad.OnLeftTrigger += OnLeftTrigger;
        _gamepad.OnViewPressed += _viewPressedHandler;
        _gamepad.OnAPressed += _aPressedHandler;
        _gamepad.OnBPressed += _bPressedHandler;
        _gamepad.OnXPressed += _xPressedHandler;
        _gamepad.OnYPressed += _yPressedHandler;
        _gamepad.OnMenuPressed += ShowTransport;

        ViewModel.PlayerInitialized += OnPlayerInitialized;
        ViewModel.PlayerDisposing += OnPlayerDisposing;
        var player = ViewModel.Player;
        if (player != null)
        {
            try
            {
                player.IsFullWindow = true;
                PlayerElement.SetMediaPlayer(player);
                _diagnostics.Log("player-ui", "existing-media-player-attached");
            }
            catch (Exception ex)
            {
                _diagnostics.LogException("player-ui", "existing-media-player-attach-failed", ex);
            }
        }

        try
        {
            if (e.Parameter is string itemId)
            {
                await ViewModel.LoadAsync(itemId);
            }
            else if (e.Parameter is ValueTuple<string, long?> tuple)
            {
                var startPos = tuple.Item2.HasValue ? TimeSpan.FromTicks(tuple.Item2.Value) : (TimeSpan?)null;
                await ViewModel.LoadAsync(tuple.Item1, startPos);
            }
            else
            {
                _diagnostics.Log("player-ui", "load-ignored", ("reason", "invalid-parameter"));
                ViewModel.HasPlaybackError = true;
                ViewModel.PlaybackErrorMessage = "No playable item was supplied.";
            }
        }
        catch (Exception ex)
        {
            _diagnostics.LogException("player-ui", "navigated-to-failed", ex);
            ViewModel.HasPlaybackError = true;
            ViewModel.PlaybackErrorMessage = "The player failed before playback could start. Open Diagnostics and share the log.";
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _diagnostics.Log("player-ui", "navigated-from");
        _isActive = false;
        DetachMediaPlayer();
        base.OnNavigatedFrom(e);
        ViewModel.PlayerInitialized -= OnPlayerInitialized;
        ViewModel.PlayerDisposing -= OnPlayerDisposing;
        _gamepad.OnRightTrigger -= OnRightTrigger;
        _gamepad.OnLeftTrigger -= OnLeftTrigger;
        _gamepad.OnViewPressed -= _viewPressedHandler;
        _gamepad.OnAPressed -= _aPressedHandler;
        _gamepad.OnBPressed -= _bPressedHandler;
        _gamepad.OnXPressed -= _xPressedHandler;
        _gamepad.OnYPressed -= _yPressedHandler;
        _gamepad.OnMenuPressed -= ShowTransport;
        ViewModel.Dispose();
    }

    private void OnPlayerDisposing(Windows.Media.Playback.MediaPlayer player) => DetachMediaPlayer(player);

    private void DetachMediaPlayer(Windows.Media.Playback.MediaPlayer? player = null)
    {
        try
        {
            if (player != null)
                player.IsFullWindow = false;

            PlayerElement.SetMediaPlayer(null!);
            _diagnostics.Log("player-ui", "media-player-detached");
        }
        catch (Exception ex)
        {
            _diagnostics.LogException("player-ui", "media-player-detach-failed", ex);
        }
    }

    // Trigger events fire continuously while held; a full 10s seek per poll
    // would be too fast, so hold-to-repeat at a fixed cadence instead.
    private DateTime _lastTriggerSeek = DateTime.MinValue;
    private static readonly TimeSpan TriggerSeekInterval = TimeSpan.FromMilliseconds(400);

    private void OnRightTrigger(double value)
    {
        var now = DateTime.UtcNow;
        if (now - _lastTriggerSeek < TriggerSeekInterval) return;
        _lastTriggerSeek = now;
        ViewModel.SeekForwardCommand.Execute(null);
        ShowTransport();
    }

    private void OnLeftTrigger(double value)
    {
        var now = DateTime.UtcNow;
        if (now - _lastTriggerSeek < TriggerSeekInterval) return;
        _lastTriggerSeek = now;
        ViewModel.SeekBackwardCommand.Execute(null);
        ShowTransport();
    }

    private void ShowTransport()
    {
        TransportOverlay.Opacity = 1;
        _hideTimer?.Start();
    }

    // ═══ Transport Controls ═══

    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        _diagnostics.Log("player-ui", "play-pause-click");
        ViewModel.TogglePlayPauseCommand.Execute(null);
        if (PlayPauseIcon != null)
            PlayPauseIcon.Glyph = ViewModel.IsPlaying ? "\uE769" : "\uE768";
        ShowTransport();
    }

    private void SeekForward_Click(object sender, RoutedEventArgs e) { ViewModel.SeekForwardCommand.Execute(null); ShowTransport(); }
    private void SeekBack_Click(object sender, RoutedEventArgs e) { ViewModel.SeekBackwardCommand.Execute(null); ShowTransport(); }
    private void Rewind_Click(object sender, RoutedEventArgs e) { ViewModel.SeekBackwardCommand.Execute(null); ViewModel.SeekBackwardCommand.Execute(null); ShowTransport(); }
    private void Forward_Click(object sender, RoutedEventArgs e) { ViewModel.SeekForwardCommand.Execute(null); ViewModel.SeekForwardCommand.Execute(null); ShowTransport(); }
    private void Back_Click(object sender, RoutedEventArgs e)
    {
        _diagnostics.Log("player-ui", "back-click");
        ViewModel.StopCommand.Execute(null);
    }

    // ═══ Track Selection ═══

    private void ToggleTrackPanel_Click(object sender, RoutedEventArgs e)
    {
        _diagnostics.Log("player-ui", "track-panel-toggle");
        ViewModel.TrackSelection.ToggleVisibilityCommand.Execute(null);
    }

    private void CloseTrackPanel_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.TrackSelection.HideCommand.Execute(null);
    }

    private void AudioListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.FirstOrDefault() is MediaStream track)
        {
            ViewModel.TrackSelection.SelectedAudioTrack = track;
            ShowTransport();
        }
    }

    private void SubtitleListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems.FirstOrDefault() is MediaStream track)
        {
            ViewModel.TrackSelection.SelectedSubtitleTrack = track;
            ShowTransport();
        }
    }

    // Show transport on any interaction
    protected override void OnPointerMoved(PointerRoutedEventArgs e) => ShowTransport();

    // ═══ Seek Slider ═══

    // While the user drags the slider, gate playback position updates so
    // the thumb doesn't fight the user; the TwoWay binding seeks via
    // ViewModel.OnSliderSecondsChanged.
    private void SeekSlider_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        ViewModel.BeginSeek();
        ShowTransport();
    }

    private void SeekSlider_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        ViewModel.EndSeek();
        ViewModel.ShowTrickplayPreview = false;
    }

    private void SeekSlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        ViewModel.EndSeek();
        ViewModel.ShowTrickplayPreview = false;
    }

    // ═══ Player Settings & Drawer Handlers ═══

    private void Speed_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string speedStr } && double.TryParse(speedStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var speed))
        {
            ViewModel.SetPlaybackSpeedCommand.Execute(speed);
            ShowTransport();
        }
    }

    private void AudioDelayMinus_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AdjustAudioOffsetCommand.Execute(-100);
        ShowTransport();
    }

    private void AudioDelayPlus_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AdjustAudioOffsetCommand.Execute(100);
        ShowTransport();
    }

    private void SubDelayMinus_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AdjustSubtitleOffsetCommand.Execute(-100);
        ShowTransport();
    }

    private void SubDelayPlus_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AdjustSubtitleOffsetCommand.Execute(100);
        ShowTransport();
    }

    private void SearchOnlineSubtitles_Click(object sender, RoutedEventArgs e)
    {
        _ = ViewModel.TrackSelection.SearchRemoteSubtitlesAsync();
        ShowTransport();
    }

    private void DownloadSubtitle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: RemoteSubtitleInfo sub })
        {
            _ = ViewModel.TrackSelection.DownloadSubtitleAsync(sub);
            ShowTransport();
        }
    }

    private void Chapter_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is ChapterInfo chapter)
        {
            ViewModel.SelectChapterCommand.Execute(chapter);
            ShowTransport();
        }
    }
}
