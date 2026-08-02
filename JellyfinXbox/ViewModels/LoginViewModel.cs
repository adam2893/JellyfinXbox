using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JellyfinClient.Models;
using JellyfinClient.Services;
using JellyfinXbox.Services;
using Windows.Foundation.Metadata;
using Windows.UI.Xaml.Controls;
using DispatcherQueue = Windows.System.DispatcherQueue;

namespace JellyfinXbox.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly JellyfinApiClient _api;
    private readonly NavigationService _nav;
    private readonly CertificateTrustService _certTrust;
    private readonly DispatcherQueue _dispatcher;

    [ObservableProperty] public partial string ServerUrl { get; set; }
    [ObservableProperty] public partial string Username { get; set; }
    [ObservableProperty] public partial string Password { get; set; }
    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial string? ErrorMessage { get; set; }
    [ObservableProperty] public partial bool ShowPasswordField { get; set; }
    [ObservableProperty] public partial string ServerName { get; set; }

    public ObservableCollection<User> PublicUsers { get; } = new();

    public LoginViewModel(JellyfinApiClient api, NavigationService nav, CertificateTrustService certTrust)
    {
        _api = api;
        _nav = nav;
        _certTrust = certTrust;
        _dispatcher = DispatcherQueue.GetForCurrentThread();
        _certTrust.CertificateRejected += OnCertificateRejected;

        // Former field initializers (partial properties can't carry them).
        ServerUrl = "";
        Username = "";
        Password = "";
        ServerName = "";

        // Prefill the server URL when a previous session was restored.
        if (!string.IsNullOrEmpty(api.ServerUrl))
            ServerUrl = api.ServerUrl;
    }

    /// <summary>
    /// A connection was refused because the server certificate isn't
    /// trusted. Ask the user; if they accept, remember the thumbprint and
    /// retry the connect flow automatically.
    /// </summary>
    private void OnCertificateRejected(string host, string thumbprint, string issuer)
    {
        _dispatcher.TryEnqueue(async () =>
        {
            var dialog = new ContentDialog
            {
                Title = "Untrusted server",
                Content =
                    $"The server at {host} presented a certificate that isn't trusted.\n\n" +
                    $"Issuer: {issuer}\nThumbprint: {thumbprint}\n\n" +
                    "Trust this server for future connections?",
                PrimaryButtonText = "Trust",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary
            };

            // XamlRoot only exists on 10.0.18362+ (min version is 17763).
            if (ApiInformation.IsApiContractPresent("Windows.Foundation.UniversalApiContract", 8))
                dialog.XamlRoot = App.MainWindow.Content.XamlRoot;

            try
            {
                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                {
                    _certTrust.AddTrusted(thumbprint);
                    await ConnectAsync(); // retry with the trusted cert
                }
                else
                {
                    ErrorMessage = "Connection refused: server certificate not trusted.";
                    IsLoading = false;
                }
            }
            finally
            {
                _certTrust.PromptResolved();
            }
        });
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(ServerUrl))
        {
            ErrorMessage = "Enter a server URL.";
            return;
        }

        // Normalize URL
        if (!ServerUrl.StartsWith("http"))
            ServerUrl = "https://" + ServerUrl;

        IsLoading = true;
        _api.SetServerUrl(ServerUrl);

        var info = await _api.GetServerInfoAsync();
        if (info == null)
        {
            ErrorMessage = "Cannot connect to server. Check the URL and try again.";
            IsLoading = false;
            return;
        }

        ServerName = info.ServerName;

        // Fetch public users
        var users = await _api.GetPublicUsersAsync();
        PublicUsers.Clear();
        foreach (var u in users) PublicUsers.Add(u);

        if (PublicUsers.Count == 1)
        {
            Username = PublicUsers[0].Name;
            ShowPasswordField = true;
        }
        else if (PublicUsers.Count > 1)
        {
            ShowPasswordField = true;
        }

        IsLoading = false;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            ErrorMessage = "Enter a username.";
            return;
        }

        ErrorMessage = null;
        IsLoading = true;

        var result = await _api.AuthenticateAsync(Username, Password);
        if (result == null)
        {
            ErrorMessage = "Login failed. Check your credentials.";
            IsLoading = false;
            return;
        }

        IsLoading = false;
        Password = string.Empty;

        // Navigate to home
        _nav.NavigateTo(typeof(Views.HomePage));
    }

    [RelayCommand]
    private void SelectUser(User user)
    {
        Username = user.Name;
        ShowPasswordField = true;
    }
}
