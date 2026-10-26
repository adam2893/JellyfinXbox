using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyfinClient;
using JellyfinClient.Models;
using JellyfinClient.Services;
using JellyfinXbox.Services;

namespace JellyfinXbox.ViewModels;

public partial class DebugViewModel : ObservableObject
{
    private readonly JellyfinApiClient _api;
    private readonly DiagnosticsLogService _diagnostics;

    [ObservableProperty] public partial bool IsRunning { get; set; }
    [ObservableProperty] public partial string LogText { get; set; } = "";
    [ObservableProperty] public partial string LogStatus { get; set; } = "";

    public ObservableCollection<SelfCheckResult> Results { get; } = new();
    [ObservableProperty] public partial string ReportText { get; set; } = "";
    public string AppVersion => ClientInfo.Version;
    public string VersionLabel => $"Version {AppVersion} · Support: github.com/adam2893/JellyfinXbox/issues";
    public string SupportUrl => "https://github.com/adam2893/JellyfinXbox/issues";
    public string LogFileName => DiagnosticsLogService.FileName;
    public string LogPathLabel => $"Local file: {_diagnostics.FilePath}";

    public DebugViewModel(JellyfinApiClient api, DiagnosticsLogService diagnostics)
    {
        _api = api;
        _diagnostics = diagnostics;
        RefreshLog();
    }

    [RelayCommand]
    private async Task RunAsync()
    {
        if (IsRunning) return;

        _diagnostics.Log("diagnostics", "self-check-started");
        IsRunning = true;
        Results.Clear();
        try
        {
            var results = await _api.RunSelfCheckAsync();
            foreach (var r in results) Results.Add(r);
            ReportText = BuildReport();
            RefreshLog();
        }
        catch (Exception ex)
        {
            _diagnostics.LogException("diagnostics", "self-check-failed", ex);
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

    [RelayCommand]
    private async Task CopyLogAsync()
    {
        RefreshLog();
        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
        package.SetText(LogText);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
        LogStatus = "Full event log copied to the Xbox clipboard.";
        _diagnostics.Log("diagnostics", "log-copied");
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void ClearLog()
    {
        _diagnostics.Clear();
        _diagnostics.Log("diagnostics", "log-cleared");
        RefreshLog();
        LogStatus = "Log cleared. Reproduce the problem, then return here.";
    }

    public void RefreshLog() => LogText = _diagnostics.ReadText();
}
