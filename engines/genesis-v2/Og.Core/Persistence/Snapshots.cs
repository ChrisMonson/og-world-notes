namespace Og.Core.Persistence;

/// <summary>
/// A character's full state, flattened for storage. Items are stored by name
/// because items are world data, not code.
/// </summary>
public sealed record CharacterSnapshot
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int HitPoints { get; init; }
    public int MaxHitPoints { get; init; }
    public int LocationId { get; init; }

    public string[] Inventory { get; init; } = [];
    public string? Weapon { get; init; }
    public string? Armor { get; init; }

    public string[] Conditions { get; init; } = [];
    public string[] DisabledInteractions { get; init; } = [];
    public string[] FiredInteractions { get; init; } = [];
    public Dictionary<string, string> Attributes { get; init; } = [];

    public DateTimeOffset BornAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DiedAt { get; init; }
    public string? CauseOfDeath { get; init; }

    public bool IsDead => DiedAt is not null;
}

public sealed record MonsterState(string Name, int HitPoints);

/// <summary>
/// Everything mutable about the world that is not in its definition file. The
/// file is the world as authored; this is what has happened to it since.
/// </summary>
public sealed record WorldSnapshot
{
    public string[] Conditions { get; init; } = [];
    public string[] DisabledInteractions { get; init; } = [];
    public string[] EnabledInteractions { get; init; } = [];
    public Dictionary<int, string> LocationDescriptions { get; init; } = [];
    public Dictionary<int, string[]> GroundItems { get; init; } = [];

    /// <summary>
    /// Locations whose monster has been killed, and what stood there. A location
    /// absent from this is left as the world file declares it, so a creature newly
    /// written into a place is not wiped by the record of that place being empty.
    /// </summary>
    public Dictionary<int, MonsterState?> Monsters { get; init; } = [];

    /// <summary>When each cleared location's monster fell, for working out returns.</summary>
    public Dictionary<int, long> ClearedAt { get; init; } = [];
}
