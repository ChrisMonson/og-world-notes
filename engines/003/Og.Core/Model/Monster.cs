using Og.Core.Definitions;

namespace Og.Core.Model;

public sealed class Monster : Creature
{
    private readonly MonsterDefinition _definition;

    public Monster(MonsterDefinition definition, IEnumerable<Item> inventory)
    {
        _definition = definition;
        Name = definition.Name;
        Description = definition.Description;
        MaxHitPoints = definition.HitPoints;
        HitPoints = definition.HitPoints;
        Inventory.AddRange(inventory);
    }

    public override int ArmorClass => _definition.ArmorClass;
    public override int AttackStrength => _definition.AttackStrength;
    public override int AttackDice => _definition.AttackDice;
}
