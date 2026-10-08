using System.Globalization;

namespace Og.Core.Model;

/// <summary>
/// One mortal being. A character is born, lives, and ends exactly once. Death is
/// final: everything here is lost, including inventory, quest progress and flags.
/// Walking out of the door alive is the other ending, and just as final — the
/// difference is only that you keep what you carried and nobody killed you.
/// Characters are identified only by their own id; connection handling is the
/// transport layer's concern and is not modelled here.
/// </summary>
public sealed class Character : Creature
{
    public const int BaseArmorClass = 12;
    public const int BaseAttackStrength = 3;
    public const int StartingHitPoints = 20;

    /// <summary>The two attributes that record a life ended by leaving rather than dying.</summary>
    public const string EscapedAtAttribute = "escaped_at";
    public const string CarriedOutAttribute = "carried_out";

    /// <summary>Unique to this character.</summary>
    public string Id { get; }

    public DateTimeOffset BornAt { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DiedAt { get; private set; }
    public string? CauseOfDeath { get; private set; }

    public bool IsDead => DiedAt is not null;

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

    // ---- leaving alive ------------------------------------------------------

    /// <summary>
    /// Whether this character went out through the door. Held in
    /// <see cref="Attributes"/> rather than a field of its own so that it stores
    /// and restores with everything else a character is, without a schema for it.
    /// </summary>
    public bool HasEscaped => Attributes.ContainsKey(EscapedAtAttribute);

    public DateTimeOffset? EscapedAt =>
        Attributes.TryGetValue(EscapedAtAttribute, out var value)
        && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;

    /// <summary>What was in their hands when they walked out. Their whole score.</summary>
    public string? CarriedOut =>
        Attributes.TryGetValue(CarriedOutAttribute, out var value) ? value : null;

    /// <summary>
    /// Dead or gone. Either way the life is over and will not be resumed: whoever
    /// connects next arrives as somebody new.
    /// </summary>
    public bool IsFinished => IsDead || HasEscaped;

    public void MarkEscaped(string carriedOut)
    {
        Attributes[EscapedAtAttribute] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        Attributes[CarriedOutAttribute] = carriedOut;
    }

    public void MarkDead(string cause)
    {
        DiedAt = DateTimeOffset.UtcNow;
        CauseOfDeath = cause;
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
