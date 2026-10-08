namespace Og.Core.Runtime;

public static class EventType
{
    public const string Command = "command";
    public const string UnparsedCommand = "unparsed_command";
    public const string Movement = "movement";
    public const string BlockedMovement = "blocked_movement";
    public const string Combat = "combat";
    public const string Death = "death";
    public const string CharacterBorn = "character_born";
    public const string MonsterSlain = "monster_slain";
    public const string MonsterReturned = "monster_returned";
    public const string MonsterWandered = "monster_wandered";
    public const string WorldReloaded = "world_reloaded";
    public const string ItemPickedUp = "item_picked_up";
    public const string ItemDropped = "item_dropped";
    public const string ItemUnequipped = "item_unequipped";
    public const string Emote = "emote";
    public const string InteractionFired = "interaction_fired";
    public const string Speech = "speech";
    public const string Arrived = "arrived";
    public const string Departed = "departed";
    public const string Escaped = "escaped";
    public const string DivineAct = "divine_act";
    public const string EngineException = "engine_exception";
}

public static class Actor
{
    public const string God = "god";
    public const string Engine = "engine";
}

/// <summary>
/// One row in the world's event log. Events are keyed by the character that acted
/// and the location it acted in.
/// </summary>
public sealed record GameEvent(
    string Actor,
    string Type,
    string? CharacterId,
    int? LocationId,
    IReadOnlyDictionary<string, string> Payload)
{
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    public static GameEvent For(
        Session session,
        string type,
        params (string Key, string Value)[] payload) =>
        new(session.CharacterId,
            type,
            session.CharacterId,
            session.CurrentLocationId,
            payload.ToDictionary(entry => entry.Key, entry => entry.Value));
}

public interface IEventSink
{
    void Emit(GameEvent gameEvent);
}

public sealed class CollectingEventSink : IEventSink
{
    public List<GameEvent> Events { get; } = [];

    public void Emit(GameEvent gameEvent) => Events.Add(gameEvent);
}
