namespace JellyfinClient.Models;

/// <summary>Result of a single API self-check probe.</summary>
public class SelfCheckResult
{
    public string Name { get; set; } = "";
    public bool Ok { get; set; }
    public string Detail { get; set; } = "";
    public long ElapsedMs { get; set; }
}
