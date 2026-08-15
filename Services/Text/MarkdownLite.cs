using System.Text;

namespace TTNOverlay.Overlay;

/// <summary>
/// Very small Markdown subset renderer: strips #, ##, -, *, ** from the raw text and
/// returns plain text plus the character ranges that should be bold/header-sized.
/// Not a full Markdown parser — just enough for typical GitHub release notes.
/// </summary>
internal static class MarkdownLite
{
    internal readonly record struct BoldSpan(int Start, int Length, bool IsHeader);

    public static (string PlainText, List<BoldSpan> Spans) Parse(string markdown)
    {
        var sb = new StringBuilder();
        var spans = new List<BoldSpan>();
        var lines = markdown.Replace("\r\n", "\n").Split('\n');

        for (int li = 0; li < lines.Length; li++)
        {
            string line = lines[li];
            bool isHeader = false;

            string trimmedLine = line.TrimEnd();
            bool isRule = trimmedLine.Length >= 3 &&
                (trimmedLine.All(c => c == '-') || trimmedLine.All(c => c == '*') || trimmedLine.All(c => c == '_'));

            if (isRule)
            {
                if (li < lines.Length - 1)
                    sb.Append('\n');
                continue;
            }

            int hashCount = 0;
            while (hashCount < line.Length && line[hashCount] == '#')
                hashCount++;

            if (hashCount is >= 1 and <= 6 && hashCount < line.Length && line[hashCount] == ' ')
            {
                line = line[(hashCount + 1)..];
                isHeader = true;
            }
            else if (line.StartsWith("- ") || line.StartsWith("* "))
            {
                line = "•  " + line[2..];
            }

            int lineStart = sb.Length;
            int i = 0;
            while (i < line.Length)
            {
                if (i + 1 < line.Length && line[i] == '*' && line[i + 1] == '*')
                {
                    int end = line.IndexOf("**", i + 2, StringComparison.Ordinal);
                    if (end >= 0)
                    {
                        string bold = line[(i + 2)..end];
                        int boldStart = sb.Length;
                        sb.Append(bold);
                        spans.Add(new BoldSpan(boldStart, bold.Length, IsHeader: false));
                        i = end + 2;
                        continue;
                    }
                }
                sb.Append(line[i]);
                i++;
            }

            if (isHeader)
                spans.Add(new BoldSpan(lineStart, sb.Length - lineStart, IsHeader: true));

            if (li < lines.Length - 1)
                sb.Append('\n');
        }

        return (sb.ToString(), spans);
    }
}