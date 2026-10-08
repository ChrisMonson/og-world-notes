namespace Og.Core.Model;

/// <summary>
/// Turning what someone typed into the name of a thing.
///
/// People say "the potion" and "an apple"; the world stores "health potion" and
/// "apple". Naming a thing with its article is the commonest way to say it, so
/// it must not be a way to fail — someone reaching for a potion at three hit
/// points is not in a position to guess the phrasing the parser wants.
/// </summary>
public static class Naming
{
    private static readonly string[] Articles = ["the ", "a ", "an ", "my ", "some "];

    /// <summary>
    /// What was typed, with any leading articles removed. Used for looking things
    /// up and for saying back what could not be found, so the refusal reads as
    /// English rather than "you aren't carrying a the potion".
    /// </summary>
    public static string Strip(string? spoken)
    {
        var wanted = (spoken ?? string.Empty).Trim();

        bool stripped;

        do
        {
            stripped = false;

            foreach (var article in Articles)
            {
                if (!wanted.StartsWith(article, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                wanted = wanted[article.Length..].TrimStart();
                stripped = true;
                break;
            }
        }
        while (stripped);

        return wanted;
    }
}
