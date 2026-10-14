using Vortice.Mathematics;
using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay;

/// <summary>
/// ChatRenderWindow partial: hover highlight for the boxes of the moderation panel and the message box of the chat
/// (tabs, pills, rows of the lists, the text field). Same idea as the Chat settings buttons: a fill that fades in
/// when the mouse is over the box and out when it leaves.
///
/// While drawing, every hoverable box registers its rectangle with a key (RegisterHover) and gets back its current
/// progress (0..1) to tint itself with. The mouse position picks the key under it; the animation timer shared with the
/// title bar buttons (EnsureHoverAnimationTimerRunning) moves the progress of each key towards 1 or 0.
/// </summary>
internal sealed partial class ChatRenderWindow
{
    private readonly List<(Rect Bounds, object Key)> _hoverTargets = new();
    private readonly Dictionary<object, float> _hoverProgress = new();
    private readonly List<object> _hoverScratch = new();
    private object? _hoverKey;

    /// <summary>Set while drawing a box that is disabled, so it does not react to the mouse.</summary>
    private bool _suppressPillHover;

    /// <summary>
    /// Registers a box for this frame and returns its hover progress. The part outside [clipTop, clipBottom] (a row
    /// scrolled under the fixed controls) does not react.
    /// </summary>
    private float RegisterHover(Rect bounds, object key, float clipTop = float.MinValue, float clipBottom = float.MaxValue)
    {
        float top = Math.Max(bounds.Top, clipTop);
        float bottom = Math.Min(bounds.Bottom, clipBottom);
        if (bottom > top && bounds.Width > 0f)
            _hoverTargets.Add((new Rect(bounds.Left, top, bounds.Width, bottom - top), key));

        return _hoverProgress.TryGetValue(key, out float progress) ? progress : 0f;
    }

    private void UpdateHoverFromMouse(int clientX, int clientY)
    {
        object? key = null;
        if (!_dropdownOpen && clientX >= 0 && clientY >= 0)
        {
            // Later ones were drawn on top.
            for (int i = _hoverTargets.Count - 1; i >= 0; i--)
            {
                if (Contains(_hoverTargets[i].Bounds, clientX, clientY))
                {
                    key = _hoverTargets[i].Key;
                    break;
                }
            }
        }

        if (Equals(key, _hoverKey))
            return;

        _hoverKey = key;
        if (key is not null && !_hoverProgress.ContainsKey(key))
            _hoverProgress[key] = 0f;
        EnsureHoverAnimationTimerRunning();
    }

    private void ClearHover()
    {
        if (_hoverKey is null)
            return;

        _hoverKey = null;
        EnsureHoverAnimationTimerRunning();
    }

    /// <summary>One step of the animation for every box that has some progress. True while something is still moving.</summary>
    private bool StepGenericHover(float t)
    {
        if (_hoverProgress.Count == 0)
            return false;

        bool moving = false;
        _hoverScratch.Clear();
        _hoverScratch.AddRange(_hoverProgress.Keys);
        foreach (var key in _hoverScratch)
        {
            float progress = _hoverProgress[key];
            bool hovered = Equals(key, _hoverKey);
            moving |= StepTowards(ref progress, hovered ? 1f : 0f, t);

            if (!hovered && progress <= 0f)
                _hoverProgress.Remove(key);
            else
                _hoverProgress[key] = progress;
        }
        return moving;
    }
}
