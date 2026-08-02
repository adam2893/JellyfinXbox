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

    [ObservableProperty] private UserSettings _settings;
    [ObservableProperty] private string _serverUrl = "";
    [ObservableProperty] private string _serverName = "";
    [ObservableProperty] private string _serverVersion = "";
    [ObservableProperty] private string _userName = "";
    [ObservableProperty] private int _selectedBitrateIndex;
    [ObservableProperty] private int _selectedSubtitleModeIndex;
    [ObservableProperty] private int _subtitleFontSize;
    [ObservableProperty] private bool _autoPlayNextEpisode;
    [ObservableProperty] private bool _preferForeignSubtitles;

    public ObservableCollection<string> BitrateLabels { get; } = new();
    public ObservableCollection<string> SubtitleModes { get; } = new();

    private readonly long[] _bitrateValues;
    private readonly string[] _subtitleModeValues = { "Default", "On", "Off", "OnlyForced" };

    public SettingsViewModel(JellyfinApiClient api, NavigationService nav)
    {
        _api = api;
        _nav = nav;
        _settings = api.Settings;

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
            _settings.MaxStreamingBitrate = _bitrateValues[value];
            ApplySettings();
        }
    }

    partial void OnSelectedSubtitleModeIndexChanged(int value)
    {
        if (value >= 0 && value < _subtitleModeValues.Length)
        {
            _settings.SubtitleMode = _subtitleModeValues[value];
            ApplySettings();
        }
    }

    partial void OnSubtitleFontSizeChanged(int value)
    {
        _settings.SubtitleFontSize = value;
        ApplySettings();
    }

    partial void OnAutoPlayNextEpisodeChanged(bool value)
    {
        _settings.AutoPlayNextEpisode = value;
        ApplySettings();
    }

    partial void OnPreferForeignSubtitlesChanged(bool value)
    {
        _settings.PreferForeignSubtitles = value;
        ApplySettings();
    }

    private void ApplySettings()
    {
        _api.UpdateSettings(_settings);
    }

    [RelayCommand]
    private void GoBack() => _nav.GoBack();

    [RelayCommand]
    private void Logout()
    {
        _api.Logout();
        _nav.NavigateTo(typeof(LoginPage));
    }
}
