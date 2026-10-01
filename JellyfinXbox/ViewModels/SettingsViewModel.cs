using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyfinClient.Models;
using JellyfinClient.Services;
using JellyfinXbox.Services;
using JellyfinXbox.Views;

namespace JellyfinXbox.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly JellyfinApiClient _api;
    private readonly NavigationService _nav;

    [ObservableProperty] public partial UserSettings Settings { get; set; }
    [ObservableProperty] public partial string ServerUrl { get; set; }
    [ObservableProperty] public partial string ServerName { get; set; }
    [ObservableProperty] public partial string ServerVersion { get; set; }
    [ObservableProperty] public partial string UserName { get; set; }
    [ObservableProperty] public partial int SelectedBitrateIndex { get; set; }
    [ObservableProperty] public partial int SelectedSubtitleModeIndex { get; set; }
    [ObservableProperty] public partial int SubtitleFontSize { get; set; }
    [ObservableProperty] public partial bool AutoPlayNextEpisode { get; set; }
    [ObservableProperty] public partial bool PreferForeignSubtitles { get; set; }

    public ObservableCollection<string> BitrateLabels { get; } = new();
    public ObservableCollection<string> SubtitleModes { get; } = new();

    private readonly long[] _bitrateValues;
    private readonly string[] _subtitleModeValues = { "Default", "On", "Off", "OnlyForced" };

    public SettingsViewModel(JellyfinApiClient api, NavigationService nav)
    {
        _api = api;
        _nav = nav;
        Settings = api.Settings;

        // Populate bitrate options
        foreach (var (label, _) in UserSettings.BitratePresets)
            BitrateLabels.Add(label);
        _bitrateValues = UserSettings.BitratePresets.Select(p => p.Value).ToArray();

        // Populate subtitle modes
        foreach (var mode in _subtitleModeValues)
            SubtitleModes.Add(mode switch
            {
                "Default" => "Default",
                "On" => "Always On",
                "Off" => "Off",
                "OnlyForced" => "Forced Only",
                _ => mode
            });

        LoadFromSettings();
    }

    public void LoadFromSettings()
    {
        var s = _api.Settings;

        // Find matching bitrate index
        SelectedBitrateIndex = Array.FindIndex(_bitrateValues, v => v == s.MaxStreamingBitrate);
        if (SelectedBitrateIndex < 0) SelectedBitrateIndex = 0;

        SelectedSubtitleModeIndex = Array.IndexOf(_subtitleModeValues, s.SubtitleMode);
        if (SelectedSubtitleModeIndex < 0) SelectedSubtitleModeIndex = 0;

        SubtitleFontSize = s.SubtitleFontSize;
        AutoPlayNextEpisode = s.AutoPlayNextEpisode;
        PreferForeignSubtitles = s.PreferForeignSubtitles;

        // Server info
        ServerUrl = _api.ServerUrl ?? "";
        ServerName = _api.ServerInfo?.ServerName ?? "";
        ServerVersion = _api.ServerInfo?.Version ?? "";
        UserName = _api.CurrentUser?.Name ?? "";
    }

    partial void OnSelectedBitrateIndexChanged(int value)
    {
        if (value >= 0 && value < _bitrateValues.Length)
        {
            Settings.MaxStreamingBitrate = _bitrateValues[value];
            ApplySettings();
        }
    }

    partial void OnSelectedSubtitleModeIndexChanged(int value)
    {
        if (value >= 0 && value < _subtitleModeValues.Length)
        {
            Settings.SubtitleMode = _subtitleModeValues[value];
            ApplySettings();
        }
    }

    partial void OnSubtitleFontSizeChanged(int value)
    {
        Settings.SubtitleFontSize = value;
        ApplySettings();
    }

    partial void OnAutoPlayNextEpisodeChanged(bool value)
    {
        Settings.AutoPlayNextEpisode = value;
        ApplySettings();
    }

    partial void OnPreferForeignSubtitlesChanged(bool value)
    {
        Settings.PreferForeignSubtitles = value;
        ApplySettings();
    }

    private void ApplySettings()
    {
        _api.UpdateSettings(Settings);
    }

    [RelayCommand]
    private void GoBack() => _nav.GoBack();

    [RelayCommand]
    private void Logout()
    {
        _api.Logout(keepServerUrl: false);
        _nav.NavigateTo(typeof(LoginPage));
    }

    /// <summary>Return to the login screen to pick a different user
    /// (session is cleared; public users are listed there).</summary>
    [RelayCommand]
    private void SwitchUser()
    {
        _api.Logout(keepServerUrl: true);
        _nav.NavigateTo(typeof(LoginPage));
    }

    [RelayCommand]
    private void OpenDiagnostics() => _nav.NavigateTo(typeof(DebugPage));
}
