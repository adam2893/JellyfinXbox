using Microsoft.Extensions.DependencyInjection;
using Windows.UI.Xaml;
using Windows.ApplicationModel.Activation;
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
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // HTTP client for Jellyfin API
        services.AddHttpClient<JellyfinApiClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
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
}
