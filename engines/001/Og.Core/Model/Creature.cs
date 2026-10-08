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
}
