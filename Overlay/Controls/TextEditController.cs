namespace TTNOverlay.Overlay.Controls;

/// <summary>
/// Caret, selection, and text-editing logic shared by native text input controls (independent of rendering).
/// </summary>
public sealed class TextEditController
{
    public string Text { get; private set; } = "";
    public int Caret { get; private set; }
    public int? SelectionAnchor { get; private set; }

    public int? MaxLength { get; set; }

    public bool HasSelection => SelectionAnchor.HasValue && SelectionAnchor.Value != Caret;

    public int SelectionStart => HasSelection ? Math.Min(SelectionAnchor!.Value, Caret) : Caret;
    public int SelectionEnd => HasSelection ? Math.Max(SelectionAnchor!.Value, Caret) : Caret;

    public void SetText(string text)
    {
        Text = text ?? "";
        Caret = Math.Clamp(Caret, 0, Text.Length);
        SelectionAnchor = null;
    }

    public void MoveCaretTo(int index, bool extendSelection)
    {
        index = Math.Clamp(index, 0, Text.Length);
        if (extendSelection)
            SelectionAnchor ??= Caret;
        else
            SelectionAnchor = null;
        Caret = index;
    }

    public void SelectAll()
    {
        SelectionAnchor = 0;
        Caret = Text.Length;
    }

    public void ClearSelection() => SelectionAnchor = null;

    public string GetSelectedText() => HasSelection ? Text.Substring(SelectionStart, SelectionEnd - SelectionStart) : "";

    public bool DeleteSelection()
    {
        if (!HasSelection)
            return false;
        int start = SelectionStart;
        int len = SelectionEnd - start;
        Text = Text.Remove(start, len);
        Caret = start;
        SelectionAnchor = null;
        return true;
    }

    public void InsertText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        DeleteSelection();

        if (MaxLength.HasValue)
        {
            int room = MaxLength.Value - Text.Length;
            if (room <= 0)
                return;
            if (text.Length > room)
                text = text.Substring(0, room);
        }

        Text = Text.Insert(Caret, text);
        Caret += text.Length;
    }

    public void Backspace()
    {
        if (DeleteSelection())
            return;
        if (Caret == 0)
            return;
        Text = Text.Remove(Caret - 1, 1);
        Caret--;
    }

    public void Delete()
    {
        if (DeleteSelection())
            return;
        if (Caret >= Text.Length)
            return;
        Text = Text.Remove(Caret, 1);
    }

    public void MoveLeft(bool extendSelection)
    {
        if (!extendSelection && HasSelection)
        {

            MoveCaretTo(SelectionStart, extendSelection: false);
            return;
        }
        MoveCaretTo(Math.Max(0, Caret - 1), extendSelection);
    }

    public void MoveRight(bool extendSelection)
    {
        if (!extendSelection && HasSelection)
        {
            MoveCaretTo(SelectionEnd, extendSelection: false);
            return;
        }
        MoveCaretTo(Math.Min(Text.Length, Caret + 1), extendSelection);
    }

    public void MoveHome(bool extendSelection) => MoveCaretTo(0, extendSelection);

    public void MoveEnd(bool extendSelection) => MoveCaretTo(Text.Length, extendSelection);
}
