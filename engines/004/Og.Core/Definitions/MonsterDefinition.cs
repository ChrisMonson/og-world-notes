namespace Og.Core.Definitions;

public sealed class MonsterDefinition
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public int HitPoints { get; set; }
    public int ArmorClass { get; set; }
    public int AttackStrength { get; set; }
    public int AttackDice { get; set; }
    public string[] Inventory { get; set; } = [];

    /// <summary>Seconds before a slain monster returns. Zero means it stays dead.</summary>
    public int RespawnSeconds { get; set; }
}
