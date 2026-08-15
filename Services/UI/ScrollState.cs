namespace TTNOverlay.Overlay;

/// <summary>
/// Offset/overflow pair used by every scrollable list in the app (messages, events,
/// moderation, alerts, release notes).
/// </summary>
internal struct ScrollState
{
    public float Offset;
    public float Overflow;

    /// <summary>Recalculate the overflow based on the current content/viewport and reclaim the offset.</summary>
    public void RecomputeOverflow(float contentHeight, float viewportHeight)
    {
        Overflow = Math.Max(0f, contentHeight - viewportHeight);
        Offset = Math.Clamp(Offset, 0f, Overflow);
    }

    /// <summary>Apply a mouse wheel delta already converted to pixels, clamped to the current overflow.</summary>
    public void ApplyWheel(float deltaPx, bool invert = false)
    {
        Offset = Math.Clamp(Offset + (invert ? -deltaPx : deltaPx), 0f, Overflow);
    }

    /// <summary>Add the added height to the current offset if it is greater than zero.</summary>
    public void OnContentGrew(float addedHeight)
    {
        if (Offset > 0f)
            Offset += addedHeight;
    }
}