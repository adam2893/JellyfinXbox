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
        StartupDiagnostics.BeginLaunch();
        try
        {
            StartupDiagnostics.Stage("app.initialize-component");
            this.InitializeComponent();
            StartupDiagnostics.Stage("app.resources");
            StartupDiagnostics.Stage("app.di");
            Services = ConfigureServices();
            Suspending += OnSuspending;
            StartupDiagnostics.Stage("app.focus-configuration");
            if (AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Xbox")
                FocusVisualKind = FocusVisualKind.Reveal;
            StartupDiagnostics.Stage("app.constructor-complete");
        }
        catch (System.Exception ex)
        {
            StartupDiagnostics.Exception("app.constructor", ex);
            StartupFailure = ex;
        }

        UnhandledException += (_, e) => StartupDiagnostics.Exception("xaml.unhandled", e.Exception);

    }

    internal static System.Exception? StartupFailure { get; private set; }

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
            HttpClientHandler CreateHandler()
            {
                return new HttpClientHandler
                {
                    // Self-hosted Jellyfin servers commonly use self-signed
                    // certs, so we can't require a valid chain. Instead accept
                    // only certificates the user explicitly trusted.
                    ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) =>
                    {
                        if (errors == SslPolicyErrors.None) return true;
                        if (certificate is X509Certificate2 cert2)
                        {
                            var thumbprint = cert2.Thumbprint;
                            var host = request?.RequestUri?.Host ?? "?";
                            if (trust.IsTrusted(host, thumbprint)) return true;
                            trust.ReportRejection(host, thumbprint, cert2.Issuer);
                        }
                        return false;
                    }
                };
            }
            HttpClient CreateClient() => new(CreateHandler());
            return new JellyfinApiClient(CreateClient(), sp.GetRequiredService<ISessionStore>(), CreateClient);
        });

        // View models
        services.AddTransient<LoginViewModel>(sp => new LoginViewModel(
            sp.GetRequiredService<JellyfinApiClient>(),
            sp.GetRequiredService<NavigationService>(),
            sp.GetRequiredService<CertificateTrustService>()));
        services.AddTransient<HomeViewModel>(sp => new HomeViewModel(
            sp.GetRequiredService<JellyfinApiClient>(),
            sp.GetRequiredService<NavigationService>()));
        services.AddTransient<MediaDetailViewModel>(sp => new MediaDetailViewModel(
            sp.GetRequiredService<JellyfinApiClient>(),
            sp.GetRequiredService<NavigationService>()));
        services.AddTransient<PlayerViewModel>(sp => new PlayerViewModel(
            sp.GetRequiredService<JellyfinApiClient>(),
            sp.GetRequiredService<NavigationService>()));
        services.AddTransient<SearchViewModel>(sp => new SearchViewModel(
            sp.GetRequiredService<JellyfinApiClient>(),
            sp.GetRequiredService<NavigationService>()));
        services.AddTransient<ShellViewModel>(sp => new ShellViewModel(
            sp.GetRequiredService<NavigationService>()));
        services.AddTransient<SettingsViewModel>(sp => new SettingsViewModel(
            sp.GetRequiredService<JellyfinApiClient>(),
            sp.GetRequiredService<NavigationService>()));
        services.AddTransient<LibraryViewModel>(sp => new LibraryViewModel(
            sp.GetRequiredService<JellyfinApiClient>(),
            sp.GetRequiredService<NavigationService>()));
        services.AddTransient<QuickConnectViewModel>(sp => new QuickConnectViewModel(
            sp.GetRequiredService<JellyfinApiClient>(),
            sp.GetRequiredService<NavigationService>()));
        services.AddTransient<DebugViewModel>(sp => new DebugViewModel(
            sp.GetRequiredService<JellyfinApiClient>()));

        // Services
        services.AddSingleton<NavigationService>(sp => new NavigationService());

        // Xbox controller input (poll-based; started by the Shell).
        services.AddSingleton<GamepadService>(sp => new GamepadService());

        // Shell (hosts all pages)
        services.AddTransient<ShellPage>(_ => new ShellPage());

        return services.BuildServiceProvider();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (StartupFailure == null) StartupDiagnostics.Stage("app.on-launched");
        MainWindow = Window.Current;
        try
        {
            if (StartupFailure != null) { ShowRecovery("APP-INIT", StartupFailure); return; }
            StartupDiagnostics.Stage("shell.construct");
            MainWindow.Content = new ShellPage();
            StartupDiagnostics.Stage("shell.assigned");
            MainWindow.Activate();
        }
        catch (System.Exception ex)
        {
            StartupDiagnostics.Exception("app.on-launched", ex);
            ShowRecovery("APP-LAUNCH", ex);
        }
    }

    internal static void ShowRecovery(string code, System.Exception ex)
    {
        StartupDiagnostics.Exception(code, ex);
        RecoveryScreen.Show(MainWindow, "Startup failed", $"Stage: {StartupDiagnostics.LastStage}\nCode: {code}\n{ex.GetType().Name} (0x{ex.HResult:X8})");
    }

    private static class RecoveryScreen
    {
        public static void Show(Window window, string title, string detail)
        {
            try
            {
                var panel = new Windows.UI.Xaml.Controls.StackPanel { HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Center, VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Center };
                var foreground = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.White);
                panel.Children.Add(new Windows.UI.Xaml.Controls.TextBlock { Text = title, Foreground = foreground, FontSize = 32, Margin = new Windows.UI.Xaml.Thickness(20) });
                panel.Children.Add(new Windows.UI.Xaml.Controls.TextBlock { Text = detail + "\nPhotograph this screen for support.\nLocal log: startup-diagnostics.log", Foreground = foreground, TextWrapping = TextWrapping.Wrap, FontSize = 22, Margin = new Windows.UI.Xaml.Thickness(20) });
                window.Content = new Windows.UI.Xaml.Controls.Grid { Background = new Windows.UI.Xaml.Media.SolidColorBrush(Windows.UI.Colors.Black), Children = { panel } }; window.Activate();
            }
            catch { }
        }
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
