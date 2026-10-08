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
    public bool Matches(string name)
    {
        var wanted = StripArticle(name);

        if (wanted.Length == 0)
        {
            return false;
        }

        if (string.Equals(Name, wanted, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Name.Split(' ').Any(word =>
            string.Equals(word, wanted, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// "the oil", "a flask", "my token": people name things the way they talk.
    /// </summary>
    public static string StripArticle(string name)
    {
        var text = name.Trim();

        foreach (var article in new[] { "the ", "a ", "an ", "my ", "some " })
        {
            if (text.StartsWith(article, StringComparison.OrdinalIgnoreCase))
            {
                return text[article.Length..].Trim();
            }
        }

        return text;
    }

    public override string ToString() => Name;
}
