using System.Drawing;
using System.Runtime.InteropServices;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.DXGI;
using Vortice.Mathematics;
using TTNOverlay.Native;
using static TTNOverlay.Overlay.SharedGraphicsResources;

namespace TTNOverlay.Overlay;

/// <summary>
/// Owns the Direct2D render target and GDI bitmap used to composite a layered (translucent) native window via UpdateLayeredWindow.
/// </summary>
internal sealed class LayeredRenderer : IDisposable
{
    private readonly IntPtr _hwnd;
    private int _width;
    private int _height;

    private int _allocatedWidth;
    private int _allocatedHeight;
    private IntPtr _memDc;
    private IntPtr _dibBitmap;

    private readonly ID2D1Factory1 _d2dFactory;
    private readonly RenderTargetProperties _renderTargetProps;
    private ID2D1DCRenderTarget _target;
    private readonly IDWriteFactory _dwriteFactory;

    public ID2D1DCRenderTarget Target => _target;
    public IDWriteFactory DWriteFactory => _dwriteFactory;

    public LayeredRenderer(IntPtr hwnd, ID2D1Factory1 d2dFactory, IDWriteFactory dwriteFactory)
    {
        _hwnd = hwnd;
        _d2dFactory = d2dFactory;
        _dwriteFactory = dwriteFactory;

        _renderTargetProps = new RenderTargetProperties(
            RenderTargetType.Default,
            new Vortice.DCommon.PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
            0f,
            0f,
            RenderTargetUsage.None,
            Vortice.Direct2D1.FeatureLevel.Default
        );
        _target = _d2dFactory.CreateDCRenderTarget(_renderTargetProps);

        _memDc = Win32.CreateCompatibleDC(IntPtr.Zero);
    }

    public event Action? TargetRecreated;
    public void Resize(int width, int height, bool allowShrink = false)
    {
        if (width <= 0 || height <= 0)
            return;

        _width = width;
        _height = height;

        bool needsGrow = width > _allocatedWidth || height > _allocatedHeight;
        bool needsShrink = allowShrink && (width < _allocatedWidth || height < _allocatedHeight);

        if (!needsGrow && !needsShrink)
            return;

        MemoryDiag.Log($"Resize:antes w={width} h={height} shrink={needsShrink}");

        _allocatedWidth = needsShrink ? width : Math.Max(width, _allocatedWidth);
        _allocatedHeight = needsShrink ? height : Math.Max(height, _allocatedHeight);

        var bmi = new Win32.BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<Win32.BITMAPINFOHEADER>(),
            biWidth = _allocatedWidth,
            biHeight = -_allocatedHeight,
            biPlanes = 1,
            biBitCount = 32,
            biCompression = Win32.BI_RGB,
        };

        var newBitmap = Win32.CreateDIBSection(_memDc, ref bmi, Win32.DIB_RGB_COLORS, out _, IntPtr.Zero, 0);
        if (newBitmap == IntPtr.Zero)
        {
            DebugLog.Write($"CreateDIBSection FAILED, err={Marshal.GetLastWin32Error()} (w={width} h={height})");
        }
        else if (Win32.SelectObject(_memDc, newBitmap) == IntPtr.Zero)
        {
            DebugLog.Write($"SelectObject FAILED, err={Marshal.GetLastWin32Error()}");
        }
        Win32.SelectObject(_memDc, newBitmap);
        if (_dibBitmap != IntPtr.Zero)
            Win32.DeleteObject(_dibBitmap);
        _dibBitmap = newBitmap;

        MemoryDiag.Log("Resize:After-DIB");
        _needsRebind = true;

        if (needsShrink)
        {
            _target.Dispose();
            _target = _d2dFactory.CreateDCRenderTarget(_renderTargetProps);
            MemoryDiag.Log("Resize:After-recreate-target");
            TargetRecreated?.Invoke();
            MemoryDiag.Log("Resize:After-TargetRecreated-invoke");
        }
    }

    private int _renderCount;
    private bool _needsRebind = true;
    private bool _inRender;
    public void Render(Action<ID2D1DCRenderTarget> draw)
    {
        if (_memDc == IntPtr.Zero || _width <= 0 || _height <= 0)
            return;

        if (_inRender)
        {
            DebugLog.Write("LayeredRenderer.Render: LLAMADA REENTRANTE detectada -- se ignora esta pasada para no corromper el render target en curso");
            return;
        }
        _inRender = true;
        try
        {
            if (_needsRebind)
            {
                _target.BindDC(_memDc, new Rectangle(0, 0, _allocatedWidth, _allocatedHeight));
                _needsRebind = false;
            }

            _target.BeginDraw();
            _target.Clear(new Color4(0f, 0f, 0f, 0f));
            draw(_target);
            _target.EndDraw();
            UploadToScreen();

            if (++_renderCount % 30 == 0)
                MemoryDiag.Log($"Render #{_renderCount} (w={_width} h={_height})");
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("LayeredRenderer.Render", ex);
        }
        finally
        {
            _inRender = false;
        }
    }

    private void UploadToScreen()
    {
        var hdcScreen = Win32.GetDC(IntPtr.Zero);

        var pptSrc = new Win32.POINT { X = 0, Y = 0 };
        var sizeWnd = new Win32.SIZE { cx = _width, cy = _height };
        var blend = new Win32.BLENDFUNCTION
        {
            BlendOp = Win32.AC_SRC_OVER,
            BlendFlags = 0,
            SourceConstantAlpha = 255,

            AlphaFormat = Win32.AC_SRC_ALPHA,
        };

        bool ok = Win32.UpdateLayeredWindow(
    _hwnd, hdcScreen, IntPtr.Zero, ref sizeWnd,
    _memDc, ref pptSrc, 0, ref blend, Win32.ULW_ALPHA
);

        if (!ok)
        {
            int err = Marshal.GetLastWin32Error();
            DebugLog.Write($"UpdateLayeredWindow FAILED, err={err} (w={_width} h={_height} allocW={_allocatedWidth} allocH={_allocatedHeight})");
        }

        Win32.ReleaseDC(IntPtr.Zero, hdcScreen);
    }

    public void Dispose()
    {

        _target.Dispose();

        if (_dibBitmap != IntPtr.Zero)
            Win32.DeleteObject(_dibBitmap);
        if (_memDc != IntPtr.Zero)
            Win32.DeleteDC(_memDc);
    }
}