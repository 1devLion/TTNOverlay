namespace TTNOverlay.Twitch;

/// <summary>
/// Parses raw Twitch IRC protocol lines into tags, prefix, command, params, and trailing text.
/// </summary>
public class ParsedIrcLine
{
    public Dictionary<string, string> Tags { get; } = new();
    public string? Prefix { get; set; }
    public string Command { get; set; } = "";
    public List<string> Params { get; } = new();
    public string? Trailing { get; set; }
}

public static class IrcMessageParser
{
    public static ParsedIrcLine Parse(string line)
    {
        var result = new ParsedIrcLine();
        if (line.Length == 0) return result;

        int pos = 0;

        if (line[0] == '@')
        {
            int end = line.IndexOf(' ');
            if (end < 0) end = line.Length;
            var tagsPart = line[1..end];
            foreach (var kv in tagsPart.Split(';'))
            {
                var idx = kv.IndexOf('=');
                if (idx < 0) { result.Tags[kv] = ""; continue; }
                var key = kv[..idx];
                var val = kv[(idx + 1)..];
                result.Tags[key] = UnescapeTagValue(val);
            }
            pos = Math.Min(end + 1, line.Length);
        }

        if (pos < line.Length && line[pos] == ':')
        {
            int end = line.IndexOf(' ', pos);
            if (end < 0) end = line.Length;
            result.Prefix = line[(pos + 1)..end];
            pos = Math.Min(end + 1, line.Length);
        }

        int trailingStart = line.IndexOf(" :", pos, StringComparison.Ordinal);
        string mainPart;
        if (trailingStart >= 0)
        {
            mainPart = line[pos..trailingStart];
            result.Trailing = line[(trailingStart + 2)..];
        }
        else
        {
            mainPart = line[pos..];
        }

        var tokens = mainPart.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 0)
        {
            result.Command = tokens[0];
            result.Params.AddRange(tokens.Skip(1));
        }
        if (result.Trailing != null) result.Params.Add(result.Trailing);

        return result;
    }

    private static string UnescapeTagValue(string val)
    {
        if (val.IndexOf('\\') < 0) return val;

        var sb = new System.Text.StringBuilder(val.Length);
        int i = 0;
        while (i < val.Length)
        {
            char c = val[i];
            if (c == '\\' && i + 1 < val.Length)
            {
                char next = val[i + 1];
                sb.Append(next switch
                {
                    's' => ' ',
                    ':' => ';',
                    'r' => '\r',
                    'n' => '\n',
                    '\\' => '\\',

                    _ => next,
                });
                i += 2;
            }
            else
            {

                if (c != '\\') sb.Append(c);
                i++;
            }
        }
        return sb.ToString();
    }
}

