using Og.Core.Definitions;

namespace Og.Core.Model;

/// <summary>An instance of an item in the world, backed by its data definition.</summary>
public sealed class Item(ItemDefinition definition)
{
    public ItemDefinition Definition { get; } = definition;

    public string Name => Definition.Name;
    public string Description => Definition.Description;
    public ItemKind Kind => Definition.Kind;

    /// <summary>
    /// Whether this is what someone meant. A whole name, or any word of one:
    /// nobody types "health potion" when "potion" is what the thing is.
    /// </summary>
    /// <summary>
    /// Strips the little word people put in front of a noun. Without this the
    /// world answers "grab the spear" with "There is no the spear here to take"
    /// — the world contradicting itself in front of somebody being careful.
    /// </summary>
    public static string Bare(string name)
    {
        var wanted = name.Trim();

        foreach (var article in Articles)
        {
            if (wanted.StartsWith(article, StringComparison.OrdinalIgnoreCase))
            {
                return wanted[article.Length..].Trim();
            }
        }

        return wanted;
    }

    private static readonly string[] Articles = ["the ", "a ", "an ", "my ", "some ", "that ", "this "];

    public bool Matches(string name)
    {
        var wanted = Bare(name);

        if (wanted.Length == 0)
        {
            return false;
        }

        if (string.Equals(Name, wanted, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Hyphens are part of how these things are named — clasp-knife, oilskin
        // coat — and nobody types the hyphen half. Malcolm asked for "knife"
        // four times on the quay and the world said he had no such thing while
        // the clasp-knife was open in his hand.
        return Name.Split(' ', '-').Any(word =>
            string.Equals(word, wanted, StringComparison.OrdinalIgnoreCase));
    }

    public override string ToString() => Name;
}
