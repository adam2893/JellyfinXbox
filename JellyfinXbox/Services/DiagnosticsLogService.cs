using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using JellyfinClient;
using Windows.Storage;

namespace JellyfinXbox.Services;

/// <summary>
/// Small, local-only diagnostic journal for reproducing Xbox failures.
/// Values are redacted before they reach disk; access tokens, passwords,
/// Quick Connect secrets, and URL query strings are never persisted.
/// </summary>
public sealed class DiagnosticsLogService
{
    public const string FileName = "xboxjellyfish-diagnostics.log";
    private const int MaxBytes = 256 * 1024;
    private const int MaxValueLength = 1200;
    private static readonly Regex SensitiveQueryRegex = new(
        @"(?i)(api[_-]?key|access[_-]?token|token|secret|password|authorization|code)=([^&\s]+)",
        RegexOptions.Compiled);
    private readonly object _gate = new();

    public string FilePath
    {
        get
        {
            try { return Path.Combine(ApplicationData.Current.LocalFolder.Path, FileName); }
            catch { return FileName; }
        }
    }

    public void BeginSession()
    {
        Log("app", "session-start",
            ("version", ClientInfo.Version),
            ("os", Environment.OSVersion.VersionString));
    }

    public void Log(string area, string eventName, params (string Name, object? Value)[] fields)
    {
        var builder = new StringBuilder()
            .Append(DateTimeOffset.UtcNow.ToString("O"))
            .Append(" area=")
            .Append(Sanitize(area))
            .Append(" event=")
            .Append(Sanitize(eventName));

        foreach (var (name, value) in fields)
        {
            builder.Append(' ')
                .Append(Sanitize(name))
                .Append('=')
                .Append(FormatValue(name, value));
        }

        WriteLine(builder.ToString());
    }

    public void LogException(string area, string eventName, Exception exception,
        params (string Name, object? Value)[] fields)
    {
        var allFields = new List<(string Name, object? Value)>(fields)
        {
            ("exception", exception.GetType().FullName),
            ("hresult", $"0x{exception.HResult:X8}"),
            ("message", exception.Message),
            ("stack", exception.StackTrace ?? "")
        };
        Log(area, eventName, allFields.ToArray());
    }

    public string ReadText()
    {
        try
        {
            lock (_gate)
            {
                return File.Exists(FilePath)
                    ? File.ReadAllText(FilePath)
                    : "No diagnostic log has been written yet.";
            }
        }
        catch (Exception ex)
        {
            return $"Unable to read diagnostic log: {ex.GetType().Name}";
        }
    }

    public void Clear()
    {
        try
        {
            lock (_gate)
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
        }
        catch { }
    }

    public static string RedactId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "<empty>";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    private void WriteLine(string line)
    {
        try
        {
            lock (_gate)
            {
                File.AppendAllText(FilePath, line + Environment.NewLine, Encoding.UTF8);
                var info = new FileInfo(FilePath);
                if (info.Length <= MaxBytes) return;

                var text = File.ReadAllText(FilePath);
                var keep = text.Length > MaxBytes ? text[^MaxBytes..] : text;
                File.WriteAllText(FilePath,
                    "# diagnostic log truncated to the most recent entries" + Environment.NewLine + keep,
                    Encoding.UTF8);
            }
        }
        catch
        {
            // Diagnostics must never become a new crash path.
        }
    }

    private static string FormatValue(string name, object? value)
    {
        var key = name.ToLowerInvariant();
        if (key.Contains("token") || key.Contains("secret") || key.Contains("password") ||
            key.Contains("authorization") || key is "code" or "api_key")
            return "<redacted>";

        if (key.Contains("item") && key.Contains("id") || key.EndsWith("id", StringComparison.Ordinal))
            return RedactId(value?.ToString());

        if (key.Contains("url"))
            return RedactUrl(value?.ToString());

        return Sanitize(value?.ToString() ?? "<null>");
    }

    private static string RedactUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "<empty>";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return "<invalid-url>";
        return $"{uri.Scheme}://{uri.Host}{uri.AbsolutePath}";
    }

    private static string Sanitize(string value)
    {
        var sanitized = SensitiveQueryRegex.Replace(value, "$1=<redacted>")
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ');
        return sanitized.Length <= MaxValueLength ? sanitized : sanitized[..MaxValueLength] + "…";
    }
}
