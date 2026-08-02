using System.Collections.Concurrent;
using System.Text.Json;
using JellyfinClient.Services;

namespace JellyfinXbox.Services;

/// <summary>
/// Tracks user-approved server certificates (by SHA-1 thumbprint, persisted
/// in LocalSettings). The HTTP handler only accepts a certificate that the
/// user explicitly trusted, instead of blindly accepting everything.
/// </summary>
public sealed class CertificateTrustService
{
    private const string StoreKey = "session.trustedCerts";

    private readonly ISessionStore _store;
    private readonly ConcurrentDictionary<string, byte> _trusted = new(StringComparer.OrdinalIgnoreCase);
    private int _promptInFlight;

    public CertificateTrustService(ISessionStore store)
    {
        _store = store;
        var json = _store.Get(StoreKey);
        if (string.IsNullOrEmpty(json)) return;

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json);
            if (list != null)
                foreach (var t in list) _trusted[t] = 0;
        }
        catch
        {
            // Corrupt trust list — start clean.
        }
    }

    public bool IsTrusted(string thumbprint) => _trusted.ContainsKey(thumbprint);

    public void AddTrusted(string thumbprint)
    {
        _trusted[thumbprint] = 0;
        _store.Set(StoreKey, JsonSerializer.Serialize(_trusted.Keys.ToList()));
    }

    /// <summary>
    /// Raised when a connection is rejected because its certificate is not
    /// trusted (host, thumbprint, issuer). Fires on a threadpool thread.
    /// </summary>
    public event Action<string, string, string>? CertificateRejected;

    /// <summary>Raises CertificateRejected once per rejection burst, so
    /// parallel requests for the same server don't stack dialogs.</summary>
    public void ReportRejection(string host, string thumbprint, string issuer)
    {
        if (Interlocked.CompareExchange(ref _promptInFlight, 1, 0) != 0) return;
        CertificateRejected?.Invoke(host, thumbprint, issuer);
    }

    /// <summary>Releases the one-at-a-time gate (call after the prompt UI closes).</summary>
    public void PromptResolved() => Interlocked.Exchange(ref _promptInFlight, 0);
}
