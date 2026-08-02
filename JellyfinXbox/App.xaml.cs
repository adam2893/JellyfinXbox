using Microsoft.Extensions.DependencyInjection;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Windows.UI.Xaml;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.System.Profile;
using JellyfinClient.Services;
using JellyfinXbox.Services;
using JellyfinXbox.ViewModels;

namespace JellyfinXbox;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;
    public static Window MainWindow { get; private set; } = null!;

    public App()
    {
        this.InitializeComponent();
        Services = ConfigureServices();
        Suspending += OnSuspending;

        // 10-foot UI: use the Xbox reveal focus visuals on consoles.
        if (AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Xbox")
        {
            FocusVisualKind = FocusVisualKind.Reveal;
        }
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Session state persistence (server URL, token, user, settings).
        services.AddSingleton<ISessionStore, SessionStore>();

        // User-approved server certificate allowlist (see TLS handler below).
        services.AddSingleton<CertificateTrustService>();

        // Single shared Jellyfin API client. It owns ALL session state
        // (ServerUrl, AccessToken, CurrentUser, Settings), so it MUST be a
        // singleton — transient instances would each start unauthenticated
        // and pages would never see the login performed elsewhere.
        services.AddSingleton<JellyfinApiClient>(sp =>
        {
            var trust = sp.GetRequiredService<CertificateTrustService>();
            var handler = new HttpClientHandler
            {
                // Self-hosted Jellyfin servers commonly use self-signed
                // certs, so we can't require a valid chain. Instead accept
                // only certificates the user explicitly trusted (persisted
                // by thumbprint); anything else is rejected and prompts the
                // user on the login page.
                ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) =>
                {
                    if (errors == SslPolicyErrors.None) return true;

                    if (certificate is X509Certificate2 cert2)
                    {
                        var thumbprint = cert2.Thumbprint;
                        if (trust.IsTrusted(thumbprint)) return true;

                        var host = request?.RequestUri?.Host ?? "?";
                        trust.ReportRejection(host, thumbprint, cert2.Issuer);
                    }
                    return false;
                }
            };
            return new JellyfinApiClient(new HttpClient(handler), sp.GetRequiredService<ISessionStore>());
        });

        // View models
        services.AddTransient<LoginViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<MediaDetailViewModel>();
        services.AddTransient<PlayerViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<ShellViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<QuickConnectViewModel>();

        // Services
        services.AddSingleton<NavigationService>();

        // Xbox controller input (poll-based; started by the Shell).
        services.AddSingleton<GamepadService>();

        // Shell (hosts all pages)
        services.AddTransient<ShellPage>();

        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = Window.Current;
        MainWindow.Content = Services.GetRequiredService<ShellPage>();
        MainWindow.Activate();
    }

    /// <summary>
    /// Session state (token, user, settings) is persisted to LocalSettings
    /// incrementally on every change, so suspension needs no extra flush —
    /// this hook is kept for future work (e.g. playback reporting).
    /// </summary>
    private static void OnSuspending(object sender, SuspendingEventArgs e)
    {
        var deferral = e.SuspendingOperation.GetDeferral();
        deferral.Complete();
    }
}
