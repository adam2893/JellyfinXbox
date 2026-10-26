using System.Net;
using System.Text;
using JellyfinClient.Models;
using JellyfinClient.Services;
using Xunit;

namespace JellyfinClient.Tests;

public sealed class ClientTests
{
    [Theory]
    [InlineData("https://example.test", "/System/Info", "https://example.test/System/Info")]
    [InlineData("https://example.test/", "System/Info", "https://example.test/System/Info")]
    [InlineData(null, "/System/Info", "System/Info")]
    public void EndpointBuilderCombinesBasePath(string? baseUrl, string path, string expected) =>
        Assert.Equal(expected, JellyfinEndpointBuilder.Combine(baseUrl, path));

    [Fact]
    public void EndpointBuilderEscapesAndAppendsQuery()
    {
        Assert.Equal("Items?name=a%20b&count=2", JellyfinEndpointBuilder.WithQuery("Items", ("name", "a b"), ("count", 2)));
        Assert.Equal("Items?existing=1&name=x", JellyfinEndpointBuilder.WithQuery("Items?existing=1", ("name", "x")));
    }

    [Fact]
    public async Task AuthenticateRejectsResponseWithoutCredentials()
    {
        var handler = new RecordingHandler(_ => Json("{\"AccessToken\":\"\",\"User\":null}"));
        var client = NewClient(handler);
        client.SetServerUrl("https://example.test");

        var result = await client.AuthenticateAsync("user", "password");

        Assert.Null(result);
        Assert.False(client.IsAuthenticated);
    }

    [Fact]
    public async Task AuthenticatePersistsValidSessionAndLogoutClearsIt()
    {
        var store = new MemoryStore();
        var handler = new RecordingHandler(_ => Json("{\"AccessToken\":\"token\",\"User\":{\"Id\":\"u1\",\"Name\":\"Ada\"}}"));
        var client = NewClient(handler, store);
        client.SetServerUrl("https://example.test/");

        Assert.NotNull(await client.AuthenticateAsync("user", "password"));
        Assert.True(client.IsAuthenticated);
        client.Logout();

        Assert.False(client.IsAuthenticated);
        Assert.Equal("https://example.test", client.ServerUrl);
        Assert.Null(store.Get("session.accessToken"));
        Assert.Null(store.Get("session.currentUser"));
    }

    [Fact]
    public async Task FavoritesReturnEmptyWhenUnauthenticatedAndBuildFavoriteQuery()
    {
        var handler = new RecordingHandler(request => request.RequestUri!.AbsolutePath.Contains("AuthenticateByName")
            ? Json("{\"AccessToken\":\"token\",\"User\":{\"Id\":\"u1\",\"Name\":\"Ada\"}}")
            : Json("{\"Items\":[{\"Id\":\"fav1\",\"Name\":\"Favorite\"}],\"TotalRecordCount\":1}"));
        var client = NewClient(handler);
        Assert.Empty((await client.GetFavoriteItemsAsync()).Items);
        client.SetServerUrl("https://example.test");
        await client.AuthenticateAsync("user", "password");

        var result = await client.GetFavoriteItemsAsync(25, 50);

        Assert.Single(result.Items);
        Assert.Contains("Filters=IsFavorite", handler.LastRequest!.RequestUri!.Query);
        Assert.Contains("StartIndex=50", handler.LastRequest.RequestUri.Query);
        Assert.Contains("Limit=25", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task ViewsParseTheUserViewsItemsEnvelope()
    {
        var handler = new RecordingHandler(request => request.RequestUri!.AbsolutePath.Contains("AuthenticateByName")
            ? Json("{\"AccessToken\":\"token\",\"User\":{\"Id\":\"u1\",\"Name\":\"Ada\"}}")
            : Json("{\"Items\":[{\"Id\":\"view1\",\"Name\":\"Library\",\"Type\":\"CollectionFolder\",\"CollectionType\":\"movies\"}]}") );
        var client = NewClient(handler);
        client.SetServerUrl("https://example.test");
        await client.AuthenticateAsync("user", "password");

        var views = await client.GetViewsAsync();

        var view = Assert.Single(views);
        Assert.Equal("view1", view.Id);
        Assert.Equal("Library", view.Name);
        Assert.Equal("movies", view.CollectionType);
    }

    [Fact]
    public async Task PaginationUrlEscapesIdsAndIncludesFilters()
    {
        var handler = new RecordingHandler(request => request.RequestUri!.AbsolutePath.Contains("AuthenticateByName")
            ? Json("{\"AccessToken\":\"token\",\"User\":{\"Id\":\"u1\",\"Name\":\"Ada\"}}")
            : Json("{\"Items\":[],\"TotalRecordCount\":0}"));
        var client = NewClient(handler);
        client.SetServerUrl("https://example.test");
        await client.AuthenticateAsync("user", "password");
        await client.GetItemsPageAsync("parent / one", 20, 10, "Movie,Episode");

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("ParentId=parent%20%2F%20one", query);
        Assert.Contains("StartIndex=20", query);
        Assert.Contains("Limit=10", query);
        Assert.Contains("Filters=Movie%2CEpisode", query);
    }

    [Fact]
    public async Task SwitchingServersClearsThePreviousSession()
    {
        var store = new MemoryStore();
        var handler = new RecordingHandler(request =>
            request.RequestUri!.AbsolutePath.Contains("AuthenticateByName")
                ? Json("{\"AccessToken\":\"token\",\"User\":{\"Id\":\"u1\",\"Name\":\"Ada\"}}")
                : Json("{}"));
        var client = NewClient(handler, store);

        client.SetServerUrl("https://first.example");
        Assert.NotNull(await client.AuthenticateAsync("user", "password"));
        Assert.True(client.IsAuthenticated);

        client.SetServerUrl("https://second.example");

        Assert.False(client.IsAuthenticated);
        Assert.Equal("https://second.example", client.ServerUrl);
        Assert.Null(store.Get("session.accessToken"));
        Assert.Null(store.Get("session.currentUser"));
    }

    [Fact]
    public async Task QuickConnectPollUsesTheClientSecretEndpoint()
    {
        var handler = new RecordingHandler(_ => Json("{\"Authenticated\":true,\"Secret\":\"authorized\"}"));
        var client = NewClient(handler);
        client.SetServerUrl("https://example.test");

        var result = await client.QuickConnectCheckAuthAsync("poll secret");

        Assert.NotNull(result);
        Assert.True(result!.Authenticated);
        Assert.Equal("/QuickConnect/Connect", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.Contains("Secret=poll%20secret", handler.LastRequest.RequestUri.Query);
    }

    [Fact]
    public async Task MediaDetailRequestsEscapeItemIds()
    {
        var handler = new RecordingHandler(request =>
            request.RequestUri!.AbsolutePath.Contains("AuthenticateByName")
                ? Json("{\"AccessToken\":\"token\",\"User\":{\"Id\":\"u1\",\"Name\":\"Ada\"}}")
                : Json("{\"Id\":\"item-1\",\"Name\":\"Title\"}"));
        var client = NewClient(handler);
        client.SetServerUrl("https://example.test");
        await client.AuthenticateAsync("user", "password");

        var result = await client.GetItemResultAsync("item / one");

        Assert.NotNull(result.Item);
        var escapedPath = handler.LastRequest!.RequestUri!.GetComponents(UriComponents.Path, UriFormat.UriEscaped);
        Assert.Contains("Items/item%20%2F%20one", escapedPath);
    }

    [Fact]
    public void ImageUrlDoesNotCreateAnInvalidPathForMissingItemId()
    {
        var client = NewClient(new RecordingHandler(_ => Json("{}")));
        client.SetServerUrl("https://example.test");

        Assert.Null(client.GetItemImageUrl(new BaseItemDto { Id = "" }));
    }

    [Fact]
    public async Task PlaybackInfoRequestsEscapeItemIds()
    {
        var handler = new RecordingHandler(request =>
            request.RequestUri!.AbsolutePath.Contains("AuthenticateByName")
                ? Json("{\"AccessToken\":\"token\",\"User\":{\"Id\":\"u1\",\"Name\":\"Ada\"}}")
                : Json("{\"MediaSources\":[]}"));
        var client = NewClient(handler);
        client.SetServerUrl("https://example.test");
        await client.AuthenticateAsync("user", "password");

        await client.GetPlaybackInfoResultAsync("item / one");

        var escapedPath = handler.LastRequest!.RequestUri!.GetComponents(UriComponents.Path, UriFormat.UriEscaped);
        Assert.Contains("Items/item%20%2F%20one/PlaybackInfo", escapedPath);
    }

    [Fact]
    public async Task PlaybackUrlUsesTheSourceProvidedByPlaybackInfo()
    {
        var handler = new RecordingHandler(_ => Json("{\"AccessToken\":\"token\",\"User\":{\"Id\":\"u1\",\"Name\":\"Ada\"}}"));
        var client = NewClient(handler);
        client.SetServerUrl("https://example.test/base");
        await client.AuthenticateAsync("user", "password");

        var url = client.BuildPlaybackUrl(
            "item-1",
            new MediaSourceInfo
            {
                SupportsDirectPlay = true,
                DirectStreamUrl = "/Videos/item-1/master.m3u8?MediaSourceId=source-1"
            });

        Assert.Contains("/Videos/item-1/master.m3u8", url);
        Assert.DoesNotContain("/Videos/item-1/stream", url);
        Assert.Contains("api_key=token", url);
    }

    [Fact]
    public async Task DeviceIdIsPersistedAndSentInMediaBrowserHeader()
    {
        var store = new MemoryStore();
        var handler = new RecordingHandler(request =>
            request.RequestUri!.AbsolutePath.Contains("AuthenticateByName")
                ? Json("{\"AccessToken\":\"token\",\"User\":{\"Id\":\"u1\",\"Name\":\"Ada\"}}")
                : Json("{}"));
        var client = NewClient(handler, store);
        client.SetServerUrl("https://example.test");
        await client.AuthenticateAsync("user", "password");

        var deviceId = client.DeviceId;
        Assert.StartsWith("XboxJellyfish-", deviceId);
        Assert.Equal(deviceId, store.Get("session.deviceId"));
        Assert.Contains($"DeviceId=\"{deviceId}\"", handler.LastRequest!.Headers.Authorization!.Parameter);
    }

    [Fact]
    public void InvalidServerUrlIsRejectedBeforeNetworkUse()
    {
        var client = NewClient(new RecordingHandler(_ => Json("{}")));

        Assert.Throws<ArgumentException>(() => client.SetServerUrl("not a URL"));
    }

    [Fact]
    public async Task TranscodeUrlEscapesAndReplacesTrackParametersSafely()
    {
        var handler = new RecordingHandler(_ => Json("{\"AccessToken\":\"token\",\"User\":{\"Id\":\"u1\",\"Name\":\"Ada\"}}"));
        var client = NewClient(handler);
        client.SetServerUrl("https://example.test/base");
        await client.AuthenticateAsync("user", "password");

        var url = client.BuildTranscodeUrlWithTracks(
            "item/one",
            new MediaSourceInfo
            {
                Id = "source 1",
                TranscodingUrl = "/Videos/item/master.m3u8?MediaSourceId=source%201"
            },
            audioIndex: 2,
            subtitleIndex: -1);

        Assert.StartsWith("https://example.test/base/Videos/item/master.m3u8?", url);
        Assert.Contains("AudioStreamIndex=2", url);
        Assert.Contains("SubtitleStreamIndex=-1", url);
        Assert.Contains("api_key=token", url);
        Assert.DoesNotContain("&&", url);
    }

    private static JellyfinApiClient NewClient(HttpMessageHandler handler, ISessionStore? store = null) =>
        new(new HttpClient(handler), store);

    private static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public List<HttpRequestMessage> Requests { get; } = new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }

    private sealed class MemoryStore : ISessionStore
    {
        private readonly Dictionary<string, string?> values = new();
        public string? Get(string key) => values.TryGetValue(key, out var value) ? value : null;
        public void Set(string key, string? value) => values[key] = value;
    }
}
