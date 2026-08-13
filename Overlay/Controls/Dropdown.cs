using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using TTNOverlay.Services;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay.Controls;

/// <summary>
/// Reusable dropdown/combo-box control drawn with Direct2D for use inside the native overlay windows.
/// </summary>
public sealed class Dropdown
{
    public sealed class Item
    {
        public string Label = "";
        public Action OnSelect = () => { };
    }

    public bool IsOpen { get; private set; }

    private Rect _bounds;
    private int _hoveredIndex = -1;
    private readonly List<Item> _items = new();
    private readonly List<Rect> _itemRects = new();
    private ID2D1SolidColorBrush? _backgroundBrush;
    private ID2D1SolidColorBrush? _borderBrush;
    private ID2D1SolidColorBrush? _hoverBrush;
    private IDWriteTextFormat? _itemFormat;

    private bool? _lastKnownIsDark;

    private const float ItemHeight = 30f;
    private const float PaddingX = 12f;

    public float Width { get; set; } = 200f;

    public void Open(float anchorX, float anchorY, float clientWidth, float clientHeight, List<Item> items, IDWriteTextFormat itemFormat)
    {
        if (items.Count == 0)
            return;

        _itemFormat = itemFormat;
        _items.Clear();
        _items.AddRange(items);
        _hoveredIndex = -1;

        float height = items.Count * ItemHeight;
        float x = Math.Min(anchorX, Math.Max(0f, clientWidth - Width));
        float y = Math.Min(anchorY, Math.Max(0f, clientHeight - height));

        _bounds = new Rect(x, y, Width, height);
        _itemRects.Clear();
        for (int i = 0; i < items.Count; i++)
            _itemRects.Add(new Rect(x, y + i * ItemHeight, Width, ItemHeight));

        IsOpen = true;
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        _items.Clear();
        _itemRects.Clear();
        _hoveredIndex = -1;
    }

    public bool HandleClick(int clientX, int clientY)
    {
        if (!IsOpen)
            return false;

        for (int i = 0; i < _itemRects.Count; i++)
        {
            if (Contains(_itemRects[i], clientX, clientY))
            {
                var item = _items[i];
                Close();
                item.OnSelect();
                return true;
            }
        }

        Close();
        return true;
    }

    public bool HandleMouseMove(int clientX, int clientY)
    {
        if (!IsOpen)
            return false;

        int newHovered = -1;
        for (int i = 0; i < _itemRects.Count; i++)
        {
            if (Contains(_itemRects[i], clientX, clientY))
            {
                newHovered = i;
                break;
            }
        }

        if (newHovered == _hoveredIndex)
            return false;
        _hoveredIndex = newHovered;
        return true;
    }

    public void Draw(ID2D1DCRenderTarget target, IDWriteFactory dwriteFactory, ID2D1SolidColorBrush textBrush)
    {
        if (!IsOpen || _items.Count == 0)
            return;

        if (_lastKnownIsDark != ThemeService.IsDark)
        {
            _lastKnownIsDark = ThemeService.IsDark;
            _backgroundBrush?.Dispose(); _backgroundBrush = null;
            _borderBrush?.Dispose(); _borderBrush = null;
            _hoverBrush?.Dispose(); _hoverBrush = null;
        }

        _backgroundBrush ??= target.CreateSolidColorBrush(ThemeService.DropdownSurfaceBackground);
        _borderBrush ??= target.CreateSolidColorBrush(ThemeService.FieldBorder);
        _hoverBrush ??= target.CreateSolidColorBrush(ThemeService.SubtleHoverFill);

        target.FillRectangle(_bounds, _backgroundBrush);

        for (int i = 0; i < _itemRects.Count; i++)
        {
            var rect = _itemRects[i];
            if (i == _hoveredIndex)
                target.FillRectangle(rect, _hoverBrush);

            using var layout = dwriteFactory.CreateTextLayout(
                _items[i].Label,
                _itemFormat!,
                Math.Max(1f, rect.Width - PaddingX * 2),
                rect.Height
            );
            target.DrawTextLayout(new Vector2(rect.Left + PaddingX, rect.Top), layout, textBrush);

            if (i > 0)
                target.DrawLine(new Vector2(rect.Left, rect.Top), new Vector2(rect.Right, rect.Top), _borderBrush);
        }

        target.DrawRectangle(_bounds, _borderBrush, 1f);
    }

    private static bool Contains(Rect rect, float x, float y) =>
        x >= rect.Left && x <= rect.Right && y >= rect.Top && y <= rect.Bottom;

    public void Dispose()
    {
        _backgroundBrush?.Dispose(); _backgroundBrush = null;
        _borderBrush?.Dispose(); _borderBrush = null;
        _hoverBrush?.Dispose(); _hoverBrush = null;
    }
}