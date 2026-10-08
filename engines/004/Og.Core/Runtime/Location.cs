using Og.Core.Definitions;
using Og.Core.Model;

namespace Og.Core.Runtime;

/// <summary>
/// A way a place reads, and what has to be true for it to read that way. See
/// <see cref="DescriptionVariantDefinition"/> for why these exist.
/// </summary>
public sealed class DescriptionVariant
{
    public string[] If { get; }
    public string[] IfNot { get; }
    public string Description { get; }

    public DescriptionVariant(DescriptionVariantDefinition definition)
    {
        If = definition.If;
        IfNot = definition.IfNot;
        Description = definition.Description;
    }

    public bool Holds(World world, Session session)
    {
        foreach (var condition in If)
        {
            if (!world.HasCondition(session, condition))
            {
                return false;
            }
        }

        foreach (var condition in IfNot)
        {
            if (world.HasCondition(session, condition))
            {
                return false;
            }
        }

        return true;
    }
}

public sealed class Location
{
    /// <summary>
    /// How many of its dead a place keeps. Deep enough that nobody in this
    /// world's lifetime will fall off the end of it, shallow enough that the
    /// stored overlay cannot grow without bound.
    /// </summary>
    public const int RemembranceLimit = 24;

    /// <summary>
    /// How much of what has been said here a place keeps. Shallower than its
    /// dead on purpose: a room that opens with twenty strangers' sentences is a
    /// guestbook, and nobody reads a guestbook. The oldest fall off, so what is
    /// here is what was said recently, by people who may still be alive.
    /// </summary>
    public const int SpokenLimit = 8;

    /// <summary>
    /// How many of the people who got out a door keeps. The same depth as the
    /// dead, because the two lists are the same question asked twice and it
    /// would be a strange world that forgot its successes faster.
    /// </summary>
    public const int SurvivorLimit = 24;

    public int Id { get; }
    public string Name { get; }
    public string Description { get; set; }
    public bool IsExit { get; }

    /// <summary>
    /// Other ways this place can read, in the order the world file wrote them.
    /// The first whose conditions hold wins; otherwise <see cref="Description"/>.
    /// </summary>
    public IReadOnlyList<DescriptionVariant> DescriptionVariants { get; }

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

    /// <summary>
    /// Everyone who has died here, oldest first. Outlives every one of them and
    /// every world restart; see <see cref="Model.Fallen"/> for why.
    /// </summary>
    public List<Fallen> Dead { get; } = [];

    /// <summary>
    /// What has been said here to nobody, oldest first. Outlives the speaker;
    /// see <see cref="Model.Words"/> for why.
    /// </summary>
    public List<Words> Spoken { get; } = [];

    /// <summary>
    /// Everyone who went out through here alive, oldest first. In practice only
    /// an exit ever has any; see <see cref="Model.Survivor"/> for why it is kept
    /// at all.
    /// </summary>
    public List<Survivor> Survivors { get; } = [];

    public List<Interaction> Interactions { get; } = [];

    public Location(LocationDefinition definition)
    {
        Id = definition.Index;
        Name = definition.Name;
        Description = definition.Description;
        DescriptionVariants = [.. definition.DescriptionWhen.Select(variant => new DescriptionVariant(variant))];
        IsExit = definition.IsExit;
        North = definition.Connections.North;
        East = definition.Connections.East;
        South = definition.Connections.South;
        West = definition.Connections.West;
        AuthoredMonster = definition.Monster;
    }

    /// <summary>Adds somebody to this place's dead, dropping the oldest past the limit.</summary>
    public void Remember(Fallen who)
    {
        Dead.Add(who);

        if (Dead.Count > RemembranceLimit)
        {
            Dead.RemoveRange(0, Dead.Count - RemembranceLimit);
        }
    }

    /// <summary>Leaves words here, dropping the oldest past the limit.</summary>
    public void Keep(Words words)
    {
        Spoken.Add(words);

        if (Spoken.Count > SpokenLimit)
        {
            Spoken.RemoveRange(0, Spoken.Count - SpokenLimit);
        }
    }

    /// <summary>Adds somebody to the people who got out here, oldest dropped past the limit.</summary>
    public void RecordEscape(Survivor who)
    {
        Survivors.Add(who);

        if (Survivors.Count > SurvivorLimit)
        {
            Survivors.RemoveRange(0, Survivors.Count - SurvivorLimit);
        }
    }

    /// <summary>The most recent person of that name to die here, if any.</summary>
    public Fallen? FindDead(string name)
    {
        for (var index = Dead.Count - 1; index >= 0; index--)
        {
            if (Dead[index].Matches(name))
            {
                return Dead[index];
            }
        }

        return null;
    }

    /// <summary>The most recent person of that name to get out here, if any.</summary>
    public Survivor? FindSurvivor(string name)
    {
        for (var index = Survivors.Count - 1; index >= 0; index--)
        {
            if (Survivors[index].Matches(name))
            {
                return Survivors[index];
            }
        }

        return null;
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
