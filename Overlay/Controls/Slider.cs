using Rect = Vortice.Mathematics.Rect;

namespace TTNOverlay.Overlay.Controls;

/// <summary>
/// Reusable horizontal slider control drawn with Direct2D for use inside the native overlay windows.
/// </summary>
public sealed class Slider
{
    private bool _dragging;

    public float Minimum { get; set; }
    public float Maximum { get; set; } = 1f;
    public float Value { get; private set; }

    public event Action<float>? ValueChanged;

    public void SetValue(float value)
    {
        float clamped = Math.Clamp(value, Minimum, Maximum);
        if (Math.Abs(clamped - Value) < 0.0001f)
            return;
        Value = clamped;
        ValueChanged?.Invoke(Value);
    }

    public void HandleLButtonDown(Rect bounds, int clientX, OverlayWindowBase host)
    {
        _dragging = true;
        host.CaptureMouse();
        SetValueFromClientX(bounds, clientX);
    }

    public void HandleMouseMove(Rect bounds, int clientX)
    {
        if (!_dragging)
            return;
        SetValueFromClientX(bounds, clientX);
    }

    public bool HandleLButtonUp()
    {
        bool wasDragging = _dragging;
        _dragging = false;
        return wasDragging;
    }

    private void SetValueFromClientX(Rect bounds, int clientX)
    {
        float t = bounds.Width <= 0 ? 0f : Math.Clamp((clientX - bounds.X) / bounds.Width, 0f, 1f);
        SetValue(Minimum + t * (Maximum - Minimum));
    }

    public float NormalizedPosition => Maximum <= Minimum ? 0f : (Value - Minimum) / (Maximum - Minimum);
}
