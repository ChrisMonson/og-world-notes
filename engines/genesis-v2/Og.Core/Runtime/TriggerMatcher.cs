namespace Og.Core.Runtime;

/// <summary>
/// Glob-style trigger matching carried over from the original engine:
/// "*word*" contains, "*word" ends with, "word*" starts with, bare word is exact.
/// </summary>
public static class TriggerMatcher
{
    private static readonly char[] Punctuation = ['.', '!', '?', ','];

    public static bool Matches(string trigger, string command)
    {
        var text = Normalize(command);
        var needle = trigger.Replace("*", "").Trim().ToLowerInvariant();

        if (needle.Length == 0)
        {
            return false;
        }

        var leading = trigger.StartsWith('*');
        var trailing = trigger.EndsWith('*');

        return (leading, trailing) switch
        {
            (true, true) => text.Contains(needle, StringComparison.Ordinal),
            (true, false) => text.EndsWith(needle, StringComparison.Ordinal),
            (false, true) => text.StartsWith(needle, StringComparison.Ordinal),
            _ => text == needle
        };
    }

    public static string Normalize(string command)
    {
        var text = command.Trim().ToLowerInvariant();

        foreach (var character in Punctuation)
        {
            text = text.Replace(character.ToString(), "");
        }

        return text.Trim();
    }
}
