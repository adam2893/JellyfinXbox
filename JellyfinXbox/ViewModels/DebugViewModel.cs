using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyfinClient.Models;
using JellyfinClient.Services;

namespace JellyfinXbox.ViewModels;

public partial class DebugViewModel : ObservableObject
{
    private readonly JellyfinApiClient _api;

    [ObservableProperty] public partial bool IsRunning { get; set; }

    public ObservableCollection<SelfCheckResult> Results { get; } = new();

    public DebugViewModel(JellyfinApiClient api)
    {
        _api = api;
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        if (IsRunning) return;

        IsRunning = true;
        Results.Clear();
        try
        {
            var results = await _api.RunSelfCheckAsync();
            foreach (var r in results) Results.Add(r);
        }
        catch (Exception ex)
        {
            Results.Add(new SelfCheckResult
            {
                Name = "Self-check failed",
                Ok = false,
                Detail = ex.Message
            });
        }
        finally
        {
            IsRunning = false;
        }
    }
}
