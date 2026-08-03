using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JellyfinClient.Models;

namespace JellyfinClient.Services;

public class JellyfinApiClient
{
    private const string StoreKeyServerUrl = "session.serverUrl";
    private const string StoreKeyAccessToken = "session.accessToken";
    private const string StoreKeyCurrentUser = "session.currentUser";
    private const string StoreKeySettings = "session.settings";
    private const string StoreKeyServerInfo = "session.serverInfo";

    private readonly HttpClient _http;
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

    public JellyfinApiClient(HttpClient http, ISessionStore? sessionStore = null)
    {
        _http = http;
        _store = sessionStore;
        _jsonOpts = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        RestoreSession();
    }

    public void SetServerUrl(string url)
    {
        ServerUrl = url.TrimEnd('/');
        _http.BaseAddress = new Uri(ServerUrl);
        _store?.Set(StoreKeyServerUrl, ServerUrl);
    }

    private void ApplyAuth()
    {
        if (!string.IsNullOrEmpty(AccessToken))
        {
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);
            _http.DefaultRequestHeaders.Add("X-Emby-Token", AccessToken);
        }
        else
        {
            _http.DefaultRequestHeaders.Authorization = null;
            _http.DefaultRequestHeaders.Remove("X-Emby-Token");
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
                _http.BaseAddress = new Uri(ServerUrl);
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

    private void ClearSession()
    {
        if (_store == null) return;
        _store.Set(StoreKeyServerUrl, null);
        _store.Set(StoreKeyAccessToken, null);
        _store.Set(StoreKeyCurrentUser, null);
        _store.Set(StoreKeySettings, null);
        _store.Set(StoreKeyServerInfo, null);
    }

    #endregion

    #region Discovery

    public async Task<ServerInfo?> GetServerInfoAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<ServerInfo>("/System/Info/Public", _jsonOpts);
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
            var result = await _http.GetFromJsonAsync<List<User>>("/Users/Public", _jsonOpts);
            return result ?? new();
        }
        catch { return new(); }
    }

    public async Task<AuthenticationResult?> AuthenticateAsync(string username, string password)
    {
        var body = new { Username = username, Pw = password };
        try
        {
            var response = await _http.PostAsJsonAsync("/Users/AuthenticateByName", body);
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

    public void Logout()
    {
        AccessToken = null;
        CurrentUser = null;
        ApplyAuth();
        ClearSession();
        OnAuthenticationChanged?.Invoke();
    }

    #endregion

    #region Quick Connect

    public async Task<QuickConnectInitResponse?> QuickConnectInitAsync()
    {
        try
        {
            var response = await _http.PostAsync("/QuickConnect/Initiate", null);
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
                $"/QuickConnect/Authorize?code={Uri.EscapeDataString(code)}", _jsonOpts);
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
            var response = await _http.PostAsJsonAsync("/Users/AuthenticateWithQuickConnect", body, _jsonOpts);
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
        catch { return null; }
    }

    #endregion

    #region Library

    public async Task<List<ViewItem>> GetViewsAsync()
    {
        try
        {
            var result = await _http.GetFromJsonAsync<ItemsResult>($"/UserViews?userId={CurrentUser!.Id}", _jsonOpts);
            if (result?.Items == null) return new();

            return result.Items.Select(i => new ViewItem
            {
                Id = i.Id,
                Name = i.Name,
                Type = i.Type,
                ImageTags = i.ImageTags
            }).ToList();
        }
        catch { return new(); }
    }

    public async Task<ItemsResult> GetItemsAsync(string parentId, string? sortBy = null, string? sortOrder = null,
        int startIndex = 0, int limit = 40, string? filters = null, string? includeItemTypes = null)
    {
        var url = $"/Items?userId={CurrentUser!.Id}&ParentId={parentId}&StartIndex={startIndex}&Limit={limit}&Fields=PrimaryImageAspectRatio,BasicSyncInfo,MediaStreams,MediaSources,Blurhashes,ImageBlurHashes";

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

    public async Task<ItemsResult> GetResumeItemsAsync(int limit = 12)
    {
        var url = $"/UserItems/Resume?userId={CurrentUser!.Id}&Limit={limit}&Fields=PrimaryImageAspectRatio,MediaSources,MediaStreams,Blurhashes,ImageBlurHashes&MediaTypes=Video";
        try
        {
            var result = await _http.GetFromJsonAsync<ItemsResult>(url, _jsonOpts);
            return result ?? new();
        }
        catch { return new(); }
    }

    public async Task<ItemsResult> GetNextUpAsync(string? seriesId = null, int limit = 12)
    {
        var url = $"/Shows/NextUp?UserId={CurrentUser!.Id}&Limit={limit}&Fields=PrimaryImageAspectRatio,MediaSources,MediaStreams,Blurhashes,ImageBlurHashes";
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
        var url = $"/Items/{itemId}?userId={CurrentUser!.Id}&Fields=PrimaryImageAspectRatio,MediaSources,MediaStreams,Blurhashes,ImageBlurHashes,People,Studios,Genres";
        try
        {
            return await _http.GetFromJsonAsync<BaseItemDto>(url, _jsonOpts);
        }
        catch { return null; }
    }

    public async Task<ItemsResult> GetSeasonsAsync(string seriesId)
    {
        var url = $"/Shows/{seriesId}/Seasons?userId={CurrentUser!.Id}&Fields=PrimaryImageAspectRatio,ItemCounts";
        try
        {
            return await _http.GetFromJsonAsync<ItemsResult>(url, _jsonOpts) ?? new();
        }
        catch { return new(); }
    }

    public async Task<ItemsResult> GetEpisodesAsync(string seriesId, string seasonId)
    {
        var url = $"/Shows/{seriesId}/Episodes?SeasonId={seasonId}&userId={CurrentUser!.Id}&Fields=PrimaryImageAspectRatio,MediaSources,MediaStreams,Blurhashes,ImageBlurHashes";
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
        var url = $"/Items?userId={CurrentUser!.Id}&SearchTerm={Uri.EscapeDataString(query)}&Limit={limit}&IncludeItemTypes=Movie,Series,Episode&Fields=PrimaryImageAspectRatio,MediaSources,MediaStreams,Blurhashes,ImageBlurHashes";
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
        var url = $"/Items/{itemId}/PlaybackInfo?UserId={CurrentUser!.Id}&MaxStreamingBitrate={maxBitrate}&AutoOpenLiveStreams=true";

        // Include audio/subtitle preferences from settings
        if (!string.IsNullOrEmpty(Settings.PreferredAudioLanguage))
            url += $"&AudioStreamIndex=-1"; // Let server pick by language preference
        if (Settings.SubtitleMode == "On")
            url += $"&SubtitleStreamIndex=-1"; // Enable default subtitle
        else if (Settings.SubtitleMode == "Off")
            url += $"&SubtitleStreamIndex=-2"; // Disable subtitles

        // The PlaybackInfo body must carry the DeviceProfile as an object
        // (PlaybackInfoDto.DeviceProfile), not as a string — otherwise the
        // server ignores it and direct-play decisions are made blind.
        try
        {
            var profile = System.Text.Json.Nodes.JsonNode.Parse(DeviceProfileBuilder.GetXboxProfileJson());
            var response = await _http.PostAsJsonAsync(url, profile, _jsonOpts);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<PlaybackInfoResponse>(_jsonOpts);
        }
        catch { return null; }
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

            await _http.PostAsJsonAsync("/Sessions/Playing", data, _jsonOpts);
        }
        catch { /* fire and forget */ }
    }

    public async Task ReportPlaybackProgressAsync(string itemId, long positionTicks, bool isPaused = false)
    {
        try
        {
            await _http.PostAsJsonAsync("/Sessions/Playing/Progress", new Dictionary<string, object?>
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
            await _http.PostAsJsonAsync("/Sessions/Playing/Stopped", new Dictionary<string, object?>
            {
                ["ItemId"] = itemId,
                ["PositionTicks"] = positionTicks
            }, _jsonOpts);
        }
        catch { /* fire and forget */ }
    }

    public async Task MarkPlayedAsync(string itemId)
    {
        try
        {
            await _http.DeleteAsync($"/UserPlayedItems/{itemId}?userId={CurrentUser!.Id}");
        }
        catch { }
    }

    public async Task ToggleFavoriteAsync(string itemId)
    {
        try
        {
            await _http.PostAsync($"/UserFavoriteItems/{itemId}?userId={CurrentUser!.Id}", null);
        }
        catch { }
    }

    /// <summary>
    /// Build a transcoding URL with specific audio/subtitle track selection.
    /// </summary>
    public string BuildTranscodeUrlWithTracks(string itemId, MediaSourceInfo source,
        int? audioIndex = null, int? subtitleIndex = null)
    {
        var baseUrl = ServerUrl;
        // Note: Jellyfin's auth middleware reads the query token as
        // "api_key" (lowercase), not "ApiKey".
        var url = $"/Videos/{itemId}/stream?static=true&MediaSourceId={source.Id}&api_key={AccessToken}";

        // If we need to switch tracks on a transcode, we need a full transcode URL
        if (!source.SupportsDirectPlay && !string.IsNullOrEmpty(source.TranscodingUrl))
        {
            url = source.TranscodingUrl;
            if (audioIndex.HasValue)
                url += $"&AudioStreamIndex={audioIndex.Value}";
            if (subtitleIndex.HasValue)
                url += $"&SubtitleStreamIndex={subtitleIndex.Value}";
            return $"{baseUrl}{url}&api_key={AccessToken}";
        }

        // For direct play / direct stream, track changes may need transcoding
        if (audioIndex.HasValue)
            url += $"&AudioStreamIndex={audioIndex.Value}";
        if (subtitleIndex.HasValue)
            url += $"&SubtitleStreamIndex={subtitleIndex.Value}";

        return $"{baseUrl}{url}";
    }

    #endregion

    #region Image URLs

    public string GetImageUrl(string itemId, string imageType = "Primary", int? maxWidth = null, int? maxHeight = null)
    {
        var url = $"/Items/{itemId}/Images/{imageType}";
        var @params = new List<string>();
        if (maxWidth.HasValue) @params.Add($"maxWidth={maxWidth.Value}");
        if (maxHeight.HasValue) @params.Add($"maxHeight={maxHeight.Value}");
        if (@params.Count > 0) url += "?" + string.Join("&", @params);
        return url;
    }

    public string GetBackdropUrl(string itemId, int index = 0, int? maxWidth = null)
    {
        var url = $"/Items/{itemId}/Images/Backdrop/{index}";
        if (maxWidth.HasValue) url += $"?maxWidth={maxWidth.Value}";
        return url;
    }

    public string GetPersonImageUrl(string personId, int? maxWidth = null)
    {
        var url = $"/Items/{personId}/Images/Primary";
        if (maxWidth.HasValue) url += $"?maxWidth={maxWidth.Value}";
        return url;
    }

    #endregion

    #region Helpers

    public TimeSpan? GetRuntime(BaseItemDto item) =>
        item.RunTimeTicks.HasValue ? TimeSpan.FromTicks(item.RunTimeTicks.Value) : null;

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
