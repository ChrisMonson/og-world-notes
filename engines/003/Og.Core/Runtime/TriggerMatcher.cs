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

    /// <summary>
    /// How many single-character edits turn one word into the other. People
    /// mistype under pressure and a world that answers "Nothing happens" to
    /// "attack the gobljn" is being obtuse on purpose.
    /// </summary>
    public static int Distance(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;

            for (var j = 1; j <= b.Length; j++)
            {
                var substitution = previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), substitution);
            }

            (previous, current) = (current, previous);
        }

        return previous[b.Length];
    }

    /// <summary>
    /// The one candidate within <paramref name="tolerance"/> edits of the word, or
    /// null if there is none — or more than one. A guess is only worth making when
    /// nothing else is as close.
    /// </summary>
    public static string? Nearest(string word, IEnumerable<string> candidates, int tolerance)
    {
        string? best = null;
        var bestDistance = int.MaxValue;
        var tied = false;

        foreach (var candidate in candidates)
        {
            var distance = Distance(word, candidate);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = candidate;
                tied = false;
            }
            else if (distance == bestDistance)
            {
                tied = true;
            }
        }

        return !tied && bestDistance <= tolerance ? best : null;
    }
}
