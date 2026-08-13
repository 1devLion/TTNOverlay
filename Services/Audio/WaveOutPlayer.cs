using System.Runtime.InteropServices;
using System.Text;

namespace TTNOverlay.Services;

/// <summary>
/// Plays short WAV alert sounds through the Windows waveOut API, with output device selection and per-key volume.
/// </summary>
public static class WaveOutPlayer
{
    private const int WaveMapper = -1;
    private const int MmsyserrNoError = 0;

    private static int _deviceId = WaveMapper;

    public static IReadOnlyList<(int Id, string Name)> GetOutputDevices()
    {
        var list = new List<(int, string)> { (WaveMapper, "Predeterminado del sistema") };

        try
        {
            var count = waveOutGetNumDevs();
            for (uint i = 0; i < count; i++)
            {
                var caps = new WAVEOUTCAPS();
                if (
                    waveOutGetDevCaps((UIntPtr)i, ref caps, (uint)Marshal.SizeOf<WAVEOUTCAPS>())
                    == MmsyserrNoError
                )
                    list.Add(((int)i, caps.szPname));
            }
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("WaveOutPlayer.GetOutputDevices", ex);
        }

        return list;
    }

    public static void SetDevice(int deviceId) => _deviceId = deviceId;

    public static void Play(byte[] wav, float volume)
    {
        if (!TryParseWav(wav, out var fmt, out var dataOffset, out var dataLength))
            return;

        var hWaveOut = IntPtr.Zero;
        var bufferHandle = default(GCHandle);
        var headerHandle = default(GCHandle);

        try
        {
            var openResult = waveOutOpen(
                out hWaveOut,
                (UIntPtr)unchecked((uint)_deviceId),
                ref fmt,
                IntPtr.Zero,
                IntPtr.Zero,
                0
            );
            if (openResult != MmsyserrNoError)
                return;

            var vol16 = (ushort)(Math.Clamp(volume, 0f, 1f) * 0xFFFF);
            waveOutSetVolume(hWaveOut, (uint)vol16 | ((uint)vol16 << 16));

            var buffer = new byte[dataLength];
            Array.Copy(wav, dataOffset, buffer, 0, dataLength);
            bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);

            var header = new WAVEHDR
            {
                lpData = bufferHandle.AddrOfPinnedObject(),
                dwBufferLength = (uint)buffer.Length,
            };
            headerHandle = GCHandle.Alloc(header, GCHandleType.Pinned);
            var headerPtr = headerHandle.AddrOfPinnedObject();
            var headerSize = (uint)Marshal.SizeOf<WAVEHDR>();

            waveOutPrepareHeader(hWaveOut, headerPtr, headerSize);
            waveOutWrite(hWaveOut, headerPtr, headerSize);

            var closureWaveOut = hWaveOut;
            var closureBufferHandle = bufferHandle;
            var closureHeaderHandle = headerHandle;
            var durationMs = (int)((long)dataLength * 1000 / Math.Max(1, fmt.nAvgBytesPerSec)) + 150;

            Task.Run(() =>
            {
                Thread.Sleep(durationMs);
                try
                {
                    waveOutReset(closureWaveOut);
                    waveOutUnprepareHeader(closureWaveOut, headerPtr, headerSize);
                    waveOutClose(closureWaveOut);
                }
                catch (Exception ex)
                {
                    DebugLog.WriteException("WaveOutPlayer.Play(cleanup)", ex);
                }
                finally
                {
                    if (closureBufferHandle.IsAllocated)
                        closureBufferHandle.Free();
                    if (closureHeaderHandle.IsAllocated)
                        closureHeaderHandle.Free();
                }
            });
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("WaveOutPlayer.Play", ex);
            if (bufferHandle.IsAllocated)
                bufferHandle.Free();
            if (headerHandle.IsAllocated)
                headerHandle.Free();
            if (hWaveOut != IntPtr.Zero)
                waveOutClose(hWaveOut);
        }
    }

    private static bool TryParseWav(
        byte[] wav,
        out WAVEFORMATEX fmt,
        out int dataOffset,
        out int dataLength
    )
    {
        fmt = default;
        dataOffset = 0;
        dataLength = 0;

        if (wav.Length < 44 || wav[0] != 'R' || wav[1] != 'I' || wav[2] != 'F' || wav[3] != 'F')
            return false;

        var pos = 12;
        while (pos + 8 <= wav.Length)
        {
            var chunkId = Encoding.ASCII.GetString(wav, pos, 4);
            var chunkSize = BitConverter.ToInt32(wav, pos + 4);

            if (chunkId == "fmt ")
            {
                fmt.wFormatTag = BitConverter.ToInt16(wav, pos + 8);
                fmt.nChannels = BitConverter.ToInt16(wav, pos + 10);
                fmt.nSamplesPerSec = BitConverter.ToInt32(wav, pos + 12);
                fmt.nAvgBytesPerSec = BitConverter.ToInt32(wav, pos + 16);
                fmt.nBlockAlign = BitConverter.ToInt16(wav, pos + 20);
                fmt.wBitsPerSample = BitConverter.ToInt16(wav, pos + 22);
                fmt.cbSize = 0;
            }
            else if (chunkId == "data")
            {
                dataOffset = pos + 8;
                dataLength = chunkSize;
                break;
            }

            pos += 8 + chunkSize + (chunkSize % 2);
        }

        return dataOffset > 0 && fmt.nAvgBytesPerSec > 0;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEFORMATEX
    {
        public short wFormatTag;
        public short nChannels;
        public int nSamplesPerSec;
        public int nAvgBytesPerSec;
        public short nBlockAlign;
        public short wBitsPerSample;
        public short cbSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WAVEHDR
    {
        public IntPtr lpData;
        public uint dwBufferLength;
        public uint dwBytesRecorded;
        public IntPtr dwUser;
        public uint dwFlags;
        public uint dwLoops;
        public IntPtr lpNext;
        public IntPtr reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WAVEOUTCAPS
    {
        public short wMid;
        public short wPid;
        public uint vDriverVersion;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szPname;
        public uint dwFormats;
        public short wChannels;
        public short wReserved1;
        public uint dwSupport;
    }

    [DllImport("winmm.dll")]
    private static extern uint waveOutGetNumDevs();

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int waveOutGetDevCaps(
        UIntPtr uDeviceID,
        ref WAVEOUTCAPS pwoc,
        uint cbwoc
    );

    [DllImport("winmm.dll")]
    private static extern int waveOutOpen(
        out IntPtr hWaveOut,
        UIntPtr uDeviceID,
        ref WAVEFORMATEX lpFormat,
        IntPtr dwCallback,
        IntPtr dwInstance,
        uint dwFlags
    );

    [DllImport("winmm.dll")]
    private static extern int waveOutPrepareHeader(
        IntPtr hWaveOut,
        IntPtr lpWaveOutHdr,
        uint uSize
    );

    [DllImport("winmm.dll")]
    private static extern int waveOutUnprepareHeader(
        IntPtr hWaveOut,
        IntPtr lpWaveOutHdr,
        uint uSize
    );

    [DllImport("winmm.dll")]
    private static extern int waveOutWrite(IntPtr hWaveOut, IntPtr lpWaveOutHdr, uint uSize);

    [DllImport("winmm.dll")]
    private static extern int waveOutClose(IntPtr hWaveOut);

    [DllImport("winmm.dll")]
    private static extern int waveOutReset(IntPtr hWaveOut);

    [DllImport("winmm.dll")]
    private static extern int waveOutSetVolume(IntPtr hWaveOut, uint dwVolume);
}
