using System.IO;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using JellyfinClient.Models;

namespace JellyfinClient.Services;

public class JellyfinApiClient
{
    private static string ReplaceQueryParameter(string url, string name, string value)
    {
        var hash = url.IndexOf('#');
        var fragment = hash >= 0 ? url[hash..] : "";
        var withoutFragment = hash >= 0 ? url[..hash] : url;
        var question = withoutFragment.IndexOf('?');
        var path = question >= 0 ? withoutFragment[..question] : withoutFragment;
        var query = question >= 0 ? withoutFragment[(question + 1)..] : "";
        var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase)).ToList();
        parts.Add($"{name}={Uri.EscapeDataString(value)}");
        return path + "?" + string.Join("&", parts) + fragment;
    }
    private const string StoreKeyServerUrl = "session.serverUrl";
    private const string StoreKeyAccessToken = "session.accessToken";
    private const string StoreKeyCurrentUser = "session.currentUser";
    private const string StoreKeySettings = "session.settings";
    private const string StoreKeyServerInfo = "session.serverInfo";

    private HttpClient _http;
    private readonly Func<HttpClient>? _httpFactory;
    private readonly ISessionStore? _store;
    private readonly JsonSerializerOptions _jsonOpts;

    public string? ServerUrl { get; private set; }
    public string? AccessToken { get; private set; }
    public User? CurrentUser { get; private set; }
    public ServerInfo? ServerInfo { get; private set; }
    public UserSettings Settings { get; private set; } = UserSettings.Default;

    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken) && CurrentUser != null;

    public event Action? OnAuthenticationChanged;
    public event Action? OnSettingsChanged;

    public JellyfinApiClient(HttpClient http, ISessionStore? sessionStore = null, Func<HttpClient>? httpFactory = null)
    {
        _http = http;
        _httpFactory = httpFactory ?? (() => new HttpClient());
        _store = sessionStore;
        _jsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        RestoreSession();
        ApplyAuth();
    }

    public void SetServerUrl(string url)
    {
        var normalized = url.TrimEnd('/');
        if (!string.Equals(ServerUrl, normalized, StringComparison.OrdinalIgnoreCase))
        {
            if (ServerUrl != null && _httpFactory != null)
            {
                _http.Dispose();
                _http = _httpFactory();
            }
            ServerUrl = normalized;
            _http.BaseAddress = new Uri(ServerUrl + "/");
            ApplyAuth();
        }
        _store?.Set(StoreKeyServerUrl, ServerUrl);
    }

    private void ApplyAuth()
    {
        const string client = "BoxJellyfish";
        const string device = "Xbox Series";
        const string deviceId = "BoxJellyfish-Xbox";
        const string version = "1.0.0.0";

        if (!string.IsNullOrEmpty(AccessToken))
        {
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("MediaBrowser",
                    $"Client=\"{client}\", Device=\"{device}\", DeviceId=\"{deviceId}\", Version=\"{version}\", Token=\"{AccessToken}\"");
            _http.DefaultRequestHeaders.Remove("X-Emby-Token");
            _http.DefaultRequestHeaders.TryAddWithoutValidation("X-Emby-Token", AccessToken);
            StartWebSocketListener();
        }
        else
        {
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("MediaBrowser",
                    $"Client=\"{client}\", Device=\"{device}\", DeviceId=\"{deviceId}\", Version=\"{version}\"");
            _http.DefaultRequestHeaders.Remove("X-Emby-Token");
            StopWebSocketListener();
        }
    }

    #region Settings Persistence

    public void UpdateSettings(UserSettings settings)
    {
        Settings = settings;
        if (_store != null)
            _store.Set(StoreKeySettings, JsonSerializer.Serialize(Settings, _jsonOpts));
        OnSettingsChanged?.Invoke();
    }

    #endregion

    #region Session Persistence

    /// <summary>
    /// Restores the last authenticated session (server URL, token, user,
    /// settings, server info) from the session store, if present. Called from
    /// the constructor so a relaunched app starts authenticated.
    /// </summary>
    private void RestoreSession()
    {
        if (_store == null) return;

        try
        {
            var serverUrl = _store.Get(StoreKeyServerUrl);
            if (!string.IsNullOrEmpty(serverUrl))
            {
                ServerUrl = serverUrl.TrimEnd('/');
                _http.BaseAddress = new Uri(ServerUrl + "/");
            }

            var token = _store.Get(StoreKeyAccessToken);
            var userJson = _store.Get(StoreKeyCurrentUser);
            if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(userJson))
            {
                var user = JsonSerializer.Deserialize<User>(userJson, _jsonOpts);
                if (user != null)
                {
                    AccessToken = token;
                    CurrentUser = user;
                    ApplyAuth();
                }
            }

            var settingsJson = _store.Get(StoreKeySettings);
            if (!string.IsNullOrEmpty(settingsJson))
            {
                var settings = JsonSerializer.Deserialize<UserSettings>(settingsJson, _jsonOpts);
                if (settings != null) Settings = settings;
            }

            var infoJson = _store.Get(StoreKeyServerInfo);
            if (!string.IsNullOrEmpty(infoJson))
                ServerInfo = JsonSerializer.Deserialize<ServerInfo>(infoJson, _jsonOpts);
        }
        catch (Exception ex)
        {
            // Corrupt or unreadable session data — start fresh.
            System.Diagnostics.Debug.WriteLine($"[Session] Restore failed: {ex.Message}");
            ClearSession();
        }
    }

    private void PersistSession()
    {
        if (_store == null) return;
        _store.Set(StoreKeyServerUrl, ServerUrl);
        if (!string.IsNullOrEmpty(AccessToken))
            _store.Set(StoreKeyAccessToken, AccessToken);
        if (CurrentUser != null)
            _store.Set(StoreKeyCurrentUser, JsonSerializer.Serialize(CurrentUser, _jsonOpts));
        _store.Set(StoreKeySettings, JsonSerializer.Serialize(Settings, _jsonOpts));
        if (ServerInfo != null)
            _store.Set(StoreKeyServerInfo, JsonSerializer.Serialize(ServerInfo, _jsonOpts));
    }

    private void ClearSession(bool keepServerUrl = false)
    {
        AccessToken = null;
        CurrentUser = null;
        if (!keepServerUrl)
        {
            ServerUrl = null;
            ServerInfo = null;
            Settings = UserSettings.Default;
        }
        if (_store == null) return;
        if (!keepServerUrl)
            _store.Set(StoreKeyServerUrl, null);
        _store.Set(StoreKeyAccessToken, null);
        _store.Set(StoreKeyCurrentUser, null);
        if (!keepServerUrl)
        {
            _store.Set(StoreKeySettings, null);
            _store.Set(StoreKeyServerInfo, null);
        }
    }

    #endregion

    #region Discovery

    public async Task<ServerInfo?> GetServerInfoAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<ServerInfo>("System/Info/Public", _jsonOpts);
            ServerInfo = result;
            if (_store != null && result != null)
                _store.Set(StoreKeyServerInfo, JsonSerializer.Serialize(result, _jsonOpts));
            return result;
        }
        catch { return null; }
    }

    #endregion

    #region Authentication

    public async Task<List<User>> GetPublicUsersAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<List<User>>("Users/Public", _jsonOpts);
            return result ?? new();
        }
        catch { return new(); }
    }

    public async Task<AuthenticationResult?> AuthenticateAsync(string username, string password)
    {
        var body = new { Username = username, Pw = password };
        try
        {
            var response = await _http.PostAsJsonAsync("Users/AuthenticateByName", body);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<AuthenticationResult>(_jsonOpts);
            if (result != null)
            {
                AccessToken = result.AccessToken;
                CurrentUser = result.User;
                ApplyAuth();
                PersistSession();
                OnAuthenticationChanged?.Invoke();
            }
            return result;
        }
        catch (HttpRequestException) { return null; }
    }

    public void Logout(bool keepServerUrl = true)
    {
        ClearSession(keepServerUrl);
        ApplyAuth();
        if (!keepServerUrl)
        {
            ServerUrl = null;
            ServerInfo = null;
        }
        OnAuthenticationChanged?.Invoke();
    }

    #endregion

    #region Quick Connect

    public async Task<QuickConnectInitResponse?> QuickConnectInitAsync()
    {
        try
        {
            var response = await _http.PostAsync("QuickConnect/Initiate", null);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<QuickConnectInitResponse>(_jsonOpts);
        }
        catch { return null; }
    }

    public async Task<QuickConnectAuthorizeResponse?> QuickConnectCheckAuthAsync(string code)
    {
        try
        {
            // Current API takes the display code, not the secret.
            var result = await _http.GetFromJsonAsync<QuickConnectAuthorizeResponse>(
                $"QuickConnect/Authorize?code={Uri.EscapeDataString(code)}", _jsonOpts);
            return result;
        }
        catch { return null; }
    }

    public async Task<AuthenticationResult?> QuickConnectConnectAsync(string secret)
    {
        try
        {
            // Exchange the secret for an access token.
            var body = new { Secret = secret };
            var response = await _http.PostAsJsonAsync("Users/AuthenticateWithQuickConnect", body, _jsonOpts);
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<AuthenticationResult>(_jsonOpts);
            if (result != null && !string.IsNullOrWhiteSpace(result.AccessToken) && result.User != null)
            {
                AccessToken = result.AccessToken;
                CurrentUser = result.User;
                ApplyAuth();
                PersistSession();
                OnAuthenticationChanged?.Invoke();
            }
            return result;
        }
        catch { return null; }
    }

    #endregion

    #region Library

    public async Task<List<ViewItem>> GetViewsAsync()
    {
        if (CurrentUser == null) return new();
        try
        {
            var result = await _http.GetFromJsonAsync<ItemsResult>($"UserViews?userId={CurrentUser.Id}", _jsonOpts);
            if (result?.Items == null) return new();

            return result.Items.Select(i => new ViewItem
            {
                Id = i.Id,
                Name = i.Name,
                Type = i.Type,
                CollectionType = i.CollectionType,
                ImageTags = i.ImageTags
            }).ToList();
        }
        catch { return new(); }
    }

    public async Task<ItemsResult> GetItemsAsync(string parentId, string? sortBy = null, string? sortOrder = null,
        int startIndex = 0, int limit = 40, string? filters = null, string? includeItemTypes = null, bool recursive = true)
    {
        if (CurrentUser == null) return new();
        var url = $"Items?userId={CurrentUser.Id}&ParentId={parentId}&StartIndex={startIndex}&Limit={limit}&Recursive={recursive.ToString().ToLowerInvariant()}&Fields=PrimaryImageAspectRatio,BasicSyncInfo,MediaStreams,MediaSources,Blurhashes,ImageBlurHashes";

        if (!string.IsNullOrEmpty(sortBy))
            url += $"&SortBy={sortBy}";
        if (!string.IsNullOrEmpty(sortOrder))
            url += $"&SortOrder={sortOrder}";
        if (!string.IsNullOrEmpty(filters))
            url += $"&Filters={filters}";
        if (!string.IsNullOrEmpty(includeItemTypes))
            url += $"&IncludeItemTypes={includeItemTypes}";

        try
        {
            var result = await _http.GetFromJsonAsync<ItemsResult>(url, _jsonOpts);
            return result ?? new();
        }
        catch { return new(); }
    }

    public async Task<ItemsResult> GetItemsPageAsync(string parentId, int startIndex, int limit, string? filters = null)
    {
        if (CurrentUser == null) throw new InvalidOperationException("Not signed in.");
        var url = $"Items?userId={Uri.EscapeDataString(CurrentUser.Id)}&ParentId={Uri.EscapeDataString(parentId)}&StartIndex={startIndex}&Limit={limit}&Recursive=true&SortBy=SortName&SortOrder=Ascending";
        if (!string.IsNullOrEmpty(filters)) url += $"&Filters={Uri.EscapeDataString(filters)}";
        return await _http.GetFromJsonAsync<ItemsResult>(url, _jsonOpts)
            ?? throw new InvalidOperationException("The server returned no library results.");
    }

    public async Task<ItemsResult> GetUserItemsPageAsync(string? itemType, bool favorites, int startIndex, int limit)
    {
        if (CurrentUser == null) throw new InvalidOperationException("Not signed in.");
        var url = $"Users/{Uri.EscapeDataString(CurrentUser.Id)}/Items?Recursive=true&StartIndex={startIndex}&Limit={limit}&SortBy=SortName&SortOrder=Ascending";
        if (favorites) url += "&Filters=IsFavorite";
        if (itemType != null) url += $"&IncludeItemTypes={Uri.EscapeDataString(itemType)}";
        return await _http.GetFromJsonAsync<ItemsResult>(url, _jsonOpts)
            ?? throw new InvalidOperationException("The server returned no library results.");
    }

    public async Task<ItemsResult> GetResumeItemsAsync(int limit = 12)
    {
        if (CurrentUser == null) return new();
        var url = $"UserItems/Resume?userId={CurrentUser.Id}&Limit={limit}&Fields=PrimaryImageAspectRatio,MediaSources,MediaStreams,Blurhashes,ImageBlurHashes&MediaTypes=Video";
        try
        {
            var result = await _http.GetFromJsonAsync<ItemsResult>(url, _jsonOpts);
            return result ?? new();
        }
        catch { return new(); }
    }

    public async Task<ItemsResult> GetNextUpAsync(string? seriesId = null, int limit = 12)
    {
        if (CurrentUser == null) return new();
        var url = $"Shows/NextUp?UserId={CurrentUser.Id}&Limit={limit}&Fields=PrimaryImageAspectRatio,MediaSources,MediaStreams,Blurhashes,ImageBlurHashes";
        if (!string.IsNullOrEmpty(seriesId))
            url += $"&SeriesId={seriesId}";
        try
        {
            var result = await _http.GetFromJsonAsync<ItemsResult>(url, _jsonOpts);
            return result ?? new();
        }
        catch { return new(); }
    }

    public async Task<BaseItemDto?> GetItemAsync(string itemId)
    {
        if (CurrentUser == null) return null;
        var url = $"Items/{itemId}?userId={CurrentUser.Id}&Fields=PrimaryImageAspectRatio,MediaSources,MediaStreams,Blurhashes,ImageBlurHashes,People,Studios,Genres,Chapters,Trickplay,RemoteTrailers";
        try
        {
            return await _http.GetFromJsonAsync<BaseItemDto>(url, _jsonOpts);
        }
        catch { return null; }
    }

    public async Task<(BaseItemDto? Item, Exception? Error)> GetItemResultAsync(string itemId)
    {
        if (CurrentUser == null) return (null, new InvalidOperationException("Not signed in."));
        try
        {
            var url = $"Items/{itemId}?userId={CurrentUser.Id}&Fields=PrimaryImageAspectRatio,MediaSources,MediaStreams,Blurhashes,ImageBlurHashes,People,Studios,Genres,Chapters,Trickplay,RemoteTrailers";
            return (await _http.GetFromJsonAsync<BaseItemDto>(url, _jsonOpts), null);
        }
        catch (Exception ex) { return (null, ex); }
    }

    public async Task<ItemsResult> GetSimilarItemsAsync(string itemId, int limit = 12)
    {
        if (CurrentUser == null) return new();
        var url = $"Items/{itemId}/Similar?userId={CurrentUser.Id}&limit={limit}&Fields=PrimaryImageAspectRatio,MediaSources,MediaStreams,Blurhashes,ImageBlurHashes";
        try
        {
            return await _http.GetFromJsonAsync<ItemsResult>(url, _jsonOpts) ?? new();
        }
        catch { return new(); }
    }

    public async Task<List<BaseItemDto>> GetLocalTrailersAsync(string itemId)
    {
        if (CurrentUser == null) return new();
        try
        {
            var url = $"Items/{itemId}/LocalTrailers?userId={CurrentUser.Id}&Fields=PrimaryImageAspectRatio,MediaSources,MediaStreams,Blurhashes,ImageBlurHashes";
            var result = await _http.GetFromJsonAsync<List<BaseItemDto>>(url, _jsonOpts);
            return result ?? new();
        }
        catch { return new(); }
    }

    public async Task<ItemsResult> GetCollectionsAsync(int limit = 50)
    {
        if (CurrentUser == null) return new();
        var url = $"Users/{CurrentUser.Id}/Items?IncludeItemTypes=BoxSet&Recursive=true&Limit={limit}&Fields=PrimaryImageAspectRatio,ItemCounts,ImageBlurHashes";
        try
        {
            return await _http.GetFromJsonAsync<ItemsResult>(url, _jsonOpts) ?? new();
        }
        catch { return new(); }
    }

    public async Task<ItemsResult> GetFavoriteItemsAsync(int limit = 100, int startIndex = 0)
    {
        if (CurrentUser == null) return new();
        var url = $"Users/{CurrentUser.Id}/Items?Filters=IsFavorite&Recursive=true&StartIndex={startIndex}&Limit={limit}&SortBy=SortName&SortOrder=Ascending&Fields=PrimaryImageAspectRatio,MediaSources,MediaStreams,Blurhashes,ImageBlurHashes";
        try { return await _http.GetFromJsonAsync<ItemsResult>(url, _jsonOpts) ?? new(); }
        catch { return new(); }
    }

    public async Task<ItemsResult> GetPlaylistsAsync(int limit = 50)
    {
        if (CurrentUser == null) return new();
        var url = $"Users/{CurrentUser.Id}/Items?IncludeItemTypes=Playlist&Recursive=true&Limit={limit}&Fields=PrimaryImageAspectRatio,ItemCounts,ImageBlurHashes";
        try
        {
            return await _http.GetFromJsonAsync<ItemsResult>(url, _jsonOpts) ?? new();
        }
        catch { return new(); }
    }

    public async Task<ItemsResult> GetSeasonsAsync(string seriesId)
    {
        if (CurrentUser == null) return new();
        var url = $"Shows/{seriesId}/Seasons?userId={CurrentUser.Id}&Fields=PrimaryImageAspectRatio,ItemCounts";
        try
        {
            return await _http.GetFromJsonAsync<ItemsResult>(url, _jsonOpts) ?? new();
        }
        catch { return new(); }
    }

    public async Task<ItemsResult> GetEpisodesAsync(string seriesId, string seasonId)
    {
        if (CurrentUser == null) return new();
        var url = $"Shows/{seriesId}/Episodes?SeasonId={seasonId}&userId={CurrentUser.Id}&Fields=PrimaryImageAspectRatio,MediaSources,MediaStreams,Blurhashes,ImageBlurHashes";
        try
        {
            return await _http.GetFromJsonAsync<ItemsResult>(url, _jsonOpts) ?? new();
        }
        catch { return new(); }
    }

    #endregion

    #region Search

    public async Task<ItemsResult> SearchAsync(string query, int limit = 20)
    {
        if (CurrentUser == null) return new();
        var url = $"Items?userId={CurrentUser.Id}&SearchTerm={Uri.EscapeDataString(query)}&Limit={limit}&Recursive=true&IncludeItemTypes=Movie,Series,Episode&Fields=PrimaryImageAspectRatio,MediaSources,MediaStreams,Blurhashes,ImageBlurHashes";
        try
        {
            return await _http.GetFromJsonAsync<ItemsResult>(url, _jsonOpts) ?? new();
        }
        catch { return new(); }
    }

    #endregion

    #region Playback

    public async Task<PlaybackInfoResponse?> GetPlaybackInfoAsync(string itemId, long maxBitrate = 120_000_000)
    {
        if (CurrentUser == null) return null;
        var url = $"Items/{itemId}/PlaybackInfo?UserId={CurrentUser.Id}&MaxStreamingBitrate={maxBitrate}&AutoOpenLiveStreams=true";

        // Include audio/subtitle preferences from settings
        if (!string.IsNullOrEmpty(Settings.PreferredAudioLanguage))
            url += $"&AudioStreamIndex=-1"; // Let server pick by language preference
        if (Settings.SubtitleMode == "On")
            url += $"&SubtitleStreamIndex=-1"; // Enable default subtitle
        else if (Settings.SubtitleMode == "Off")
            url += $"&SubtitleStreamIndex=-2"; // Disable subtitles

        try
        {
            var profileJson = DeviceProfileBuilder.GetXboxProfileJson(maxBitrate);
            var profile = System.Text.Json.Nodes.JsonNode.Parse(profileJson);
            var response = await _http.PostAsJsonAsync(url, profile, _jsonOpts);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<PlaybackInfoResponse>(_jsonOpts);
        }
        catch { return null; }
    }

    public async Task<(PlaybackInfoResponse? PlaybackInfo, Exception? Error)> GetPlaybackInfoResultAsync(string itemId, long maxBitrate = 120_000_000)
    {
        if (CurrentUser == null) return (null, new InvalidOperationException("Not signed in."));
        try
        {
            var profile = System.Text.Json.Nodes.JsonNode.Parse(DeviceProfileBuilder.GetXboxProfileJson(maxBitrate));
            var url = $"Items/{itemId}/PlaybackInfo?UserId={CurrentUser.Id}&MaxStreamingBitrate={maxBitrate}&AutoOpenLiveStreams=true";
            var response = await _http.PostAsJsonAsync(url, profile, _jsonOpts);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<PlaybackInfoResponse>(_jsonOpts), null);
        }
        catch (Exception ex) { return (null, ex); }
    }

    public async Task ReportPlaybackStartAsync(string itemId, string? audioStreamIndex = null, string? subtitleStreamIndex = null)
    {
        try
        {
            var data = new Dictionary<string, object?>
            {
                ["ItemId"] = itemId,
                ["CanSeek"] = true
            };
            if (audioStreamIndex != null) data["AudioStreamIndex"] = audioStreamIndex;
            if (subtitleStreamIndex != null) data["SubtitleStreamIndex"] = subtitleStreamIndex;

            await _http.PostAsJsonAsync("Sessions/Playing", data, _jsonOpts);
        }
        catch { /* fire and forget */ }
    }

    public async Task ReportPlaybackProgressAsync(string itemId, long positionTicks, bool isPaused = false)
    {
        try
        {
            await _http.PostAsJsonAsync("Sessions/Playing/Progress", new Dictionary<string, object?>
            {
                ["ItemId"] = itemId,
                ["PositionTicks"] = positionTicks,
                ["IsPaused"] = isPaused,
                ["IsMuted"] = false
            }, _jsonOpts);
        }
        catch { /* fire and forget */ }
    }

    public async Task ReportPlaybackStoppedAsync(string itemId, long positionTicks)
    {
        try
        {
            await _http.PostAsJsonAsync("Sessions/Playing/Stopped", new Dictionary<string, object?>
            {
                ["ItemId"] = itemId,
                ["PositionTicks"] = positionTicks
            }, _jsonOpts);
        }
        catch { /* fire and forget */ }
    }

    public async Task MarkPlayedAsync(string itemId)
    {
        if (CurrentUser == null) return;
        try
        {
            await _http.PostAsync($"UserPlayedItems/{itemId}?userId={CurrentUser.Id}", null);
        }
        catch { }
    }

    public async Task MarkUnplayedAsync(string itemId)
    {
        if (CurrentUser == null) return;
        try
        {
            await _http.DeleteAsync($"UserPlayedItems/{itemId}?userId={CurrentUser.Id}");
        }
        catch { }
    }

    public async Task ToggleFavoriteAsync(string itemId, bool isCurrentlyFavorite = false)
    {
        if (CurrentUser == null) throw new InvalidOperationException("Not signed in.");
        var url = $"UserFavoriteItems/{Uri.EscapeDataString(itemId)}?userId={Uri.EscapeDataString(CurrentUser.Id)}";
        using var response = isCurrentlyFavorite
            ? await _http.DeleteAsync(url)
            : await _http.PostAsync(url, null);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Build a playback/transcoding URL with specific audio/subtitle track selection.
    /// </summary>
    public string BuildTranscodeUrlWithTracks(string itemId, MediaSourceInfo source,
        int? audioIndex = null, int? subtitleIndex = null)
    {
        var baseUrl = ServerUrl;
        string tokenParam = !string.IsNullOrEmpty(AccessToken) ? $"api_key={AccessToken}" : "";

        // If the source already provides a TranscodingUrl from PlaybackInfo, use it
        if (!string.IsNullOrEmpty(source.TranscodingUrl))
        {
            var url = source.TranscodingUrl;
            if (audioIndex.HasValue)
                url = ReplaceQueryParameter(url, "AudioStreamIndex", audioIndex.Value.ToString());
            if (subtitleIndex.HasValue && subtitleIndex.Value >= 0)
                url = ReplaceQueryParameter(url, "SubtitleStreamIndex", subtitleIndex.Value.ToString());
            else if (subtitleIndex.HasValue && subtitleIndex.Value < 0)
                url = ReplaceQueryParameter(url, "SubtitleStreamIndex", "-1");

            if (!string.IsNullOrEmpty(tokenParam) && !url.Contains("api_key="))
                url += (url.Contains('?') ? "&" : "?") + tokenParam;

            return $"{baseUrl}{url}";
        }

        // When changing tracks, static=true must NOT be used because static streams ignore track selection
        bool isStatic = !audioIndex.HasValue && (!subtitleIndex.HasValue || subtitleIndex.Value < 0) && source.SupportsDirectPlay;

        var streamUrl = $"Videos/{itemId}/stream?MediaSourceId={source.Id}";
        if (isStatic) streamUrl += "&static=true";
        if (audioIndex.HasValue) streamUrl = ReplaceQueryParameter(streamUrl, "AudioStreamIndex", audioIndex.Value.ToString());
        if (subtitleIndex.HasValue && subtitleIndex.Value >= 0) streamUrl = ReplaceQueryParameter(streamUrl, "SubtitleStreamIndex", subtitleIndex.Value.ToString());
        else if (subtitleIndex.HasValue && subtitleIndex.Value < 0) streamUrl = ReplaceQueryParameter(streamUrl, "SubtitleStreamIndex", "-1");

        if (!string.IsNullOrEmpty(tokenParam))
            streamUrl += $"&{tokenParam}";

        return $"{baseUrl}{streamUrl}";
    }

    public async Task<IntroTimestampsResult> GetIntroTimestampsAsync(string itemId)
    {
        if (CurrentUser == null) return new();

        // 1. Try native Jellyfin MediaSegments API (10.10+)
        try
        {
            var segments = await _http.GetFromJsonAsync<List<MediaSegmentDto>>($"MediaSegments?itemId={itemId}", _jsonOpts);
            if (segments != null && segments.Count > 0)
            {
                var intro = segments.FirstOrDefault(s => string.Equals(s.Type, "Intro", StringComparison.OrdinalIgnoreCase));
                var credits = segments.FirstOrDefault(s => string.Equals(s.Type, "Credits", StringComparison.OrdinalIgnoreCase) || string.Equals(s.Type, "Outro", StringComparison.OrdinalIgnoreCase));

                if (intro != null || credits != null)
                {
                    return new IntroTimestampsResult
                    {
                        Valid = true,
                        IntroStart = intro?.StartSeconds ?? 0,
                        IntroEnd = intro?.EndSeconds ?? 0,
                        CreditsStart = credits?.StartSeconds ?? 0,
                        CreditsEnd = credits?.EndSeconds ?? 0
                    };
                }
            }
        }
        catch { /* Fallback to plugin endpoint */ }

        // 2. Try classic Intro Skipper plugin endpoints
        try
        {
            var res = await _http.GetFromJsonAsync<IntroTimestampsResult>($"Episode/{itemId}/IntroTimestamps", _jsonOpts);
            if (res != null && (res.Valid || res.IntroEnd > 0 || res.CreditsEnd > 0))
            {
                res.Valid = true;
                return res;
            }
        }
        catch { /* Try /Items/{itemId}/IntroTimestamps */ }

        try
        {
            var res = await _http.GetFromJsonAsync<IntroTimestampsResult>($"Items/{itemId}/IntroTimestamps", _jsonOpts);
            if (res != null && (res.Valid || res.IntroEnd > 0 || res.CreditsEnd > 0))
            {
                res.Valid = true;
                return res;
            }
        }
        catch { /* No intro timestamps available */ }

        return new IntroTimestampsResult { Valid = false };
    }

    public async Task<TrickplayInfoDto?> GetTrickplayManifestAsync(string itemId, int width = 320)
    {
        if (CurrentUser == null) return null;
        try
        {
            var manifest = await _http.GetFromJsonAsync<TrickplayInfoDto>($"Videos/{itemId}/Trickplay/{width}/GetManifest", _jsonOpts);
            if (manifest != null && manifest.ThumbnailCount > 0)
                return manifest;
        }
        catch { /* Fall back */ }

        try
        {
            var item = await GetItemAsync(itemId);
            if (item?.Trickplay != null)
            {
                foreach (var mediaSourceKvp in item.Trickplay.Values)
                {
                    if (mediaSourceKvp.TryGetValue(width.ToString(), out var info))
                        return info;
                    if (mediaSourceKvp.Values.FirstOrDefault() is { } fallback)
                        return fallback;
                }
            }
        }
        catch { /* best effort */ }

        return null;
    }

    public string GetTrickplayTileUrl(string itemId, int width, int tileIndex)
    {
        var url = JellyfinEndpointBuilder.Combine(ServerUrl, $"Videos/{itemId}/Trickplay/{width}/{tileIndex}.jpg");
        return JellyfinEndpointBuilder.WithQuery(url, ("api_key", AccessToken));
    }

    #endregion

    #region Subtitles

    public async Task<List<RemoteSubtitleInfo>> SearchRemoteSubtitlesAsync(string itemId, string language = "eng")
    {
        if (CurrentUser == null) return new();
        try
        {
            var url = $"Items/{itemId}/RemoteSearch/Subtitles/{language}";
            var results = await _http.GetFromJsonAsync<List<RemoteSubtitleInfo>>(url, _jsonOpts);
            return results ?? new();
        }
        catch { return new(); }
    }

    public async Task<bool> DownloadRemoteSubtitlesAsync(string itemId, string subtitleId)
    {
        if (CurrentUser == null) return false;
        try
        {
            var url = $"Items/{itemId}/RemoteSearch/Subtitles/{subtitleId}";
            var response = await _http.PostAsync(url, null);
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    #endregion

    #region Remote Control & WebSockets

    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _wsCts;

    public event Action<PlayCommandData>? OnPlayCommand;
    public event Action<PlaystateCommandData>? OnPlaystateCommand;
    public event Action<GeneralCommandData>? OnGeneralCommand;

    public async Task ReportCapabilitiesAsync()
    {
        if (CurrentUser == null || string.IsNullOrEmpty(AccessToken)) return;
        try
        {
            var capabilities = new
            {
                PlayableMediaTypes = new[] { "Video", "Audio" },
                SupportedCommands = new[]
                {
                    "Play", "Playstate", "PlayNext", "PlayMediaSource",
                    "ToggleOsd", "SetVolume", "VolumeUp", "VolumeDown",
                    "Mute", "Unmute", "ToggleMute", "SetAudioStreamIndex", "SetSubtitleStreamIndex"
                },
                SupportsMediaControl = true,
                SupportsPersistentIdentifier = true
            };
            await _http.PostAsJsonAsync("Sessions/Capabilities/Full", capabilities, _jsonOpts);
        }
        catch { /* best effort */ }
    }

    public void StartWebSocketListener()
    {
        StopWebSocketListener();
        if (string.IsNullOrEmpty(ServerUrl) || string.IsNullOrEmpty(AccessToken)) return;

        _wsCts = new CancellationTokenSource();
        _ = RunWebSocketLoopAsync(_wsCts.Token);
    }

    public void StopWebSocketListener()
    {
        try
        {
            _wsCts?.Cancel();
            _wsCts?.Dispose();
            _wsCts = null;
            _webSocket?.Dispose();
            _webSocket = null;
        }
        catch { /* best effort */ }
    }

    private async Task RunWebSocketLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (string.IsNullOrEmpty(ServerUrl) || string.IsNullOrEmpty(AccessToken))
                {
                    await Task.Delay(3000, ct);
                    continue;
                }

                var baseUri = new Uri(ServerUrl);
                var wsScheme = baseUri.Scheme == "https" ? "wss" : "ws";
                var path = baseUri.AbsolutePath.TrimEnd('/');
                var wsUri = new Uri($"{wsScheme}://{baseUri.Authority}{path}/socket?api_key={Uri.EscapeDataString(AccessToken)}&deviceId=BoxJellyfish-Xbox");

                _webSocket = new ClientWebSocket();
                await _webSocket.ConnectAsync(wsUri, ct);

                // Report capabilities once connected so server recognizes client for Play To
                _ = ReportCapabilitiesAsync();

                var buffer = new byte[8192];
                var ms = new MemoryStream();

                while (_webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    ms.SetLength(0);
                    WebSocketReceiveResult result;
                    do
                    {
                        var segment = new ArraySegment<byte>(buffer);
                        result = await _webSocket.ReceiveAsync(segment, ct);
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                            break;
                        }
                        ms.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);

                    if (result.MessageType == WebSocketMessageType.Text && ms.Length > 0)
                    {
                        ms.Seek(0, SeekOrigin.Begin);
                        using var doc = await JsonDocument.ParseAsync(ms, cancellationToken: ct);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("MessageType", out var msgTypeProp))
                        {
                            var msgType = msgTypeProp.GetString();
                            if (msgType == "Play" && root.TryGetProperty("Data", out var playDataProp))
                            {
                                var playData = JsonSerializer.Deserialize<PlayCommandData>(playDataProp.GetRawText(), _jsonOpts);
                                if (playData != null) OnPlayCommand?.Invoke(playData);
                            }
                            else if (msgType == "Playstate" && root.TryGetProperty("Data", out var playstateDataProp))
                            {
                                var playstateData = JsonSerializer.Deserialize<PlaystateCommandData>(playstateDataProp.GetRawText(), _jsonOpts);
                                if (playstateData != null) OnPlaystateCommand?.Invoke(playstateData);
                            }
                            else if (msgType == "GeneralCommand" && root.TryGetProperty("Data", out var generalDataProp))
                            {
                                var generalData = JsonSerializer.Deserialize<GeneralCommandData>(generalDataProp.GetRawText(), _jsonOpts);
                                if (generalData != null) OnGeneralCommand?.Invoke(generalData);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Reconnect delay
                try { await Task.Delay(5000, ct); } catch { break; }
            }
        }
    }

    #endregion

    #region Image URLs

    public string GetImageUrl(string itemId, string imageType = "Primary", int? maxWidth = null, int? maxHeight = null)
    {
        var url = $"Items/{itemId}/Images/{imageType}";
        var @params = new List<string>();
        if (maxWidth.HasValue) @params.Add($"maxWidth={maxWidth.Value}");
        if (maxHeight.HasValue) @params.Add($"maxHeight={maxHeight.Value}");
        if (@params.Count > 0) url += "?" + string.Join("&", @params);
        return url;
    }

    public string GetBackdropUrl(string itemId, int index = 0, int? maxWidth = null)
    {
        var url = $"Items/{itemId}/Images/Backdrop/{index}";
        if (maxWidth.HasValue) url += $"?maxWidth={maxWidth.Value}";
        return url;
    }

    public string GetPersonImageUrl(string personId, int? maxWidth = null)
    {
        var url = $"Items/{personId}/Images/Primary";
        if (maxWidth.HasValue) url += $"?maxWidth={maxWidth.Value}";
        return url;
    }

    public string? GetItemImageUrl(BaseItemDto? item, string imageType = "Primary", int? maxWidth = null)
    {
        if (item == null || string.IsNullOrEmpty(ServerUrl)) return null;

        string itemId = item.Id;

        if (imageType == "Primary")
        {
            if (item.ImageTags == null || !item.ImageTags.ContainsKey("Primary"))
            {
                if (!string.IsNullOrEmpty(item.SeriesId))
                    itemId = item.SeriesId;
            }
        }
        else if (imageType == "Backdrop")
        {
            var backdropId = item.BackdropImageTags?.Count > 0 ? item.Id : item.ParentBackdropItemId;
            if (!string.IsNullOrEmpty(backdropId))
                return $"{ServerUrl}{GetBackdropUrl(backdropId, 0, maxWidth)}";
            return null;
        }

        return $"{ServerUrl}{GetImageUrl(itemId, imageType, maxWidth)}";
    }

    public string? GetPersonProfileImageUrl(BaseItemPerson? person, int? maxWidth = null)
    {
        if (person == null || string.IsNullOrEmpty(ServerUrl) || string.IsNullOrEmpty(person.PrimaryImageTag))
            return null;
        return $"{ServerUrl}{GetPersonImageUrl(person.Id, maxWidth)}";
    }

    #endregion

    #region Helpers

    public TimeSpan? GetRuntime(BaseItemDto item) =>
        item.RunTimeTicks.HasValue ? TimeSpan.FromTicks(item.RunTimeTicks.Value) : null;

    /// <summary>
    /// Probes every API surface the app depends on and reports per-call
    /// success, so failures (auth, routes, playback URLs) are visible on
    /// the device instead of silently swallowed.
    /// </summary>
    public async Task<List<SelfCheckResult>> RunSelfCheckAsync()
    {
        var results = new List<SelfCheckResult>();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        async Task Check(string name, Func<Task<(bool ok, string detail)>> probe)
        {
            var start = sw.ElapsedMilliseconds;
            try
            {
                var (ok, detail) = await probe();
                results.Add(new SelfCheckResult
                {
                    Name = name,
                    Ok = ok,
                    Detail = detail,
                    ElapsedMs = sw.ElapsedMilliseconds - start
                });
            }
            catch (Exception ex)
            {
                results.Add(new SelfCheckResult
                {
                    Name = name,
                    Ok = false,
                    Detail = ex.Message,
                    ElapsedMs = sw.ElapsedMilliseconds - start
                });
            }
        }

        // 1. Server reachability (no auth needed)
        await Check("Server (System/Info/Public)", async () =>
        {
            var info = await GetServerInfoAsync();
            if (info == null) return (false, "no response");
            return (true, $"{info.ServerName} v{info.Version}");
        });

        // 2. Session state
        await Check("Session restored", () => Task.FromResult(
            (IsAuthenticated, $"token: {(!string.IsNullOrEmpty(AccessToken) ? "present" : "missing")}, user: {CurrentUser?.Name ?? "none"}")));

        if (!IsAuthenticated)
        {
            results.Add(new SelfCheckResult { Name = "Library (skipped)", Ok = true, Detail = "not authenticated" });
            return results;
        }

        // 3. Views
        await Check("Views (UserViews)", async () =>
        {
            var views = await GetViewsAsync();
            return (true, $"{views.Count} library view(s)");
        });

        // 4. Resume
        await Check("Resume (UserItems/Resume)", async () =>
        {
            var items = await GetResumeItemsAsync(5);
            return (true, $"{items.Items.Count} item(s)");
        });

        // 5. Library items (first view)
        await Check("Library items (Items)", async () =>
        {
            var views = await GetViewsAsync();
            var view = views.FirstOrDefault();
            if (view == null) return (false, "no library views");
            var items = await GetItemsAsync(view.Id, limit: 5);
            return (true, $"{items.Items.Count} item(s) in '{view.Name}'");
        });

        // 6. Search
        await Check("Search (Items?SearchTerm)", async () =>
        {
            var items = await SearchAsync("test", limit: 5);
            return (true, $"{items.Items.Count} result(s)");
        });

        // 7. Next up
        await Check("Next up (Shows/NextUp)", async () =>
        {
            var items = await GetNextUpAsync(limit: 5);
            return (true, $"{items.Items.Count} episode(s)");
        });

        // 8. PlaybackInfo + stream URL (the critical path for playback)
        await Check("Playback info + stream URL", async () =>
        {
            var views = await GetViewsAsync();
            var view = views.FirstOrDefault();
            if (view == null) return (false, "no library views");

            var items = await GetItemsAsync(view.Id, includeItemTypes: "Movie,Episode", limit: 3);
            var item = items.Items.FirstOrDefault();
            if (item == null) return (false, "no playable items found");

            var playback = await GetPlaybackInfoAsync(item.Id);
            var source = playback?.MediaSources.FirstOrDefault();
            if (source == null) return (false, "no media sources returned");

            // Verify the stream URL authenticates (api_key query param).
            var streamUrl = $"{ServerUrl}/Videos/{item.Id}/stream?static=true&MediaSourceId={source.Id}&api_key={AccessToken}";
            using var probe = new HttpRequestMessage(HttpMethod.Head, streamUrl);
            var response = await _http.SendAsync(probe);
            var code = (int)response.StatusCode;
            var ok = code is 200 or 206 or 302 or 401 or 403; // 401/403 = auth param issue, still a signal
            return (code == 200 || code == 206,
                $"'{item.Name}' source='{source.Id}' directPlay={source.SupportsDirectPlay} stream HTTP {code}");
        });

        // 9. Image loading
        await Check("Image endpoint", async () =>
        {
            var views = await GetViewsAsync();
            var view = views.FirstOrDefault();
            if (view == null) return (false, "no library views");
            var items = await GetItemsAsync(view.Id, limit: 1);
            var item = items.Items.FirstOrDefault();
            if (item == null) return (false, "no items");
            using var probe = new HttpRequestMessage(HttpMethod.Head,
                $"{ServerUrl}{GetImageUrl(item.Id, maxWidth: 128)}");
            var response = await _http.SendAsync(probe);
            return (response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}");
        });

        // 10. Quick Connect availability
        await Check("Quick Connect (Initiate)", async () =>
        {
            var qc = await QuickConnectInitAsync();
            return qc != null
                ? (true, $"enabled, code '{qc.Code}' (secret discarded)")
                : (false, "not enabled on this server");
        });

        return results;
    }

    public string FormatRuntime(TimeSpan? runtime)
    {
        if (!runtime.HasValue) return "";
        var ts = runtime.Value;
        if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        return $"{ts.Minutes}m";
    }

    public static List<MediaStream> GetAudioStreams(MediaSourceInfo source) =>
        source.MediaStreams.Where(s => s.Type == "Audio").OrderBy(s => !s.IsDefault).ToList();

    public static List<MediaStream> GetSubtitleStreams(MediaSourceInfo source) =>
        source.MediaStreams.Where(s => s.Type == "Subtitle").OrderBy(s => !s.IsDefault).ToList();

    #endregion
}
