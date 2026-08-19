using System.Runtime.InteropServices;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using TTNOverlay.Native;

namespace TTNOverlay.Overlay;

/// <summary>
/// Base class for all native overlay windows: Win32 window class registration, message loop plumbing, and shared window behavior.
/// </summary>
public abstract class OverlayWindowBase : IDisposable
{
    private const uint ClassStyle = 0x0002 | 0x0001;
    private readonly Win32.WndProcDelegate _wndProcDelegate;
    private readonly string _className;
    private IntPtr _hInstance;

    private bool _ownsWindowClass;
    private IntPtr _ownedIconHandle;
    private bool _clickThrough;
    private LayeredRenderer? _renderer;
    private readonly System.Collections.Concurrent.ConcurrentQueue<Action> _uiThreadQueue = new();

    private bool _renderDirty;
    private System.Threading.Timer? _renderLoopTimer;

    private const int RenderTargetFps = 60;

    private const int MaxUiActionsPerDrain = 256;

    private int _pendingUiActionCount;

    protected int PendingUiActionCount =>
        System.Threading.Volatile.Read(ref _pendingUiActionCount);

    protected virtual int TitleBarHeight => 22;

    protected virtual int ResizeGripSize => 16;

    protected virtual int MinimumClientWidth => 100;
    protected virtual int MinimumClientHeight => 60;

    public IntPtr Hwnd { get; private set; }

    /// <summary>Managed thread id of the app's single UI/message-loop thread. Set once, by whichever overlay window is created first.</summary>
    private static int? _uiThreadId;
    public static bool IsOnUiThread => _uiThreadId is int id && Environment.CurrentManagedThreadId == id;

    private bool _inLiveResize;
    private DateTime _lastLiveResizeRenderUtc = DateTime.MinValue;
    private static readonly TimeSpan LiveResizeRenderInterval = TimeSpan.FromMilliseconds(50);

    public event Action? Destroyed;

    protected OverlayWindowBase(string className)
    {
        _className = className;
        _wndProcDelegate = WndProc;
    }

    protected virtual void OnDeviceResourcesInvalidated() { }

    public void Create(string title, int x, int y, int width, int height, bool visible = true)
    {
        _uiThreadId ??= Environment.CurrentManagedThreadId;

        _hInstance = Marshal.GetHINSTANCE(GetType().Module);

        IntPtr appIcon = LoadApplicationIconHandle(out _ownedIconHandle);

        var wndClass = new Win32.WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<Win32.WNDCLASSEX>(),
            style = ClassStyle,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate),
            hInstance = _hInstance,
            hCursor = Win32.LoadCursor(IntPtr.Zero, 32512),
            hIcon = appIcon,
            hIconSm = appIcon,
            lpszClassName = _className,
        };

        _ownsWindowClass = Win32.RegisterClassEx(ref wndClass) != 0;

        int exStyle = Win32.WS_EX_LAYERED | Win32.WS_EX_TOPMOST | Win32.WS_EX_APPWINDOW;

        Hwnd = Win32.CreateWindowEx(
            exStyle,
            _className,
            title,
            Win32.WS_POPUP | (visible ? Win32.WS_VISIBLE : 0),
            x,
            y,
            width,
            height,
            IntPtr.Zero,
            IntPtr.Zero,
            _hInstance,
            IntPtr.Zero
        );

        Win32.SetWindowText(Hwnd, title);

        _renderer = new LayeredRenderer(Hwnd, SharedGraphicsResources.D2DFactory, SharedGraphicsResources.DWriteFactory);
        _renderer.TargetRecreated += OnDeviceResourcesInvalidated;
        _renderer.Resize(width, height);

        StartRenderLoop();
        try
        {
            OnCreated();
        }
        catch (Exception ex)
        {
            DebugLog.Write($"OverlayWindowBase.Create: unhandled exception in OnCreated ({_className}). {ex}");
            DebugLog.FlushNow();
            throw;
        }

        Win32.SetFocus(Hwnd);
        RequestRender();
    }

    /// <summary>
    /// Reveals a window created with <c>visible: false</c> (e.g. the main overlay held back until a
    /// startup check like an update prompt has been resolved). Safe to call from any thread (hops to
    /// the UI thread internally); no-op if the window is already visible.
    /// </summary>
    public void ShowWindow()
    {
        PostToUiThread(() =>
        {
            Win32.ShowWindow(Hwnd, Win32.SW_SHOW);
            RequestRender();
        });
    }

    /// <summary>
    /// Extracts the app's own icon for use as the window class icon.
    /// <paramref name="ownedHandle"/> is set to the returned handle when it came from ExtractIconEx
    /// (the caller then owns it and must DestroyIcon it when the window is disposed), or IntPtr.Zero
    /// when we fell back to a shared system icon via LoadIcon (which must NOT be destroyed).
    /// </summary>
    private static IntPtr LoadApplicationIconHandle(out IntPtr ownedHandle)
    {
        ownedHandle = IntPtr.Zero;
        try
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exePath))
            {
                var large = new IntPtr[1];
                var small = new IntPtr[1];
                int count = Win32.ExtractIconEx(exePath, 0, large, small, 1);
                if (count > 0)
                {
                    if (small[0] != IntPtr.Zero)
                    {
                        if (large[0] != IntPtr.Zero && large[0] != small[0])
                            Win32.DestroyIcon(large[0]);
                        ownedHandle = small[0];
                        return small[0];
                    }

                    if (large[0] != IntPtr.Zero)
                    {
                        ownedHandle = large[0];
                        return large[0];
                    }
                }
            }
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("OverlayWindowBase.LoadApplicationIconHandle", ex);
        }

        return IntPtr.Zero;
    }

    public void RunMessageLoop()
    {
        while (Win32.GetMessage(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            Win32.TranslateMessage(ref msg);
            Win32.DispatchMessage(ref msg);
        }
    }

    public void SetClickThrough(bool enabled)
    {
        _clickThrough = enabled;
        int style = Win32.GetWindowLong(Hwnd, Win32.GWL_EXSTYLE);
        style = enabled ? style | Win32.WS_EX_TRANSPARENT : style & ~Win32.WS_EX_TRANSPARENT;
        Win32.SetWindowLong(Hwnd, Win32.GWL_EXSTYLE, style);
    }

    public void Resize(int width, int height)
    {
        Win32.SetWindowPos(
            Hwnd,
            IntPtr.Zero,
            0,
            0,
            width,
            height,
            Win32.SWP_NOMOVE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE
        );
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            return WndProcCore(hWnd, msg, wParam, lParam);
        }
        catch (Exception ex)
        {
            DebugLog.Write($"WndProc: unhandled exception in msg=0x{msg:X4}. {ex}");
            DebugLog.FlushNow();
            return Win32.DefWindowProc(hWnd, msg, wParam, lParam);
        }
    }

    private IntPtr WndProcCore(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case Win32.WM_NCHITTEST:
                var hit = HitTest(lParam);
                if (hit.HasValue)
                    return (IntPtr)hit.Value;
                break;

            case Win32.WM_SETCURSOR:

                if (unchecked((short)(lParam.ToInt64() & 0xFFFF)) == Win32.HTBOTTOMRIGHT)
                {
                    Win32.SetCursor(Win32.LoadCursor(IntPtr.Zero, Win32.IDC_SIZENWSE));
                    return (IntPtr)1;
                }
                break;

            case Win32.WM_PAINT:

                Win32.ValidateRect(hWnd, IntPtr.Zero);
                return IntPtr.Zero;

            case Win32.WM_GETMINMAXINFO:

                var mmi = Marshal.PtrToStructure<Win32.MINMAXINFO>(lParam);
                mmi.ptMinTrackSize = new Win32.POINT { X = MinimumClientWidth, Y = MinimumClientHeight };
                Marshal.StructureToPtr(mmi, lParam, false);
                return IntPtr.Zero;

            case Win32.WM_SIZE:
                Win32.GetClientRect(hWnd, out var rect);
                int width = rect.Right - rect.Left;
                int height = rect.Bottom - rect.Top;

                if (_inLiveResize && DateTime.UtcNow - _lastLiveResizeRenderUtc < LiveResizeRenderInterval)
                    return IntPtr.Zero;
                _lastLiveResizeRenderUtc = DateTime.UtcNow;

                _renderer?.Resize(width, height, allowShrink: !_inLiveResize);
                OnResize(width, height);
                RequestRender();
                return IntPtr.Zero;

            case Win32.WM_ENTERSIZEMOVE:
                _inLiveResize = true;
                return IntPtr.Zero;

            case Win32.WM_EXITSIZEMOVE:
                _inLiveResize = false;
                Win32.GetClientRect(hWnd, out var finalRect);
                _renderer?.Resize(finalRect.Right - finalRect.Left, finalRect.Bottom - finalRect.Top, allowShrink: true);
                RequestRender();
                #if DEBUG
                SharedGraphicsResources.DumpLiveD2DObjects("post-resize");
                #endif
                return IntPtr.Zero;

            case Win32.WM_LBUTTONDOWN:

                int ldX = unchecked((short)(lParam.ToInt64() & 0xFFFF));
                int ldY = unchecked((short)((lParam.ToInt64() >> 16) & 0xFFFF));
                OnClientLButtonDown(ldX, ldY);
                break;

            case Win32.WM_LBUTTONUP:

                int lbX = unchecked((short)(lParam.ToInt64() & 0xFFFF));
                int lbY = unchecked((short)((lParam.ToInt64() >> 16) & 0xFFFF));
                OnClientLButtonUp(lbX, lbY);

                Win32.ReleaseCapture();
                break;

            case Win32.WM_MOUSEMOVE:

                int mmX = unchecked((short)(lParam.ToInt64() & 0xFFFF));
                int mmY = unchecked((short)((lParam.ToInt64() >> 16) & 0xFFFF));

                var tme = new Win32.TRACKMOUSEEVENT
                {
                    cbSize = (uint)Marshal.SizeOf<Win32.TRACKMOUSEEVENT>(),
                    dwFlags = Win32.TME_LEAVE,
                    hwndTrack = hWnd,
                    dwHoverTime = 0,
                };
                Win32.TrackMouseEvent(ref tme);

                OnClientMouseMove(mmX, mmY);
                return IntPtr.Zero;

            case Win32.WM_MOUSELEAVE:
                OnClientMouseLeave();
                return IntPtr.Zero;

            case Win32.WM_MOUSEWHEEL:

                int wheelDelta = unchecked((short)((wParam.ToInt64() >> 16) & 0xFFFF));
                var wheelPt = new Win32.POINT
                {
                    X = unchecked((short)(lParam.ToInt64() & 0xFFFF)),
                    Y = unchecked((short)((lParam.ToInt64() >> 16) & 0xFFFF)),
                };
                Win32.ScreenToClient(hWnd, ref wheelPt);
                OnMouseWheel(wheelDelta, wheelPt.X, wheelPt.Y);
                return IntPtr.Zero;

            case Win32.WM_KEYDOWN:

                bool ctrlDown = (Win32.GetKeyState(Win32.VK_CONTROL) & 0x8000) != 0;
                bool shiftDown = (Win32.GetKeyState(Win32.VK_SHIFT) & 0x8000) != 0;
                OnKeyDown((int)wParam.ToInt64(), ctrlDown, shiftDown);
                return IntPtr.Zero;

            case Win32.WM_CHAR:

                char ch = (char)wParam.ToInt64();
                if (!char.IsControl(ch))
                    OnChar(ch);
                return IntPtr.Zero;

            case Win32.WM_SETFOCUS:
                OnWindowFocusGained();
                return IntPtr.Zero;

            case Win32.WM_KILLFOCUS:
                OnWindowFocusLost();
                return IntPtr.Zero;

            case Win32.WM_HOTKEY:
            case Win32.WM_APP + 1:
                if (OnCustomMessage(msg, wParam, lParam))
                    return IntPtr.Zero;
                break;

            case Win32.WM_UI_THREAD_CALLBACK:

                System.Threading.Interlocked.Exchange(ref _uiCallbackPosted, 0);

                int drained = 0;
                while (drained < MaxUiActionsPerDrain && _uiThreadQueue.TryDequeue(out var action))
                {
                    try
                    {
                        action();
                    }
                    catch (Exception ex)
                    {
                        DebugLog.Write($"WM_UI_THREAD_CALLBACK: exception in glued action. {ex}");
                    }
                    System.Threading.Interlocked.Decrement(ref _pendingUiActionCount);
                    drained++;
                }

                if (
                    !_uiThreadQueue.IsEmpty
                    && System.Threading.Interlocked.CompareExchange(ref _uiCallbackPosted, 1, 0) == 0
                )
                    Win32.PostMessage(hWnd, Win32.WM_UI_THREAD_CALLBACK, IntPtr.Zero, IntPtr.Zero);
                return IntPtr.Zero;

            case Win32.WM_CLOSE:
                if (!OnClosing())
                    return IntPtr.Zero;
                Win32.DestroyWindow(hWnd);
                return IntPtr.Zero;

            case Win32.WM_DESTROY:

                StopRenderLoop();
                OnDestroyed();

                // The window is gone at the OS level the moment DestroyWindow() returns (which
                // already happened by the time we get here — WM_DESTROY is sent synchronously
                // from within DestroyWindow). Clear Hwnd now so a later Dispose() call (e.g. the
                // one queued by ReleaseNotesDialogWindow.Show via the Destroyed event below)
                // doesn't call DestroyWindow a second time on an already-invalid handle.
                Hwnd = IntPtr.Zero;

                Destroyed?.Invoke();

                if (QuitApplicationOnDestroy)
                    Win32.PostQuitMessage(0);
                return IntPtr.Zero;
        }

        return Win32.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private int? HitTest(IntPtr lParam)
    {
        if (_clickThrough)
            return null;

        int x = unchecked((short)(lParam.ToInt64() & 0xFFFF));
        int y = unchecked((short)((lParam.ToInt64() >> 16) & 0xFFFF));
        var pt = new Win32.POINT { X = x, Y = y };
        Win32.ScreenToClient(Hwnd, ref pt);
        Win32.GetClientRect(Hwnd, out var client);

        if (pt.X >= client.Right - ResizeGripSize && pt.Y >= client.Bottom - ResizeGripSize)
            return Win32.HTBOTTOMRIGHT;

        if (pt.Y <= TitleBarHeight && IsInDraggableTitleBarArea(pt.X, pt.Y))
            return Win32.HTCAPTION;

        return Win32.HTCLIENT;
    }

    protected virtual bool IsInDraggableTitleBarArea(int clientX, int clientY) => true;

    protected virtual void OnClientLButtonUp(int clientX, int clientY) { }

    protected virtual void OnClientLButtonDown(int clientX, int clientY) { }

    protected virtual void OnKeyDown(int virtualKeyCode, bool ctrlDown, bool shiftDown) { }

    protected virtual void OnChar(char c) { }

    protected virtual void OnWindowFocusGained() { }

    protected virtual void OnWindowFocusLost() { }

    protected virtual void OnClientMouseMove(int clientX, int clientY) { }

    protected virtual void OnClientMouseLeave() { }

    protected virtual void OnMouseWheel(int delta, int clientX, int clientY) { }

    protected virtual void OnCreated() { }

    protected void RequestRender()
    {
        _renderDirty = true;
        EnsureRenderLoopRunning();
    }

    private DateTime _lastLiveMoveRenderUtc = DateTime.MinValue;
    private static readonly TimeSpan LiveMoveRenderInterval = TimeSpan.FromMilliseconds(50);

    private void RenderIfDirty()
    {
        if (!_renderDirty)
        {
            PauseRenderLoop();
            return;
        }

        if (_inLiveResize)
        {
            var now = DateTime.UtcNow;
            if (now - _lastLiveMoveRenderUtc < LiveMoveRenderInterval)
                return;
            _lastLiveMoveRenderUtc = now;
        }

        _renderDirty = false;
        _renderer?.Render(OnRender);

        // Nothing re-marked dirty during this render (by OnRender itself, or by anything else
        // that ran on the UI thread before this callback), so there's nothing to wait for —
        // pause instead of continuing to tick at RenderTargetFps while idle.
        if (!_renderDirty)
            PauseRenderLoop();
    }

    /// <summary>
    /// True while _renderLoopTimer is actively ticking at RenderTargetFps. Distinct from whether
    /// the Timer object itself exists (that lives for the whole window lifetime, see
    /// StartRenderLoop/StopRenderLoop) — this tracks whether it's currently *running*, so idle
    /// windows (nothing dirty, no animation in flight) don't wake up 60 times a second for
    /// nothing. Only ever read/written on the UI thread: RequestRender/RenderIfDirty/PauseRenderLoop
    /// all run there (RequestRender is always called from UI-thread code, and the timer callback
    /// itself hands off to the UI thread via PostToUiThread before touching any of this state).
    /// </summary>
    private bool _renderLoopActive;

    private void EnsureRenderLoopRunning()
    {
        if (_renderLoopActive || _renderLoopTimer is null)
            return;
        _renderLoopActive = true;
        _renderLoopTimer.Change(0, 1000 / RenderTargetFps);
    }

    private void PauseRenderLoop()
    {
        if (!_renderLoopActive)
            return;
        _renderLoopActive = false;
        _renderLoopTimer?.Change(System.Threading.Timeout.Infinite, System.Threading.Timeout.Infinite);
    }

    private void StartRenderLoop()
    {
        // Created paused (Infinite/Infinite) — RequestRender() is what actually starts it
        // ticking, and RenderIfDirty pauses it again once nothing is left to draw. This avoids
        // waking every overlay window up at RenderTargetFps forever, even while idle/hidden.
        _renderLoopTimer ??= new System.Threading.Timer(
            _ => PostToUiThread(RenderIfDirty),
            null,
            System.Threading.Timeout.Infinite,
            System.Threading.Timeout.Infinite
        );
    }

    private void StopRenderLoop()
    {
        _renderLoopTimer?.Dispose();
        _renderLoopTimer = null;
        _renderLoopActive = false;
    }

    protected abstract void OnRender(ID2D1DCRenderTarget target);

    protected IDWriteFactory DWriteFactory =>
        _renderer?.DWriteFactory ?? throw new InvalidOperationException("The window has not been created yet (call Create() first).");

    protected virtual void OnResize(int width, int height) { }

    protected virtual bool QuitApplicationOnDestroy => true;

    protected virtual bool OnClosing() => true;

    protected virtual void OnDestroyed() { }

    protected virtual bool OnCustomMessage(uint msg, IntPtr wParam, IntPtr lParam) => false;

    private int _uiCallbackPosted;

    protected void PostToUiThread(Action action)
    {
        _uiThreadQueue.Enqueue(action);
        System.Threading.Interlocked.Increment(ref _pendingUiActionCount);
        if (System.Threading.Interlocked.CompareExchange(ref _uiCallbackPosted, 1, 0) == 0)
            Win32.PostMessage(Hwnd, Win32.WM_UI_THREAD_CALLBACK, IntPtr.Zero, IntPtr.Zero);
    }

    internal void CaptureMouse() => Win32.SetCapture(Hwnd);

    internal void ShowCaretAt(int clientX, int clientY, int height)
    {
        Win32.CreateCaret(Hwnd, IntPtr.Zero, 1, height);
        Win32.SetCaretPos(clientX, clientY);
        Win32.ShowCaret(Hwnd);
    }

    internal void HideAndDestroyCaret()
    {
        Win32.HideCaret(Hwnd);
        Win32.DestroyCaret();
    }

    internal static bool SetClipboardText(string text)
    {
        if (!Win32.OpenClipboard(IntPtr.Zero))
            return false;
        try
        {
            Win32.EmptyClipboard();
            int bytes = (text.Length + 1) * 2;
            IntPtr hMem = Win32.GlobalAlloc(Win32.GMEM_MOVEABLE, (UIntPtr)bytes);
            if (hMem == IntPtr.Zero)
                return false;

            IntPtr ptr = Win32.GlobalLock(hMem);
            if (ptr == IntPtr.Zero)
                return false;
            try
            {
                Marshal.Copy((text + '\0').ToCharArray(), 0, ptr, text.Length + 1);
            }
            finally
            {
                Win32.GlobalUnlock(hMem);
            }

            Win32.SetClipboardData(Win32.CF_UNICODETEXT, hMem);
            return true;
        }
        finally
        {
            Win32.CloseClipboard();
        }
    }

    internal static string GetClipboardText()
    {
        if (!Win32.OpenClipboard(IntPtr.Zero))
            return "";
        try
        {
            IntPtr hMem = Win32.GetClipboardData(Win32.CF_UNICODETEXT);
            if (hMem == IntPtr.Zero)
                return "";

            IntPtr ptr = Win32.GlobalLock(hMem);
            if (ptr == IntPtr.Zero)
                return "";
            try
            {
                return Marshal.PtrToStringUni(ptr) ?? "";
            }
            finally
            {
                Win32.GlobalUnlock(hMem);
            }
        }
        finally
        {
            Win32.CloseClipboard();
        }
    }

    public void Dispose()
    {
        if (Hwnd != IntPtr.Zero)
        {
            Win32.DestroyWindow(Hwnd);
            Hwnd = IntPtr.Zero;
        }

        if (_ownsWindowClass)
        {
            Win32.UnregisterClass(_className, _hInstance);
            _ownsWindowClass = false;
        }
        _renderer?.Dispose();
        _renderer = null;

        if (_ownedIconHandle != IntPtr.Zero)
        {
            Win32.DestroyIcon(_ownedIconHandle);
            _ownedIconHandle = IntPtr.Zero;
        }

        GC.SuppressFinalize(this);
    }
}