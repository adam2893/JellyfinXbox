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
    private DispatcherTimer? _hideTimer;

    // Stored as fields so += / -= in OnNavigatedTo/From match exactly.
    private readonly Action _viewPressedHandler;
    private readonly Action _aPressedHandler;
    private readonly Action _bPressedHandler;

    public PlayerPage()
    {
        ViewModel = App.Services.GetRequiredService<PlayerViewModel>();
        _gamepad = App.Services.GetRequiredService<GamepadService>();
        _viewPressedHandler = () => ToggleTrackPanel_Click(this, null!);
        _aPressedHandler = () => PlayPause_Click(this, null!);
        _bPressedHandler = () => Back_Click(this, null!);
        InitializeComponent();

        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _hideTimer.Tick += (s, e) =>
        {
            TransportOverlay.Opacity = 0;
            _hideTimer.Stop();
        };
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        // Controller shortcuts (D-pad focus + A activation are native):
        //   Right/Left trigger → seek; View → track panel; A → play/pause;
        //   B → stop & back; Menu → keep transport visible.
        _gamepad.Start();
        _gamepad.OnRightTrigger += OnRightTrigger;
        _gamepad.OnLeftTrigger += OnLeftTrigger;
        _gamepad.OnViewPressed += _viewPressedHandler;
        _gamepad.OnAPressed += _aPressedHandler;
        _gamepad.OnBPressed += _bPressedHandler;
        _gamepad.OnMenuPressed += ShowTransport;

        if (e.Parameter is string itemId)
        {
            var player = ViewModel.Player;
            if (player != null)
                MediaPlayerElement.SetMediaPlayer(player);

            await ViewModel.LoadAsync(itemId);
        }
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _gamepad.OnRightTrigger -= OnRightTrigger;
        _gamepad.OnLeftTrigger -= OnLeftTrigger;
        _gamepad.OnViewPressed -= _viewPressedHandler;
        _gamepad.OnAPressed -= _aPressedHandler;
        _gamepad.OnBPressed -= _bPressedHandler;
        _gamepad.OnMenuPressed -= ShowTransport;
        ViewModel.Dispose();
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
        ViewModel.TogglePlayPauseCommand.Execute(null);
        PlayPauseIcon.Glyph = ViewModel.IsPlaying ? "\uE769" : "\uE768";
        ShowTransport();
    }

    private void SeekForward_Click(object sender, RoutedEventArgs e) { ViewModel.SeekForwardCommand.Execute(null); ShowTransport(); }
    private void SeekBack_Click(object sender, RoutedEventArgs e) { ViewModel.SeekBackwardCommand.Execute(null); ShowTransport(); }
    private void Rewind_Click(object sender, RoutedEventArgs e) { ViewModel.SeekBackwardCommand.Execute(null); ViewModel.SeekBackwardCommand.Execute(null); ShowTransport(); }
    private void Forward_Click(object sender, RoutedEventArgs e) { ViewModel.SeekForwardCommand.Execute(null); ViewModel.SeekForwardCommand.Execute(null); ShowTransport(); }
    private void Back_Click(object sender, RoutedEventArgs e) => ViewModel.StopCommand.Execute(null);

    // ═══ Track Selection ═══

    private void ToggleTrackPanel_Click(object sender, RoutedEventArgs e)
    {
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
    }

    private void SeekSlider_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        ViewModel.EndSeek();
    }
}
