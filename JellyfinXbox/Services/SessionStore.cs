using JellyfinClient.Services;
using Windows.Storage;

namespace JellyfinXbox.Services;

/// <summary>
/// Persists Jellyfin session state (server URL, token, user, settings) in
/// UWP LocalSettings so the app resumes authenticated across launches and
/// suspension.
/// </summary>
public sealed class SessionStore : ISessionStore
{
    private readonly ApplicationDataContainer _settings = ApplicationData.Current.LocalSettings;

    public string? Get(string key) =>
        _settings.Values.TryGetValue(key, out var value) ? value as string : null;

    public void Set(string key, string? value)
    {
        if (value is null)
            _settings.Values.Remove(key);
        else
            _settings.Values[key] = value;
    }
}
