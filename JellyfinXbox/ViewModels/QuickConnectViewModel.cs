using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyfinClient.Services;
using JellyfinXbox.Services;
using JellyfinXbox.Views;

namespace JellyfinXbox.ViewModels;

public partial class QuickConnectViewModel : ObservableObject, IDisposable
{
    private readonly JellyfinApiClient _api;
    private readonly NavigationService _nav;
    private CancellationTokenSource? _pollCts;

    [ObservableProperty] public partial bool IsActive { get; set; }
    [ObservableProperty] public partial string Code { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; }
    [ObservableProperty] public partial bool IsSuccess { get; set; }
    [ObservableProperty] public partial bool IsError { get; set; }
    [ObservableProperty] public partial string ErrorMessage { get; set; }
    [ObservableProperty] public partial string ConnectedUserName { get; set; }

    public QuickConnectViewModel(JellyfinApiClient api, NavigationService nav)
    {
        _api = api;
        _nav = nav;
        StatusMessage = "Waiting for connection..."; // was field initializer
    }

    [RelayCommand]
    private async Task StartQuickConnectAsync()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
        IsActive = true;
        IsSuccess = false;
        IsError = false;
        ErrorMessage = "";
        ConnectedUserName = "";

        // Init Quick Connect
        var initResult = await _api.QuickConnectInitAsync();
        if (!IsActive) return;
        if (initResult == null || string.IsNullOrWhiteSpace(initResult.Secret) || string.IsNullOrWhiteSpace(initResult.Code))
        {
            IsError = true;
            ErrorMessage = "Quick Connect is not available on this server. Enable it in Jellyfin server settings.";
            IsActive = false;
            return;
        }

        Code = initResult.Code;
        StatusMessage = $"Enter code {Code} on another device";
        Debug.WriteLine("[QuickConnect] Initiated");

        // Poll for authorization
        _pollCts = new CancellationTokenSource();
        try
        {
            await PollForAuthorization(initResult.Secret, _pollCts.Token);
        }
        catch (OperationCanceledException)
        {
            if (IsActive)
                StatusMessage = "Connection timed out. Try again.";
        }
    }

    private async Task PollForAuthorization(string secret, CancellationToken ct)
    {
        int attempts = 0;
        const int maxAttempts = 120; // 2 minutes at 1s interval

        while (!ct.IsCancellationRequested && attempts < maxAttempts)
        {
            await Task.Delay(1000, ct);
            attempts++;

            var authResult = await _api.QuickConnectCheckAuthAsync(secret);
            if (authResult == null) continue;

            if (authResult.Authenticated)
            {
                StatusMessage = $"Authorized! Connecting...";

                // Exchange the secret for an access token
                // Jellyfin may return the exchange secret in the authorize
                // response. Prefer it when present; older servers leave it
                // empty and require the original initiate secret.
                var exchangeSecret = !string.IsNullOrWhiteSpace(authResult.Secret)
                    ? authResult.Secret
                    : secret;
                var connectResult = await _api.QuickConnectConnectAsync(exchangeSecret);
                if (connectResult != null)
                {
                    IsSuccess = true;
                    ConnectedUserName = connectResult.User.Name;
                    StatusMessage = $"Connected as {ConnectedUserName}!";
                    IsActive = false;
                    Debug.WriteLine($"[QuickConnect] Connected as {ConnectedUserName}");

                    // Navigate to home after a short delay
                    await Task.Delay(1500, CancellationToken.None);
                    _nav.NavigateTo(typeof(HomePage));
                }
                else
                {
                    IsError = true;
                    IsActive = false;
                    ErrorMessage = "Authorization succeeded but connection failed. Try again.";
                }
                return;
            }
        }

        // Timed out
        if (IsActive)
        {
            IsError = true;
            StatusMessage = "Connection timed out.";
            ErrorMessage = "No device responded within 2 minutes. Try again.";
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        _pollCts?.Cancel();
        IsActive = false;
        Code = "";
        StatusMessage = "Waiting for connection...";
    }

    [RelayCommand]
    private void GoBack()
    {
        _pollCts?.Cancel();
        _nav.GoBack();
    }

    public void Dispose()
    {
        _pollCts?.Cancel();
        _pollCts?.Dispose();
    }
}
