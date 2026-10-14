using System.Numerics;
using TTNOverlay.Models;
using TTNOverlay.Native;
using TTNOverlay.Services;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: links in chat text. They are drawn blue and underlined, a tooltip says
/// "Ctrl + Click to open the link" while the mouse is over one, and Ctrl + Click opens it in the browser.
///
/// How it works: messages (and Messages-tab rows) that contain a link are drawn word by word, like the ones that
/// contain emotes (see DrawBodyWithEmotes / FlowModerationRow), so a link word can get its own color. While drawing,
/// every link word records its rectangle in <see cref="_linkHits"/>; the list is rebuilt each frame, so it always
/// matches what is on screen, including after scrolling. Messages without links keep using the cached text layouts.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private readonly List<(Rect Bounds, string Url)> _linkHits = new();
    private float _linkClipTop;
    private float _linkClipBottom = float.MaxValue;

    // Names in the main chat: clicking one opens that user's messages in the moderation panel.
    private readonly List<(Rect Bounds, string Login)> _userHits = new();
    private string? _hoveredUserLogin;

    private string? _hoveredLinkUrl;
    private int _linkMouseX = -1;
    private int _linkMouseY = -1;

    private ID2D1SolidColorBrush? _linkBrush;

    // The chat overlay always draws white text with an outline over the game, the panel follows the theme.
    private static Color4 ChatLinkColor => new(0.36f, 0.67f, 1f, 1f);

    private static Color4 PanelLinkColor =>
        ThemeService.IsDark ? new Color4(0.42f, 0.71f, 1f, 1f) : new Color4(0.05f, 0.38f, 0.82f, 1f);

    private ID2D1SolidColorBrush GetLinkBrush(ID2D1DCRenderTarget target, Color4 color)
    {
        _linkBrush ??= target.CreateSolidColorBrush(color);
        _linkBrush.Color = color;
        return _linkBrush;
    }

    private void DisposeLinkResources()
    {
        _linkBrush?.Dispose();
        _linkBrush = null;
        _splitPiecesCache.Clear();
    }

    // ------------------------------------------------------------------ hit areas (rebuilt every frame)

    private void BeginLinkFrame()
    {
        _linkHits.Clear();
        _userHits.Clear();
        _linkClipTop = 0f;
        _linkClipBottom = float.MaxValue;
    }

    /// <summary>Only the part of a link inside the scrolling list can be clicked, not what is scrolled under the fixed controls.</summary>
    private void SetLinkClip(float top, float bottom)
    {
        _linkClipTop = top;
        _linkClipBottom = bottom;
    }

    private void AddLinkHit(float x, float y, float width, float height, string url)
    {
        float top = Math.Max(y, _linkClipTop);
        float bottom = Math.Min(y + height, _linkClipBottom);
        if (bottom <= top || width <= 0f)
            return;
        _linkHits.Add((new Rect(x, top, width, bottom - top), url));
    }

    private void AddUserHit(float x, float y, float width, float height, string login)
    {
        float top = Math.Max(y, _linkClipTop);
        float bottom = Math.Min(y + height, _linkClipBottom);
        if (bottom <= top || width <= 0f)
            return;
        _userHits.Add((new Rect(x, top, width, bottom - top), login));
    }

    private string? FindUserAt(int clientX, int clientY)
    {
        if (clientX < 0 || clientY < 0)
            return null;

        for (int i = _userHits.Count - 1; i >= 0; i--)
        {
            if (Contains(_userHits[i].Bounds, clientX, clientY))
                return _userHits[i].Login;
        }
        return null;
    }

    private bool TryOpenUserHistoryAt(int clientX, int clientY)
    {
        if (_dropdownOpen || !UserHistoryClickAvailable)
            return false;

        string? login = FindUserAt(clientX, clientY);
        if (login is null)
            return false;

        OpenModerationForUser(login);
        return true;
    }

    private string? FindLinkAt(int clientX, int clientY)
    {
        if (clientX < 0 || clientY < 0)
            return null;

        for (int i = _linkHits.Count - 1; i >= 0; i--)
        {
            if (Contains(_linkHits[i].Bounds, clientX, clientY))
                return _linkHits[i].Url;
        }
        return null;
    }

    // ------------------------------------------------------------------ mouse

    private void HandleLinkMouseMove(int clientX, int clientY)
    {
        _linkMouseX = clientX;
        _linkMouseY = clientY;

        string? url = _dropdownOpen ? null : FindLinkAt(clientX, clientY);
        bool changed = !string.Equals(url, _hoveredLinkUrl, StringComparison.Ordinal);
        _hoveredLinkUrl = url;

        string? user = _dropdownOpen || url is not null || !UserHistoryClickAvailable ? null : FindUserAt(clientX, clientY);
        bool userChanged = !string.Equals(user, _hoveredUserLogin, StringComparison.Ordinal);
        _hoveredUserLogin = user;

        // While over a link or a name the tooltip follows the mouse, so every move needs a frame.
        if (changed || userChanged || url is not null || user is not null)
            RequestRender();
    }

    private void ClearLinkHover()
    {
        _linkMouseX = -1;
        _linkMouseY = -1;
        if (_hoveredLinkUrl is null && _hoveredUserLogin is null)
            return;
        _hoveredLinkUrl = null;
        _hoveredUserLogin = null;
        RequestRender();
    }

    /// <summary>Ctrl + Click on a link opens it. Returns true when the click was consumed by a link.</summary>
    private bool TryOpenLinkAt(int clientX, int clientY)
    {
        if ((Win32.GetKeyState(Win32.VK_CONTROL) & 0x8000) == 0)
            return false;

        string? url = FindLinkAt(clientX, clientY);
        if (url is null)
            return false;

        ChatLinks.Open(url);
        return true;
    }

    protected override bool OnSetCursor()
    {
        if ((_hoveredLinkUrl is null && _hoveredUserLogin is null) || _dropdownOpen)
            return false;

        Win32.SetCursor(Win32.LoadCursor(IntPtr.Zero, Win32.IDC_HAND));
        return true;
    }

    // ------------------------------------------------------------------ tooltip

    private void DrawLinkTooltip(ID2D1DCRenderTarget target, float width, float height)
    {
        // Looked up again here, with this frame's rectangles: the list may have scrolled under a still mouse.
        string? url = _dropdownOpen ? null : FindLinkAt(_linkMouseX, _linkMouseY);
        _hoveredLinkUrl = url;
        _hoveredUserLogin = _dropdownOpen || url is not null || !UserHistoryClickAvailable ? null : FindUserAt(_linkMouseX, _linkMouseY);
        if (url is null && _hoveredUserLogin is null)
            return;

        float mouseX = _linkMouseX;
        float mouseY = _linkMouseY;
        DrawTooltip(
            target,
            LocalizationService.T(url is not null ? "Link_OpenHint" : "Chat_UserHistoryHint"),
            width,
            height,
            (boxWidth, boxHeight) =>
            {
                float top = mouseY + 20f;
                if (top + boxHeight > height - 4f)
                    top = mouseY - boxHeight - 8f; // no room below the pointer: above it
                return (mouseX + 14f, top);
            }
        );
    }

    /// <summary>
    /// A small box with a line of text, black or white following the theme. Shared by the link hint and the title bar
    /// buttons. <paramref name="place"/> gets the size of the box and returns where its top-left corner goes; the box is
    /// then kept inside the window horizontally.
    /// </summary>
    private void DrawTooltip(
        ID2D1DCRenderTarget target,
        string text,
        float width,
        float height,
        Func<float, float, (float Left, float Top)> place
    )
    {
        EnsureToastResources(target);
        var brush = _toastBrush!;
        bool dark = ThemeService.IsDark;

        using var layout = DWriteFactory.CreateTextLayout(text, _toastFormat!, Math.Max(80f, width - 16f), 100f);
        float boxWidth = (float)layout.Metrics.Width + 18f;
        float boxHeight = (float)layout.Metrics.Height + 10f;

        var (left, top) = place(boxWidth, boxHeight);
        if (left + boxWidth > width - 4f)
            left = width - boxWidth - 4f;
        if (left < 4f)
            left = 4f;
        if (top < 4f)
            top = 4f;

        var box = new Rect(left, top, boxWidth, boxHeight);
        brush.Color = dark ? new Color4(0.10f, 0.10f, 0.11f, 0.97f) : new Color4(1f, 1f, 1f, 0.98f);
        target.FillRectangle(box, brush);
        brush.Color = dark ? new Color4(1f, 1f, 1f, 0.28f) : new Color4(0f, 0f, 0f, 0.28f);
        target.DrawRectangle(box, brush, 1f);
        brush.Color = dark ? new Color4(0.95f, 0.95f, 0.95f, 1f) : new Color4(0.10f, 0.10f, 0.10f, 1f);
        target.DrawTextLayout(new Vector2(left + 9f, top + 5f), layout, brush);
    }

    // ------------------------------------------------------------------ words wider than the line

    private readonly Dictionary<(string Word, float Width, IDWriteTextFormat Format), string[]> _splitPiecesCache = new();

    /// <summary>
    /// Breaks a word that is wider than the whole line (a long URL) into pieces that each fit on one line.
    /// The result is cached: this is asked every frame for every visible message that has such a word.
    /// </summary>
    private string[] GetPiecesThatFit(string word, IDWriteTextFormat format, float maxWidth)
    {
        var key = (word, maxWidth, format);
        if (_splitPiecesCache.TryGetValue(key, out var cached))
            return cached;

        var pieces = new List<string>();
        int start = 0;
        while (start < word.Length)
        {
            int lo = 1;
            int hi = word.Length - start;
            int best = 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                using var probe = DWriteFactory.CreateTextLayout(
                    word.Substring(start, mid),
                    format,
                    float.MaxValue,
                    float.MaxValue
                );
                if (probe.Metrics.WidthIncludingTrailingWhitespace <= maxWidth)
                {
                    best = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }
            pieces.Add(word.Substring(start, best));
            start += best;
        }

        if (_splitPiecesCache.Count >= 256)
            _splitPiecesCache.Clear();

        var result = pieces.ToArray();
        _splitPiecesCache[key] = result;
        return result;
    }

    // ------------------------------------------------------------------ Messages-tab rows that contain a link

    /// <summary>
    /// One Messages-tab row laid out word by word: "HH:mm:ss  [logo slot]Name: text [tags]", name in bold, links blue
    /// and underlined. Used only for rows that contain a link (every other row keeps its single text layout, see
    /// CreateModerationRowLayout). With <paramref name="draw"/> false nothing is drawn (target may be null) and
    /// the return value is the height the row takes.
    /// </summary>
    private float FlowModerationRow(
        ID2D1DCRenderTarget? target,
        ModerationLogEntry entry,
        float x,
        float y,
        float width,
        bool draw
    )
    {
        EnsureModerationIconMetrics();
        entry.TimeText ??= entry.ReceivedAtUtc.ToLocalTime().ToString("HH:mm:ss");

        var format = _moderationBodyFormat!;
        float lineHeight = _moderationLineHeight;
        var textBrush = entry.IsDeleted ? _moderationSecondaryBrush! : _moderationTextBrush!;
        float cursorX = x;
        float cursorY = y;

        // The platform's logo (Multichat) is drawn over the blank slot that follows the time.
        string tag = ModerationMode == ModeMultichat ? ModerationIconSlot : "";
        using (var prefix = DWriteFactory.CreateTextLayout($"{entry.TimeText}  {tag}", format, float.MaxValue, 1000f))
        {
            if (draw)
                target!.DrawTextLayout(new Vector2(cursorX, cursorY), prefix, textBrush);
            cursorX += (float)prefix.Metrics.WidthIncludingTrailingWhitespace;
        }

        string label = DisplayNameOf(entry) + ": ";
        using (var name = DWriteFactory.CreateTextLayout(label, format, float.MaxValue, 1000f))
        {
            name.SetFontWeight(
                Vortice.DirectWrite.FontWeight.Bold,
                new Vortice.DirectWrite.TextRange(0, (uint)label.Length)
            );
            float nameWidth = (float)name.Metrics.WidthIncludingTrailingWhitespace;
            if (cursorX + nameWidth > x + width && cursorX > x)
            {
                cursorX = x;
                cursorY += lineHeight;
            }
            if (draw)
                target!.DrawTextLayout(new Vector2(cursorX, cursorY), name, textBrush);
            cursorX += nameWidth;
        }

        string rest = entry.Text;
        if (entry.IsDeleted)
            rest += " " + LocalizationService.T("Moderation_MsgDeletedTag");
        if (entry.UserState == ModerationUserState.TimedOut)
            rest += " " + LocalizationService.T("Moderation_MsgMutedTag");
        else if (entry.UserState == ModerationUserState.Banned)
            rest += " " + LocalizationService.T("Moderation_MsgBannedTag");

        void Place(string word, string? url)
        {
            var layout = GetOrCreateWordLayout(word, format);
            float wordWidth = (float)layout.Metrics.WidthIncludingTrailingWhitespace;

            if (wordWidth > width && width > 20f && word.Length > 1)
            {
                foreach (var piece in GetPiecesThatFit(word, format, width))
                    Place(piece, url);
                return;
            }

            if (cursorX + wordWidth > x + width && cursorX > x)
            {
                cursorX = x;
                cursorY += lineHeight;
            }

            if (draw)
            {
                if (url is not null)
                {
                    var linkBrush = GetLinkBrush(target!, PanelLinkColor);
                    float textWidth = (float)layout.Metrics.Width;
                    float textHeight = (float)layout.Metrics.Height;
                    target!.DrawTextLayout(new Vector2(cursorX, cursorY), layout, linkBrush);
                    float lineY = cursorY + textHeight - 3f;
                    target.DrawLine(new Vector2(cursorX, lineY), new Vector2(cursorX + textWidth, lineY), linkBrush, 1f);
                    AddLinkHit(cursorX, cursorY, textWidth, textHeight, url);
                }
                else
                {
                    target!.DrawTextLayout(new Vector2(cursorX, cursorY), layout, textBrush);
                }
            }
            cursorX += wordWidth;
        }

        foreach (var word in SplitWords(rest))
            Place(word, ChatLinks.TryGetUrl(word, out string linkUrl) ? linkUrl : null);

        return cursorY + lineHeight - y;
    }
}
