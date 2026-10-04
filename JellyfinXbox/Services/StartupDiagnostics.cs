using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Windows.Storage;

namespace JellyfinXbox.Services;

internal static class StartupDiagnostics
{
    private const string CurrentFile = "startup-diagnostics.log";
    private const string PreviousFile = "startup-diagnostics.previous.log";
    private static readonly object Gate = new();
    private static bool rotated;

    public static void BeginLaunch()
    {
        if (rotated) return;
        rotated = true;
        try
        {
            var folder = ApplicationData.Current.LocalFolder.Path;
            var current = Path.Combine(folder, CurrentFile);
            if (File.Exists(current)) File.Move(current, Path.Combine(folder, PreviousFile), true);
        }
        catch { }
        Stage("launch.begin");
    }

    public static string LastStage { get; private set; } = "launch.begin";

    public static void Stage(string name)
    {
        LastStage = name;
        TryWrite("stage=" + name);
    }

    public static void Exception(string stage, System.Exception error) =>
        TryWrite($"stage={stage} exception={error.GetType().FullName} hresult=0x{error.HResult:X8} methods={Methods(error)}");

    public static void TryWrite(string line)
    {
        try
        {
            lock (Gate)
            {
                var file = Path.Combine(ApplicationData.Current.LocalFolder.Path, CurrentFile);
                var existing = File.Exists(file) ? File.ReadAllText(file) : "";
                var text = existing + DateTime.UtcNow.ToString("O") + " " + line + Environment.NewLine;
                if (text.Length > 64 * 1024) text = text.Substring(text.Length - 64 * 1024);
                File.WriteAllText(file, text);
            }
        }
        catch { }
    }

    private static string Methods(System.Exception error)
    {
        try
        {
            return string.Join(",", new StackTrace(error, false).GetFrames()?.Take(8)
                .Select(f => f.GetMethod()?.Name ?? "?") ?? Enumerable.Empty<string>());
        }
        catch { return "?"; }
    }
}
