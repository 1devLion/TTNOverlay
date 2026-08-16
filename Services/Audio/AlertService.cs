namespace TTNOverlay.Services;

/// <summary>
/// Loads and plays short one-shot alert sounds (message/event) with per-key cooldown and an in-memory byte cache.
/// </summary>
public static class AlertService
{
    private const int MaxDurationMs = 1000;
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(5);
    private static bool _cooldownEnabled = true;
    private static readonly Dictionary<string, byte[]?> _cache = new();
    private static readonly Dictionary<string, DateTime> _lastPlayed = new();
    private static readonly Dictionary<string, float> _volumes = new();

    public static void PrepareAlert(string key, string? path)
    {
        _cache[key] = null;

        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return;

        try
        {
            var raw = File.ReadAllBytes(path);
            _cache[key] = TrimWavToOneSecond(raw);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException($"AlertService.PrepareAlert({key})", ex);
        }
    }

    public static void SetCooldownEnabled(bool enabled) => _cooldownEnabled = enabled;

    public static bool ShouldTrigger(string key)
    {
        var now = DateTime.UtcNow;
        if (
            _cooldownEnabled
            && _lastPlayed.TryGetValue(key, out var last)
            && now - last < Cooldown
        )
            return false;

        _lastPlayed[key] = now;
        return true;
    }

    public static void SetOutputDevice(int deviceId) => WaveOutPlayer.SetDevice(deviceId);

    public static void SetVolume(string key, float volume) =>
        _volumes[key] = Math.Clamp(volume, 0f, 1f);

    public static IReadOnlyList<(int Id, string Name)> GetOutputDevices() =>
        WaveOutPlayer.GetOutputDevices();

    public static void PlaySound(string key)
    {
        if (!_cache.TryGetValue(key, out var buffer) || buffer is null)
            return;

        var volume = _volumes.TryGetValue(key, out var v) ? v : 1f;

        try
        {
            WaveOutPlayer.Play(buffer, volume);
        }
        catch (Exception ex)
        {
            DebugLog.WriteException($"AlertService.PlaySound({key})", ex);
        }
    }

    private static byte[]? TrimWavToOneSecond(byte[] wav)
    {
        if (wav.Length < 44)
            return null;

        if (
            wav[0] != 'R'
            || wav[1] != 'I'
            || wav[2] != 'F'
            || wav[3] != 'F'
            || wav[8] != 'W'
            || wav[9] != 'A'
            || wav[10] != 'V'
            || wav[11] != 'E'
        )
            return null;

        int pos = 12;
        short channels = 0,
            bitsPerSample = 0;
        int sampleRate = 0;
        int dataChunkPos = -1,
            dataChunkSize = 0;

        while (pos + 8 <= wav.Length)
        {
            var chunkId = System.Text.Encoding.ASCII.GetString(wav, pos, 4);
            int chunkSize = BitConverter.ToInt32(wav, pos + 4);

            if (chunkId == "fmt ")
            {
                channels = BitConverter.ToInt16(wav, pos + 10);
                sampleRate = BitConverter.ToInt32(wav, pos + 12);
                bitsPerSample = BitConverter.ToInt16(wav, pos + 22);
            }
            else if (chunkId == "data")
            {
                dataChunkPos = pos + 8;
                dataChunkSize = chunkSize;
                break;
            }

            pos += 8 + chunkSize + (chunkSize % 2);
        }

        if (dataChunkPos < 0 || channels == 0 || bitsPerSample == 0 || sampleRate == 0)
            return null;

        int bytesPerSecond = sampleRate * channels * (bitsPerSample / 8);
        int maxBytes = bytesPerSecond * MaxDurationMs / 1000;

        int frameSize = channels * (bitsPerSample / 8);
        maxBytes -= maxBytes % frameSize;

        int newDataSize = Math.Min(dataChunkSize, maxBytes);
        int totalSize = dataChunkPos + newDataSize;

        if (newDataSize == dataChunkSize && wav.Length == totalSize)
            return wav;

        var trimmed = new byte[totalSize];
        Array.Copy(wav, trimmed, totalSize);

        BitConverter.GetBytes(totalSize - 8).CopyTo(trimmed, 4);
        BitConverter.GetBytes(newDataSize).CopyTo(trimmed, dataChunkPos - 4);

        return trimmed;
    }
}
