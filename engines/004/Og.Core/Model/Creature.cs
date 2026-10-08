namespace Og.Core.Model;

public abstract class Creature
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int HitPoints { get; set; }
    public int MaxHitPoints { get; set; }
    public List<Item> Inventory { get; } = [];

    public abstract int ArmorClass { get; }
    public abstract int AttackStrength { get; }
    public abstract int AttackDice { get; }

    public bool IsAlive => HitPoints > 0;

    /// <summary>
    /// How badly hurt this thing looks, in words rather than numbers. Null if it
    /// is dead or unmarked.
    ///
    /// A fight here takes about five exchanges and every swing is close to a
    /// coin, so a person three hits in is nearly finished and has no way to know
    /// it. Worse: monsters keep their wounds while a character can be mended at
    /// a fire, which means walking away and coming back is the strongest move in
    /// this world — and it is only worth making if you can see that what you did
    /// last time is still there. Numbers would settle it too flatly; this is the
    /// same information at the resolution the room descriptions are written in.
    /// </summary>
    public string? WoundLook()
    {
        if (!IsAlive || MaxHitPoints <= 0 || HitPoints >= MaxHitPoints)
        {
            return null;
        }

        // Whole quarters remaining, 0 to 3 — the guard above rules out a full
        // creature, so this never reaches 4. Held in its own local rather than
        // written inline: `switch` grabs the smallest expression to its left, so
        // `a / b switch { ... }` quietly matches on b, which is not what anyone
        // reading it assumes and does not always fail to compile.
        var quartersLeft = HitPoints * 4 / MaxHitPoints;

        return quartersLeft switch
        {
            3 => "scratched",
            2 => "bleeding",
            1 => "badly hurt",
            _ => "barely standing"
        };
    }

    /// <summary>Resolves an incoming attack. Returns true if it connected.</summary>
    public bool ReceiveAttack(int attackRoll, int damage)
    {
        if (attackRoll < ArmorClass)
        {
            return false;
        }

        HitPoints = Math.Max(0, HitPoints - damage);
        return true;
    }

    public Item? FindItem(string name) =>
        Inventory.FirstOrDefault(item => item.Matches(name));

    /// <summary>Whether this creature is what someone just named.</summary>
    public bool Matches(string name)
    {
        var wanted = name.Trim();

        if (wanted.Length == 0)
        {
            return true;
        }

        if (string.Equals(Name, wanted, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Name.Split(' ').Any(word =>
            string.Equals(word, wanted, StringComparison.OrdinalIgnoreCase));
    }
}
