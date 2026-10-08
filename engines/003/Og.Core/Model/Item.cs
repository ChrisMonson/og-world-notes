using Og.Core.Definitions;
using Og.Core.Runtime;

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
    /// nobody types "health potion" when "potion" is what the thing is. Nor do
    /// they type "shortsword" when they are holding a sword and a troll is
    /// looking at them — so the end or the start of a word counts too, and a
    /// letter's worth of misspelling is forgiven on anything long enough to be
    /// unmistakable.
    /// </summary>
    public bool Matches(string name)
    {
        var wanted = name.Trim();

        if (wanted.Length == 0)
        {
            return false;
        }

        if (string.Equals(Name, wanted, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var words = Name.Split(' ');

        if (words.Any(word => string.Equals(word, wanted, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // "sword" is what a shortsword is. "pot" is what a potion is, in a hurry.
        if (wanted.Length >= 3 && words.Any(word =>
                word.Length > wanted.Length
                && (word.StartsWith(wanted, StringComparison.OrdinalIgnoreCase)
                    || word.EndsWith(wanted, StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        // "armour" is armor. "potoin" is a potion. Short words are left alone:
        // at four letters a single slip turns one real thing into another.
        return wanted.Length >= 5 && words.Any(word =>
            word.Length >= 5
            && TriggerMatcher.Distance(word.ToLowerInvariant(), wanted.ToLowerInvariant()) <= 1);
    }

    public override string ToString() => Name;
}
