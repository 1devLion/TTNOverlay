using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using TTNOverlay.Services;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: the timeout-duration dropdown used by the moderation panel.
/// </summary>
internal sealed partial class ChatRenderWindow
{

    private sealed class ModerationDropdownItem
    {
        public string Label = "";
        public Action OnSelect = () => { };
    }

    private bool _dropdownOpen;
    private Rect _dropdownBounds;
    private int _dropdownHoveredIndex = -1;
    private readonly List<ModerationDropdownItem> _dropdownItems = new();
    private readonly List<Rect> _dropdownItemRects = new();
    private ID2D1SolidColorBrush? _dropdownBackgroundBrush;
    private ID2D1SolidColorBrush? _dropdownBorderBrush;
    private ID2D1SolidColorBrush? _dropdownHoverBrush;
    private IDWriteTextFormat? _dropdownItemFormat;

    private const float DropdownItemHeight = 30f;
    private const float DropdownPaddingX = 12f;

    private const float DropdownWidth = 200f;

    private const int MaxTimeoutSeconds = 1_209_600;

    private static readonly (int Seconds, string LabelKey)[] ModerationMuteDurations =
    {
        (60, "MainWindow_1Minute"),
        (300, "MainWindow_5Minutes"),
        (600, "MainWindow_10Minutes"),
        (1800, "MainWindow_30Minutes"),
        (3600, "MainWindow_1Hour"),
        (86400, "MainWindow_24Hours"),
        (259200, "MainWindow_3Days"),
        (604800, "MainWindow_7Days"),
        (MaxTimeoutSeconds, "MainWindow_14Days"),
    };

    private void OpenModerationDropdown(float anchorX, float anchorY, List<ModerationDropdownItem> items)
    {
        if (items.Count == 0)
            return;

        _dropdownItemFormat ??= CreateTitleBarFormat("Segoe UI", Vortice.DirectWrite.FontWeight.Normal, 14f, Vortice.DirectWrite.TextAlignment.Leading);
        _dropdownItemFormat.ParagraphAlignment = ParagraphAlignment.Center;

        _dropdownItems.Clear();
        _dropdownItems.AddRange(items);
        _dropdownHoveredIndex = -1;

        float width = DropdownWidth;
        float height = items.Count * DropdownItemHeight;

        Win32.GetClientRect(Hwnd, out var client);
        float clientWidth = client.Right - client.Left;
        float clientHeight = client.Bottom - client.Top;

        float x = Math.Min(anchorX, Math.Max(0f, clientWidth - width));
        float y = Math.Min(anchorY, Math.Max(0f, clientHeight - height));

        _dropdownBounds = new Rect(x, y, width, height);
        _dropdownItemRects.Clear();
        for (int i = 0; i < items.Count; i++)
            _dropdownItemRects.Add(new Rect(x, y + i * DropdownItemHeight, width, DropdownItemHeight));

        _dropdownOpen = true;
        RequestRender();
    }

    private void CloseModerationDropdown()
    {
        if (!_dropdownOpen)
            return;

        _dropdownOpen = false;
        _dropdownOwnerChatSettingField = null;
        _dropdownItems.Clear();
        _dropdownItemRects.Clear();
        _dropdownHoveredIndex = -1;
        RequestRender();
    }

    private bool HandleModerationDropdownClick(int clientX, int clientY)
    {
        if (!_dropdownOpen)
            return false;

        for (int i = 0; i < _dropdownItemRects.Count; i++)
        {
            if (Contains(_dropdownItemRects[i], clientX, clientY))
            {
                var item = _dropdownItems[i];
                CloseModerationDropdown();
                item.OnSelect();
                return true;
            }
        }

        CloseModerationDropdown();
        return true;
    }

    private bool HandleModerationDropdownMouseMove(int clientX, int clientY)
    {
        if (!_dropdownOpen)
            return false;

        int newHovered = -1;
        for (int i = 0; i < _dropdownItemRects.Count; i++)
        {
            if (Contains(_dropdownItemRects[i], clientX, clientY))
            {
                newHovered = i;
                break;
            }
        }

        if (newHovered != _dropdownHoveredIndex)
        {
            _dropdownHoveredIndex = newHovered;
            RequestRender();
        }
        return true;
    }

    private void HandleModerationRowMouseMove(int clientX, int clientY)
    {
        if (!_showingModeration)
            return;

        bool newLoginHovered = _moderationLoginActionRect is { } loginRect && Contains(loginRect, clientX, clientY);
        if (newLoginHovered != _moderationLoginButtonHovered)
        {
            _moderationLoginButtonHovered = newLoginHovered;
            RequestRender();
        }

        ModerationChatSettingField? newHovered = null;
        foreach (var (bounds, field) in _moderationChatSettingButtonRects)
        {
            if (Contains(bounds, clientX, clientY))
            {
                newHovered = field;
                break;
            }
        }

        if (newHovered == _hoveredChatSettingButton)
            return;

        _hoveredChatSettingButton = newHovered;
        EnsureHoverAnimationTimerRunning();
    }

    private void DrawModerationDropdown(ID2D1DCRenderTarget target)
    {
        if (!_dropdownOpen || _dropdownItems.Count == 0)
            return;

        _dropdownBackgroundBrush ??= target.CreateSolidColorBrush(ThemeService.DropdownSurfaceBackground);
        _dropdownBorderBrush ??= target.CreateSolidColorBrush(ThemeService.FieldBorder);
        _dropdownHoverBrush ??= target.CreateSolidColorBrush(ThemeService.SubtleHoverFill);

        target.FillRectangle(_dropdownBounds, _dropdownBackgroundBrush);

        for (int i = 0; i < _dropdownItemRects.Count; i++)
        {
            var rect = _dropdownItemRects[i];
            if (i == _dropdownHoveredIndex)
                target.FillRectangle(rect, _dropdownHoverBrush);

            using var layout = DWriteFactory.CreateTextLayout(
                _dropdownItems[i].Label,
                _dropdownItemFormat!,
                Math.Max(1f, rect.Width - DropdownPaddingX * 2),
                rect.Height
            );
            target.DrawTextLayout(new Vector2(rect.Left + DropdownPaddingX, rect.Top), layout, _moderationTextBrush!);

            if (i > 0)
                target.DrawLine(new Vector2(rect.Left, rect.Top), new Vector2(rect.Right, rect.Top), _dropdownBorderBrush);
        }

        target.DrawRectangle(_dropdownBounds, _dropdownBorderBrush, 1f);
    }
}