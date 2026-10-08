using Og.Core.Definitions;
using Og.Core.Model;

namespace Og.Core.Runtime;

public sealed class Location
{
    public int Id { get; }
    public string Name { get; }
    public string Description { get; set; }
    public bool IsExit { get; }

    /// <summary>The world's own parting words, if it authored any.</summary>
    public string? Farewell { get; }

    /// <summary>Conditions that must hold before this exit will open.</summary>
    public string[] ExitRequires { get; }

    public int North { get; set; }
    public int East { get; set; }
    public int South { get; set; }
    public int West { get; set; }

    /// <summary>Shared across all players: one player killing it clears it for everyone.</summary>
    public Monster? Monster { get; set; }

    /// <summary>The creature the world file declares belongs here, if any.</summary>
    public string? AuthoredMonster { get; }

    /// <summary>When this location's creature was last killed, if it was.</summary>
    public DateTimeOffset? ClearedAt { get; set; }

    /// <summary>Seconds before a slain creature returns here. Zero means it stays dead.</summary>
    public int RespawnSeconds { get; set; }

    /// <summary>Items dropped here, visible to everyone in the room.</summary>
    public List<Item> Items { get; } = [];

    public List<Interaction> Interactions { get; } = [];

    /// <summary>Keys the file set here that the loader threw away. Carried for the validator.</summary>
    public string[] UnknownKeys { get; }

    /// <summary>The same, for this location's connections block.</summary>
    public string[] UnknownConnectionKeys { get; }

    public Location(LocationDefinition definition)
    {
        Id = definition.Index;
        UnknownKeys = definition.UnknownKeys;
        UnknownConnectionKeys = definition.Connections.UnknownKeys;
        Name = definition.Name;
        Description = definition.Description;
        IsExit = definition.IsExit;
        Farewell = definition.Farewell;
        ExitRequires = definition.ExitRequires;
        North = definition.Connections.North;
        East = definition.Connections.East;
        South = definition.Connections.South;
        West = definition.Connections.West;
        AuthoredMonster = definition.Monster;
    }

    public int Exit(Direction direction) => direction switch
    {
        Direction.North => North,
        Direction.East => East,
        Direction.South => South,
        Direction.West => West,
        _ => 0
    };

    public IEnumerable<Direction> OpenDirections()
    {
        if (North > 0) yield return Direction.North;
        if (East > 0) yield return Direction.East;
        if (South > 0) yield return Direction.South;
        if (West > 0) yield return Direction.West;
    }
}

public enum Direction
{
    North,
    East,
    South,
    West
}
