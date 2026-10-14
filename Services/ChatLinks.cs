using System.Diagnostics;
using System.Text.RegularExpressions;
using TTNOverlay.Models;

namespace TTNOverlay.Services;

/// <summary>
/// Finds links in chat text. A "word" (text between spaces) is a link when it starts with http://, https:// or
/// www., or when it is a bare domain with a common TLD ("twitch.tv/foo"). Only http and https links are ever opened.
///
/// Detection is allocation-free for words that are not links (this runs per word, per frame, for the visible
/// messages that contain emotes or links), and whether a whole message has a link is cached on the message.
/// </summary>
internal static class ChatLinks
{
    // Interpreted on purpose (no RegexOptions.Compiled): it only runs on words that contain a dot, and compiling it
    // would cost startup time and memory for a check that is this rare.
    private static readonly Regex BareDomain = new(
        @"^(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+(?:com|net|org|tv|gg|io|ly|me|co|app|dev|xyz|info|gl|be|to|fm|ar|es|us|uk|de|fr|br|ru|jp)(?::\d{1,5})?(?:[/?#]\S*)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    private static bool IsLeadingJunk(char c) => c is '(' or '<' or '"' or '\'' or '[';

    private static bool IsTrailingJunk(char c) =>
        c is '.' or ',' or ';' or ':' or '!' or '?' or ')' or ']' or '}' or '"' or '\'' or '>';

    /// <summary>True (with a normalized absolute URL) when the word is a link. The word may carry a trailing space.</summary>
    public static bool TryGetUrl(ReadOnlySpan<char> word, out string url)
    {
        url = "";

        int start = 0;
        int end = word.Length;
        while (start < end && (char.IsWhiteSpace(word[start]) || IsLeadingJunk(word[start])))
            start++;
        while (end > start && (char.IsWhiteSpace(word[end - 1]) || IsTrailingJunk(word[end - 1])))
            end--;

        var s = word.Slice(start, end - start);
        if (s.Length < 5 || s.IndexOf('.') < 0)
            return false;

        string candidate;
        if (
            s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
        )
        {
            candidate = s.ToString();
        }
        else if (s.StartsWith("www.", StringComparison.OrdinalIgnoreCase) && s.Slice(4).IndexOf('.') > 0)
        {
            candidate = "https://" + s.ToString();
        }
        else if (BareDomain.IsMatch(s))
        {
            candidate = "https://" + s.ToString();
        }
        else
        {
            return false;
        }

        if (
            !Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrEmpty(uri.Host)
            || !uri.Host.Contains('.')
        )
        {
            return false;
        }

        url = uri.AbsoluteUri;
        return true;
    }

    public static bool TryGetUrl(string word, out string url) => TryGetUrl(word.AsSpan(), out url);

    /// <summary>True when any word of the text is a link.</summary>
    public static bool ContainsLink(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 5 || text.IndexOf('.') < 0)
            return false;

        int i = 0;
        while (i < text.Length)
        {
            while (i < text.Length && text[i] == ' ')
                i++;
            int wordStart = i;
            while (i < text.Length && text[i] != ' ')
                i++;
            if (i > wordStart && TryGetUrl(text.AsSpan(wordStart, i - wordStart), out _))
                return true;
        }
        return false;
    }

    /// <summary>Cached per message: 0 = not checked yet, 1 = no link, 2 = has a link.</summary>
    public static bool HasLink(ChatMessage message)
    {
        if (message.LinkState == 0)
            message.LinkState = ContainsLink(message.Text) ? (sbyte)2 : (sbyte)1;
        return message.LinkState == 2;
    }

    public static bool HasLink(ModerationLogEntry entry)
    {
        if (entry.LinkState == 0)
            entry.LinkState = ContainsLink(entry.Text) ? (sbyte)2 : (sbyte)1;
        return entry.LinkState == 2;
    }

    /// <summary>Opens the link in the default browser. Only http(s) URLs get here (see TryGetUrl), anything else is refused.</summary>
    public static void Open(string url)
    {
        try
        {
            if (
                !Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            )
            {
                return;
            }

            Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            DebugLog.WriteException("ChatLinks.Open", ex);
        }
    }
}
