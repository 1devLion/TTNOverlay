using Vortice.DirectWrite;
using Vortice.Mathematics;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay.Controls;

/// <summary>
/// Reusable single-line text box control drawn with Direct2D, backed by TextEditController for caret/selection handling.
/// </summary>
public sealed class TextBox
{
    private readonly TextEditController _edit = new();

    public bool IsFocused { get; private set; }

    private bool _dragging;

    public bool HasSelection => _edit.HasSelection;

    public int SelectionStart => _edit.SelectionStart;

    public int SelectionEnd => _edit.SelectionEnd;

    public bool IsPassword { get; set; }

    public int? MaxLength
    {
        get => _edit.MaxLength;
        set => _edit.MaxLength = value;
    }

    public string Text
    {
        get => _edit.Text;
        set => _edit.SetText(value);
    }

    public int CaretIndex => _edit.Caret;

    public bool CaretVisibleThisFrame { get; private set; } = true;

    public void Focus()
    {
        IsFocused = true;
        CaretVisibleThisFrame = true;
    }

    public void Blur()
    {
        IsFocused = false;
        _dragging = false;
        _edit.ClearSelection();
    }

    public bool TickBlink()
    {
        if (!IsFocused)
            return false;
        CaretVisibleThisFrame = !CaretVisibleThisFrame;
        return true;
    }

    public bool RevealPassword { get; set; }

    private string DisplayText => IsPassword && !RevealPassword ? new string('\u25CF', _edit.Text.Length) : _edit.Text;

    public void HandleClick(IDWriteFactory dwriteFactory, IDWriteTextFormat format, Rect bounds, int clientX, bool shiftDown, OverlayWindowBase host)
    {
        Focus();
        int index = HitTestIndex(dwriteFactory, format, bounds, clientX);
        _edit.MoveCaretTo(index, shiftDown);
        _dragging = true;
        host.CaptureMouse();
    }

    public bool HandleMouseMoveDrag(IDWriteFactory dwriteFactory, IDWriteTextFormat format, Rect bounds, int clientX)
    {
        if (!_dragging || !IsFocused)
            return false;
        int index = HitTestIndex(dwriteFactory, format, bounds, clientX);
        if (index == _edit.Caret)
            return false;
        _edit.MoveCaretTo(index, extendSelection: true);
        CaretVisibleThisFrame = true;
        return true;
    }

    public bool HandleLButtonUp()
    {
        bool wasDragging = _dragging;
        _dragging = false;
        return wasDragging;
    }

    public void HandleKeyDown(int vk, bool ctrlDown, bool shiftDown)
    {
        switch (vk)
        {
            case Win32.VK_LEFT:
                _edit.MoveLeft(shiftDown);
                break;
            case Win32.VK_RIGHT:
                _edit.MoveRight(shiftDown);
                break;
            case Win32.VK_HOME:
                _edit.MoveHome(shiftDown);
                break;
            case Win32.VK_END:
                _edit.MoveEnd(shiftDown);
                break;
            case Win32.VK_BACK:
                _edit.Backspace();
                break;
            case Win32.VK_DELETE:
                _edit.Delete();
                break;
            case Win32.VK_A when ctrlDown:
                _edit.SelectAll();
                break;
            case Win32.VK_C when ctrlDown:
                CopySelection();
                break;
            case Win32.VK_X when ctrlDown:
                CutSelection();
                break;
            case Win32.VK_V when ctrlDown:
                PasteFromClipboard();
                break;
            default:
                return;
        }
        CaretVisibleThisFrame = true;
    }

    public void HandleChar(char c)
    {
        _edit.InsertText(c.ToString());
        CaretVisibleThisFrame = true;
    }

    private void CopySelection()
    {
        if (IsPassword)
            return;
        string sel = _edit.GetSelectedText();
        if (sel.Length > 0)
            OverlayWindowBase.SetClipboardText(sel);
    }

    private void CutSelection()
    {
        if (IsPassword)
            return;
        string sel = _edit.GetSelectedText();
        if (sel.Length > 0)
        {
            OverlayWindowBase.SetClipboardText(sel);
            _edit.DeleteSelection();
        }
    }

    private void PasteFromClipboard()
    {
        string text = OverlayWindowBase.GetClipboardText();
        if (text.Length == 0)
            return;

        text = text.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        _edit.InsertText(text);
    }

    private int HitTestIndex(IDWriteFactory dwriteFactory, IDWriteTextFormat format, Rect bounds, int clientX)
    {
        string display = DisplayText;
        if (display.Length == 0)
            return 0;

        using var layout = dwriteFactory.CreateTextLayout(display, format, bounds.Width, bounds.Height);
        var metrics = layout.HitTestPoint(clientX - bounds.X, 0f, out SharpGen.Runtime.RawBool isTrailingHit, out _);
        int index = (int)metrics.TextPosition;
        return isTrailingHit ? index + 1 : index;
    }
}
