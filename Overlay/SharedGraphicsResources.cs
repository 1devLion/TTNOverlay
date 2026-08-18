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

    // ChatRenderWindow corre en el hilo principal y SettingsRenderWindow corre en su propio hilo
    // dedicado (ver RunSettingsWindow en ChatRenderWindow.TrayHotkeys.cs); ambos pueden llegar a pedir
    // estos factories "por primera vez" en paralelo (p.ej. si Configuración se abre apenas arranca la
    // app, antes de que el hilo principal ya los haya inicializado). El "??=" de abajo no es atómico:
    // sin este lock, dos hilos podrían ver _d2dFactory==null a la vez, crear DOS factories, y que el
    // que "pierde" la carrera quede huérfano mientras otro código ya tiene una referencia viva a él.
    private static readonly object InitLock = new();

    private static readonly Guid DxgiDebugAll = new("e48ae283-da80-490b-87e6-43e9a9cfda08");

#if DEBUG
    private const DebugLevel D2DDebugLevel = DebugLevel.Information;
#else
    private const DebugLevel D2DDebugLevel = DebugLevel.None;
#endif

    public static ID2D1Factory1 D2DFactory
    {
        get
        {
            if (_d2dFactory is not null)
                return _d2dFactory;
            lock (InitLock)
            {
                return _d2dFactory ??= D2D1.D2D1CreateFactory<ID2D1Factory1>(
                    Vortice.Direct2D1.FactoryType.MultiThreaded,
                    D2DDebugLevel
                );
            }
        }
    }

    public static IDWriteFactory DWriteFactory
    {
        get
        {
            if (_dwriteFactory is not null)
                return _dwriteFactory;
            lock (InitLock)
            {
                return _dwriteFactory ??= DWrite.DWriteCreateFactory<IDWriteFactory>(Vortice.DirectWrite.FactoryType.Shared);
            }
        }
    }

    #if DEBUG
    public static void DumpLiveD2DObjects(string label)
    {
        try
        {
            DebugLog.Write($"DumpLiveD2DObjects: Starting ({label})");

            using var dxgiDebug = DXGI.DXGIGetDebugInterface1<IDXGIDebug1>();
            dxgiDebug.ReportLiveObjects(DxgiDebugAll, ReportLiveObjectFlags.Detail | ReportLiveObjectFlags.IgnoreInternal);

            using var infoQueue = DXGI.DXGIGetDebugInterface1<IDXGIInfoQueue>();
            ulong count = infoQueue.GetNumStoredMessages(DxgiDebugAll);
            DebugLog.Write($"DumpLiveD2DObjects: {count} queued messages ({label})");

            for (ulong i = 0; i < count; i++)
            {
                var message = infoQueue.GetMessage(DxgiDebugAll, i);
                DebugLog.Write($"  [{label}] {message.Description}");
            }
            infoQueue.ClearStoredMessages(DxgiDebugAll);

            DebugLog.Write($"DumpLiveD2DObjects: finished ({label})");
        }
        catch (Exception ex)
        {
            DebugLog.Write($"DumpLiveD2DObjects: EXCEPTION ({label}). {ex}");
        }
        DebugLog.FlushNow();
    }
    #endif

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