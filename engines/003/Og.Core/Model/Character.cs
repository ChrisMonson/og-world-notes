namespace Og.Core.Model;

/// <summary>
/// One mortal being. A character is born, lives, and ends exactly once. The end
/// is final: everything here is lost, including inventory, quest progress and
/// flags. Characters are identified only by their own id; connection handling is
/// the transport layer's concern and is not modelled here.
///
/// There are two ways to end. Most are killed. A few walk out of the door with a
/// key in their hand, and that is not a death — it is the other ending, and the
/// record keeps the difference.
/// </summary>
public sealed class Character : Creature
{
    public const int BaseArmorClass = 12;
    public const int BaseAttackStrength = 3;
    public const int StartingHitPoints = 20;

    /// <summary>
    /// What is written against a character who left through the door rather than
    /// being carried out of the world feet first.
    /// </summary>
    public const string WalkedOut = "walking out of the valley alive";

    /// <summary>Unique to this character.</summary>
    public string Id { get; }

    public DateTimeOffset BornAt { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DiedAt { get; private set; }
    public string? CauseOfDeath { get; private set; }

    public bool IsDead => DiedAt is not null;

    /// <summary>
    /// Finished, but standing up. Still <see cref="IsDead"/> as far as the world
    /// is concerned — one life, no return — but nobody killed them.
    /// </summary>
    public bool HasLeft => CauseOfDeath == WalkedOut;

    public int CurrentLocationId { get; set; }

    public Item? Weapon { get; set; }
    public Item? Armor { get; set; }

    /// <summary>Open-ended attributes, for anything not modelled as a field.</summary>
    public Dictionary<string, string> Attributes { get; } = [];

    /// <summary>
    /// Character-scoped state. All of it dies with the character: a new life starts
    /// with no flags, no quest progress, and nothing already fired.
    /// </summary>
    public HashSet<string> Conditions { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> DisabledInteractions { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> FiredInteractions { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Character(string id, string name)
    {
        Id = id;
        Name = name;
        MaxHitPoints = StartingHitPoints;
        HitPoints = StartingHitPoints;
    }

    /// <summary>Rebuilds a character from storage, preserving its whole history.</summary>
    internal Character(string id, string name, DateTimeOffset bornAt, DateTimeOffset? diedAt, string? causeOfDeath)
    {
        Id = id;
        Name = name;
        BornAt = bornAt;
        DiedAt = diedAt;
        CauseOfDeath = causeOfDeath;
    }

    public override int ArmorClass => BaseArmorClass + (Armor?.Definition.ArmorClassBonus ?? 0);
    public override int AttackStrength => BaseAttackStrength + (Weapon?.Definition.AttackBonus ?? 0);
    public override int AttackDice => Weapon?.Definition.AttackDice ?? 1;

    public bool HasKey => Inventory.Any(item => item.Definition.IsKey);

    public void MarkDead(string cause)
    {
        DiedAt = DateTimeOffset.UtcNow;
        CauseOfDeath = cause;
    }

    /// <summary>
    /// Finished without dying. Stored the same way a death is, because the world
    /// holds one life per character either way and nothing may come back through
    /// that door; only the cause says which ending it was.
    /// </summary>
    public void MarkLeft()
    {
        DiedAt = DateTimeOffset.UtcNow;
        CauseOfDeath = WalkedOut;
    }

    /// <summary>Everything carried or worn, for what the corpse leaves behind.</summary>
    public IEnumerable<Item> AllPossessions()
    {
        foreach (var item in Inventory)
        {
            yield return item;
        }

        if (Weapon is not null) yield return Weapon;
        if (Armor is not null) yield return Armor;
    }

    public Item? FindItemOrEquipped(string name)
    {
        if (FindItem(name) is { } carried)
        {
            return carried;
        }

        if (Weapon is not null && Weapon.Matches(name))
        {
            return Weapon;
        }

        return Armor is not null && Armor.Matches(name) ? Armor : null;
    }
}
