namespace Og.Core.Definitions;

public sealed class WorldDefinition
{
    public string Name { get; set; } = "world";

    /// <summary>Location id where new and respawning players appear.</summary>
    public int SpawnLocation { get; set; }

    /// <summary>Items every new player arrives carrying. Equipment is auto-equipped.</summary>
    public string[] StartingItems { get; set; } = [];

    public ItemDefinition[] Items { get; set; } = [];
    public MonsterDefinition[] Monsters { get; set; } = [];
    public LocationDefinition[] Locations { get; set; } = [];
}
