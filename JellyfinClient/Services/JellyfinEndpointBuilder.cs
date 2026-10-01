namespace JellyfinClient.Services;

public static class JellyfinEndpointBuilder
{
    public static string Combine(string? serverUrl, string path) =>
        string.IsNullOrWhiteSpace(serverUrl) ? path.TrimStart('/') : $"{serverUrl.TrimEnd('/')}/{path.TrimStart('/')}";

    public static string WithQuery(string url, params (string Name, object? Value)[] values)
    {
        var query = values.Where(v => v.Value is not null).Select(v => $"{Uri.EscapeDataString(v.Name)}={Uri.EscapeDataString(Convert.ToString(v.Value, System.Globalization.CultureInfo.InvariantCulture) ?? "")}").ToArray();
        return query.Length == 0 ? url : url + (url.Contains('?') ? "&" : "?") + string.Join("&", query);
    }
}
