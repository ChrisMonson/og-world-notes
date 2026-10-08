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

    /// <summary>
    /// Whether this character went out through the door alive. A life can end two
    /// ways here and this is the other one; it is kept in <see cref="Attributes"/>
    /// so that it needs no column of its own.
    /// </summary>
    public bool HasEscaped => Attributes.ContainsKey("escaped_at");

    /// <summary>
    /// Whether this life is over — killed or walked out. Storage asks this
    /// question to decide whether a returning visitor picks their character back
    /// up or names a new one, and the answer is the same either way: they do not
    /// come back. <see cref="DiedAt"/> remains the record of actual death, and
    /// <see cref="HasEscaped"/> of the other ending; only this asks about both.
    /// </summary>
    public bool IsDead => DiedAt is not null || HasEscaped;
}

public sealed record MonsterState(string Name, int HitPoints);

/// <summary>
/// One of a place's dead, flattened for storage.
///
/// This is deliberately not a reference to a character row. A life here is
/// singular and nothing may link one to another, and a place's dead are not the
/// characters — they are what the floor kept of them: a name, what did it, how
/// long they lasted, and what fell out of their hands.
/// </summary>
public sealed record FallenState(
    string Name,
    string Cause,
    int LifetimeSeconds,
    string Carried,
    long At);

/// <summary>
/// Something said in a place and left there, flattened for storage. A name and
/// a sentence; no link to the character, who is very likely dead by the time
/// anybody reads it.
/// </summary>
public sealed record WordsState(string Speaker, string Text, long At);

/// <summary>
/// One of the people who got out through a door, flattened for storage. Like
/// <see cref="FallenState"/> it is not a reference to a character row: what the
/// door kept is a name, how long they were down here, and what went out with
/// them. The character itself is finished and nothing may reach back into it.
/// </summary>
public sealed record SurvivorState(
    string Name,
    int LifetimeSeconds,
    string Carried,
    long At);

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

    /// <summary>Who has died where. Outlives every character in it.</summary>
    public Dictionary<int, FallenState[]> Dead { get; init; } = [];

    /// <summary>What has been said where, and left there. Outlives the speaker.</summary>
    public Dictionary<int, WordsState[]> Spoken { get; init; } = [];

    /// <summary>Who has got out where, and with what. Outlives them too.</summary>
    public Dictionary<int, SurvivorState[]> Survivors { get; init; } = [];

    /// <summary>
    /// Locations whose monster has been killed, and what stood there. A location
    /// absent from this is left as the world file declares it, so a creature newly
    /// written into a place is not wiped by the record of that place being empty.
    /// </summary>
    public Dictionary<int, MonsterState?> Monsters { get; init; } = [];

    /// <summary>When each cleared location's monster fell, for working out returns.</summary>
    public Dictionary<int, long> ClearedAt { get; init; } = [];
}
