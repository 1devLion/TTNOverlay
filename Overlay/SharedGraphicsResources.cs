using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.DXGI.Debug;

namespace TTNOverlay.Overlay;

/// <summary>
/// Process-wide Direct2D/DirectWrite factories shared by all windows, created once and released on shutdown.
/// </summary>
internal static class SharedGraphicsResources
{
    private static ID2D1Factory1? _d2dFactory;
    private static IDWriteFactory? _dwriteFactory;

    private static readonly Guid DxgiDebugAll = new("e48ae283-da80-490b-87e6-43e9a9cfda08");

#if DEBUG
    private const DebugLevel D2DDebugLevel = DebugLevel.Information;
#else
    private const DebugLevel D2DDebugLevel = DebugLevel.None;
#endif

    public static ID2D1Factory1 D2DFactory =>
        _d2dFactory ??= D2D1.D2D1CreateFactory<ID2D1Factory1>(
            Vortice.Direct2D1.FactoryType.MultiThreaded,
            D2DDebugLevel
        );

    public static IDWriteFactory DWriteFactory =>
        _dwriteFactory ??= DWrite.DWriteCreateFactory<IDWriteFactory>(Vortice.DirectWrite.FactoryType.Shared);

    public static void DumpLiveD2DObjects(string label)
    {
        try
        {
            DebugLog.Write($"DumpLiveD2DObjects: arrancando ({label})");

            using var dxgiDebug = DXGI.DXGIGetDebugInterface1<IDXGIDebug1>();
            dxgiDebug.ReportLiveObjects(DxgiDebugAll, ReportLiveObjectFlags.Detail | ReportLiveObjectFlags.IgnoreInternal);

            using var infoQueue = DXGI.DXGIGetDebugInterface1<IDXGIInfoQueue>();
            ulong count = infoQueue.GetNumStoredMessages(DxgiDebugAll);
            DebugLog.Write($"DumpLiveD2DObjects: {count} mensajes en cola ({label})");

            for (ulong i = 0; i < count; i++)
            {
                var message = infoQueue.GetMessage(DxgiDebugAll, i);
                DebugLog.Write($"  [{label}] {message.Description}");
            }
            infoQueue.ClearStoredMessages(DxgiDebugAll);

            DebugLog.Write($"DumpLiveD2DObjects: terminó ({label})");
        }
        catch (Exception ex)
        {
            DebugLog.Write($"DumpLiveD2DObjects: EXCEPCIÓN ({label}) -- {ex}");
        }
        DebugLog.FlushNow();
    }

    public static void Shutdown()
    {
        _dwriteFactory?.Dispose();
        _dwriteFactory = null;
        _d2dFactory?.Dispose();
        _d2dFactory = null;
    }

    internal static class MemoryDiag
    {
        [System.Diagnostics.Conditional("DEBUG")]
        public static void Log(string label)
        {
            using var proc = System.Diagnostics.Process.GetCurrentProcess();
            proc.Refresh();
            DebugLog.Write(
                $"MEM[{label}]: WorkingSet={proc.WorkingSet64 / 1024 / 1024}MB " +
                $"Private={proc.PrivateMemorySize64 / 1024 / 1024}MB"
            );
        }
    }
}