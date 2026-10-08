using Og.Core.Model;

namespace Og.Core.Definitions;

/// <summary>
/// Items are data, not code. Adding a new item is a world-file edit, never a build.
/// </summary>
public sealed class ItemDefinition
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ItemKind Kind { get; set; } = ItemKind.Misc;

    public int AttackBonus { get; set; }
    public int AttackDice { get; set; }
    public int ArmorClassBonus { get; set; }

    /// <summary>Hit points restored when used. Consumables only.</summary>
    public int HealAmount { get; set; }

    /// <summary>Whether using the item removes it from the inventory.</summary>
    public bool ConsumedOnUse { get; set; } = true;

    /// <summary>Marks the item as satisfying exit conditions.</summary>
    public bool IsKey { get; set; }
}
