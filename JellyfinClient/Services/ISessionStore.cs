namespace JellyfinClient.Services;

/// <summary>
/// Minimal key-value persistence for session state (server URL, access token,
/// current user, settings). Implemented by the app host (e.g. UWP
/// ApplicationData) so this library stays portable.
/// </summary>
public interface ISessionStore
{
    string? Get(string key);
    void Set(string key, string? value);
}
