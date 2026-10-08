using Og.Core.Definitions;

namespace Og.Core.Model;

/// <summary>An instance of an item in the world, backed by its data definition.</summary>
public sealed class Item(ItemDefinition definition)
{
    /// <summary>
    /// How much of a word someone has to say before we take their meaning. Four
    /// letters, so "sword" reaches "shortsword" and "one" does not reach "bone".
    /// </summary>
    private const int ShortestPartialWord = 4;

    public ItemDefinition Definition { get; } = definition;

    public string Name => Definition.Name;
    public string Description => Definition.Description;
    public ItemKind Kind => Definition.Kind;

    /// <summary>
    /// Whether this is what someone meant. A whole name, or any word of one, or
    /// the end or start of such a word: nobody types "health potion" when
    /// "potion" is what the thing is, and nobody types "shortsword" when they
    /// have been calling it a sword for the last ten minutes.
    ///
    /// That last case is here because it happened. Krasus, at ten hit points
    /// with a cave troll in front of him, typed "equip sword" while carrying a
    /// shortsword and was told he was not carrying a sword. He recovered; the
    /// next person might not. A compound name must not be a way to fail.
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

        return Name.Split(' ').Any(word => IsWord(word, wanted));
    }

    private static bool IsWord(string word, string wanted)
    {
        if (string.Equals(word, wanted, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Half a compound word is still a name for the thing, but only once it is
        // long enough to have been meant. Below that it is a coincidence of
        // letters, and answering it would hand people the wrong item.
        return wanted.Length >= ShortestPartialWord
            && (word.EndsWith(wanted, StringComparison.OrdinalIgnoreCase)
                || word.StartsWith(wanted, StringComparison.OrdinalIgnoreCase));
    }

    public override string ToString() => Name;
}
