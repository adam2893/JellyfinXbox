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
    [ObservableProperty] public partial string ReportText { get; set; } = "";
    public string AppVersion => "1.0.0.0";
    public string SupportUrl => "https://github.com/adam2893/JellyfinXbox/issues";

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
            ReportText = BuildReport();
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

    private string BuildReport()
    {
        // Excludes access tokens and credentials. Self-check details may include
        // server/user/media metadata, so the user must review before sharing.
        var host = _api.ServerUrl == null ? "not configured" : Uri.TryCreate(_api.ServerUrl, UriKind.Absolute, out var u) ? u.Host : "invalid";
        var lines = new List<string> { $"XboxJellyfish diagnostics", $"App version: {AppVersion}", $"OS: {Environment.OSVersion}", $"Server host: {host}", $"Authenticated: {_api.IsAuthenticated}", $"Generated (UTC): {DateTimeOffset.UtcNow:O}" };
        lines.AddRange(Results.Select(r => $"{r.Name}: {(r.Ok ? "PASS" : "FAIL")} ({r.ElapsedMs} ms) - {r.Detail}"));
        return string.Join(Environment.NewLine, lines);
    }

    [RelayCommand]
    private async Task CopyReportAsync()
    {
        if (string.IsNullOrWhiteSpace(ReportText)) ReportText = BuildReport();
        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
        package.SetText(ReportText);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        await Task.CompletedTask;
    }
}
