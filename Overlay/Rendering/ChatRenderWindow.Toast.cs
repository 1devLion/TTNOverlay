using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using TTNOverlay.Services;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: short-lived notices ("Message copied", "Banned X", "Couldn't ban X"...).
/// A toast slides up from the bottom edge, stays for <see cref="ToastHoldSeconds"/> seconds and then fades away.
/// Neutral black/white box following the theme; the glyph (X / check / i) tells the kind. Up to <see cref="MaxToasts"/> stack on top of each other.
///
/// Cost: nothing runs while no toast is alive. While one is, OnRender re-requests a frame (that is what keeps the
/// render loop ticking, see OverlayWindowBase.RenderIfDirty) and stops as soon as the last toast has faded out.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private enum ToastKind
    {
        Success,
        Error,
        Info,
    }

    private sealed class Toast
    {
        public string Text = "";
        public ToastKind Kind;
        public long StartTimestamp;

        /// <summary>Current top of the toast (animated). NaN until its first frame, when it starts below its slot.</summary>
        public float CurrentTop = float.NaN;
    }

    private const int MaxToasts = 4;
    private const float ToastHoldSeconds = 3f;
    private const float ToastFadeInSeconds = 0.20f;
    private const float ToastFadeOutSeconds = 0.40f;
    private const float ToastEnterOffset = 40f;
    private const float ToastExitLift = 12f;
    private const float ToastPaddingX = 12f;
    private const float ToastPaddingY = 9f;
    private const float ToastIconSize = 18f;
    private const float ToastIconGap = 10f;
    private const float ToastGap = 8f;
    private const float ToastBottomMargin = 14f;
    private const float ToastMaxWidth = 460f;
    private const int ToastMaxTextLength = 220;

    // Only touched on the UI thread (ShowToast hops there first).
    private readonly List<Toast> _toasts = new();
    private long _toastLastFrameTimestamp;
    private ID2D1SolidColorBrush? _toastBrush;
    private IDWriteTextFormat? _toastFormat;

    /// <summary>Shows a notice. Safe to call from any thread.</summary>
    private void ShowToast(string text, ToastKind kind)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (text.Length > ToastMaxTextLength)
            text = text.Substring(0, ToastMaxTextLength - 1) + "\u2026";

        PostToUiThread(() =>
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();

            // The same notice twice in a row (copying twice, say) just restarts the timer of the one on screen.
            if (_toasts.Count > 0)
            {
                var newest = _toasts[^1];
                if (newest.Kind == kind && string.Equals(newest.Text, text, StringComparison.Ordinal))
                {
                    newest.StartTimestamp = now;
                    RequestRender();
                    return;
                }
            }

            _toasts.Add(new Toast { Text = text, Kind = kind, StartTimestamp = now });
            while (_toasts.Count > MaxToasts)
                _toasts.RemoveAt(0);

            RequestRender();
        });
    }

    private void ShowSuccessToast(string text) => ShowToast(text, ToastKind.Success);

    private void ShowErrorToast(string text) => ShowToast(text, ToastKind.Error);

    /// <summary>
    /// What the panel's status line shows when no action is running: the state of the Twitch session. The outcome of
    /// an action goes to a toast, so the status line goes back to this instead of keeping the last result around.
    /// </summary>
    private string IdleModerationStatus()
    {
        if (_moderation is null)
            return "";
        if (!_moderation.HasCredentials)
            return LocalizationService.T("Moderation_TwitchDisabled");
        if (!_moderation.IsLoggedIn)
            return LocalizationService.T("Moderation_LoginPrompt");
        return string.Format(LocalizationService.T("Moderation_SessionLabel"), _moderation.ModeratorLogin);
    }

    /// <summary>Reports the outcome of a moderation action: clears the "working..." line and shows the result as a toast.</summary>
    private void ReportModerationResult(string text, bool ok)
    {
        _moderationStatusText = IdleModerationStatus();
        ShowToast(text, ok ? ToastKind.Success : ToastKind.Error);
    }

    /// <summary>Brush and font shared by the toasts and the link tooltip (same look: small rounded notices).</summary>
    private void EnsureToastResources(ID2D1DCRenderTarget target)
    {
        _toastBrush ??= target.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 1f));
        _toastFormat ??= DWriteFactory.CreateTextFormat(
            "Segoe UI",
            FontWeight.Normal,
            Vortice.DirectWrite.FontStyle.Normal,
            13f
        );
    }

    private string? _lastRenderFailure;

    /// <summary>Logs a drawing failure and shows it once as a toast (diagnostic text, not localized), so it is visible without the log.</summary>
    private void ReportRenderFailure(string where, Exception ex)
    {
        DebugLog.WriteException("ChatRenderWindow.OnRender: " + where, ex);

        string text = $"Render error ({where}): {ex.GetType().Name}: {ex.Message}";
        if (string.Equals(text, _lastRenderFailure, StringComparison.Ordinal))
            return;
        _lastRenderFailure = text;
        ShowErrorToast(text);
    }

    private void DisposeToastResources()
    {
        _toastBrush?.Dispose();
        _toastBrush = null;
        _toastFormat?.Dispose();
        _toastFormat = null;
    }

    private static float EaseOutCubic(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        float inv = 1f - t;
        return 1f - inv * inv * inv;
    }

    private void DrawToasts(ID2D1DCRenderTarget target, float width, float height)
    {
        if (_toasts.Count == 0)
            return;

        long now = System.Diagnostics.Stopwatch.GetTimestamp();
        double freq = System.Diagnostics.Stopwatch.Frequency;

        float dt = _toastLastFrameTimestamp == 0 ? 0.016f : (float)((now - _toastLastFrameTimestamp) / freq);
        _toastLastFrameTimestamp = now;
        dt = Math.Clamp(dt, 0f, 0.1f);
        float follow = 1f - MathF.Exp(-16f * dt);

        // Drop the ones that already faded out.
        float lifetime = ToastHoldSeconds + ToastFadeOutSeconds;
        _toasts.RemoveAll(t => (now - t.StartTimestamp) / freq >= lifetime);
        if (_toasts.Count == 0)
        {
            _toastLastFrameTimestamp = 0;
            return;
        }

        EnsureToastResources(target);

        bool dark = ThemeService.IsDark;
        float maxToastWidth = Math.Min(ToastMaxWidth, width - Padding * 2f);
        if (maxToastWidth < 120f)
            return;

        float textMaxWidth = maxToastWidth - ToastPaddingX * 2f - ToastIconSize - ToastIconGap;

        // The message box of the Messages tab sits at the bottom of the panel: stay above it.
        float bottom = height - ToastBottomMargin;
        // The message box of the main chat sits at the bottom: stay above it.
        bottom -= _chatInputStripShownHeight;

        // Newest at the bottom, older ones pushed up.
        for (int i = _toasts.Count - 1; i >= 0; i--)
        {
            var toast = _toasts[i];
            float age = (float)((now - toast.StartTimestamp) / freq);

            using var layout = DWriteFactory.CreateTextLayout(toast.Text, _toastFormat, textMaxWidth, 400f);
            float textWidth = Math.Min((float)layout.Metrics.Width, textMaxWidth);
            float textHeight = (float)layout.Metrics.Height;

            float boxWidth = ToastPaddingX * 2f + ToastIconSize + ToastIconGap + textWidth + 2f;
            boxWidth = Math.Min(Math.Max(boxWidth, 140f), maxToastWidth);
            float boxHeight = Math.Max(textHeight, ToastIconSize) + ToastPaddingY * 2f;

            float targetTop = bottom - boxHeight;
            bottom = targetTop - ToastGap;

            if (float.IsNaN(toast.CurrentTop))
                toast.CurrentTop = targetTop + ToastEnterOffset; // enters from below
            else
                toast.CurrentTop += (targetTop - toast.CurrentTop) * follow;

            float alpha = EaseOutCubic(age / ToastFadeInSeconds);
            float lift = 0f;
            if (age > ToastHoldSeconds)
            {
                float outT = Math.Clamp((age - ToastHoldSeconds) / ToastFadeOutSeconds, 0f, 1f);
                alpha *= 1f - outT;
                lift = EaseOutCubic(outT) * ToastExitLift; // keeps drifting up while it disappears
            }
            if (alpha <= 0.01f)
                continue;

            float left = (width - boxWidth) / 2f;
            float top = toast.CurrentTop - lift;
            DrawOneToast(target, toast, layout, left, top, boxWidth, boxHeight, alpha, dark);
        }

        // Keeps the render loop ticking for the animation; once the list is empty nothing asks for another frame.
        RequestRender();
    }

    private void DrawOneToast(
        ID2D1DCRenderTarget target,
        Toast toast,
        IDWriteTextLayout layout,
        float left,
        float top,
        float boxWidth,
        float boxHeight,
        float alpha,
        bool dark
    )
    {
        var brush = _toastBrush!;

        // Plain system look, same as the link tooltip: black or white box following the theme, no colored accents.
        // The kind is told apart by the glyph only (check / X / i).
        Color4 background = dark ? new Color4(0.10f, 0.10f, 0.11f, 0.97f) : new Color4(1f, 1f, 1f, 0.98f);
        Color4 border = dark ? new Color4(1f, 1f, 1f, 0.28f) : new Color4(0f, 0f, 0f, 0.28f);
        Color4 text = dark ? new Color4(0.95f, 0.95f, 0.95f, 1f) : new Color4(0.10f, 0.10f, 0.10f, 1f);

        var rounded = new RoundedRectangle
        {
            Rect = new Rect(left, top, boxWidth, boxHeight),
            RadiusX = 0f,
            RadiusY = 0f,
        };

        brush.Color = new Color4(background.R, background.G, background.B, background.A * alpha);
        target.FillRoundedRectangle(rounded, brush);
        brush.Color = new Color4(border.R, border.G, border.B, border.A * alpha);
        target.DrawRoundedRectangle(rounded, brush, 1f);

        // Icon: a thin ring with a check / X / "i", all in the text color.
        float cx = left + ToastPaddingX + ToastIconSize / 2f;
        float cy = top + boxHeight / 2f;
        brush.Color = new Color4(text.R, text.G, text.B, alpha);
        target.DrawEllipse(new Ellipse(new Vector2(cx, cy), ToastIconSize / 2f - 0.7f, ToastIconSize / 2f - 0.7f), brush, 1.3f);

        const float stroke = 1.6f;
        switch (toast.Kind)
        {
            case ToastKind.Success:
                target.DrawLine(new Vector2(cx - 4.2f, cy + 0.4f), new Vector2(cx - 1.2f, cy + 3.4f), brush, stroke);
                target.DrawLine(new Vector2(cx - 1.2f, cy + 3.4f), new Vector2(cx + 4.4f, cy - 3.2f), brush, stroke);
                break;
            case ToastKind.Error:
                target.DrawLine(new Vector2(cx - 3.4f, cy - 3.4f), new Vector2(cx + 3.4f, cy + 3.4f), brush, stroke);
                target.DrawLine(new Vector2(cx + 3.4f, cy - 3.4f), new Vector2(cx - 3.4f, cy + 3.4f), brush, stroke);
                break;
            default:
                target.FillEllipse(new Ellipse(new Vector2(cx, cy - 3.6f), 1.1f, 1.1f), brush);
                target.DrawLine(new Vector2(cx, cy - 1.2f), new Vector2(cx, cy + 4.2f), brush, stroke);
                break;
        }

        brush.Color = new Color4(text.R, text.G, text.B, alpha);
        float textLeft = left + ToastPaddingX + ToastIconSize + ToastIconGap;
        float textTop = top + (boxHeight - (float)layout.Metrics.Height) / 2f;
        target.DrawTextLayout(new Vector2(textLeft, textTop), layout, brush);
    }
}
