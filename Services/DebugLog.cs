using System.Collections.Concurrent;

namespace TTNOverlay.Services;

/// <summary>
/// Lightweight file-backed debug logger with a background flush timer; only writes when enabled in settings.
/// </summary>
public static class DebugLog
{
    public static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TTNOverlay",
        "debug.log"
    );

    private static readonly ConcurrentQueue<string> _pending = new();
    private static readonly System.Threading.Timer _flushTimer;
    private static StreamWriter? _writer;

    public static bool Enabled { get; set; } = true;

    static DebugLog()
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath)!;
            Directory.CreateDirectory(dir);
            _writer = new StreamWriter(FilePath, append: false) { AutoFlush = false };
            _writer.WriteLine($"=== Log started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            _writer.Flush();
        }
        catch
        {
            _writer = null;
        }

        _flushTimer = new System.Threading.Timer(_ => Flush(), null, 250, 250);
    }

    public static void Write(string message)
    {
        if (!Enabled)
            return;

        _pending.Enqueue($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
    }

    public static void WriteException(string context, Exception ex)
    {
        Write($"EXCEPTION en {context}: {ex.GetType().Name}: {ex.Message}");
        Write(ex.StackTrace ?? "(sin stack trace)");
    }

    private static void Flush()
    {
        if (_writer is null || _pending.IsEmpty)
            return;

        try
        {
            while (_pending.TryDequeue(out var line))
                _writer.WriteLine(line);
            _writer.Flush();
        }
        catch
        {

        }
    }

    public static void FlushNow() => Flush();

    public static void Shutdown()
    {
        _flushTimer.Dispose();
        Flush();
        _writer?.Dispose();
        _writer = null;
    }
}

