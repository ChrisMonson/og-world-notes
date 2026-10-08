using Og.Core.Model;
using Og.Core.Runtime;

namespace Og.Core.Persistence;

/// <summary>Converts live world objects to and from their stored form.</summary>
public static class SnapshotMapper
{
    // ---- characters ---------------------------------------------------------

    public static CharacterSnapshot ToSnapshot(this Character character) => new()
    {
        Id = character.Id,
        Name = character.Name,
        HitPoints = character.HitPoints,
        MaxHitPoints = character.MaxHitPoints,
        LocationId = character.CurrentLocationId,
        Inventory = [.. character.Inventory.Select(item => item.Name)],
        Weapon = character.Weapon?.Name,
        Armor = character.Armor?.Name,
        Conditions = [.. character.Conditions],
        DisabledInteractions = [.. character.DisabledInteractions],
        FiredInteractions = [.. character.FiredInteractions],
        Attributes = new Dictionary<string, string>(character.Attributes),
        BornAt = character.BornAt,
        DiedAt = character.DiedAt,
        CauseOfDeath = character.CauseOfDeath
    };

    public static Character ToCharacter(this CharacterSnapshot snapshot, World world)
    {
        var character = new Character(snapshot.Id, snapshot.Name, snapshot.BornAt, snapshot.DiedAt, snapshot.CauseOfDeath)
        {
            MaxHitPoints = snapshot.MaxHitPoints,
            HitPoints = snapshot.HitPoints,
            CurrentLocationId = snapshot.LocationId,
            Weapon = Resolve(world, snapshot.Weapon),
            Armor = Resolve(world, snapshot.Armor)
        };

        foreach (var name in snapshot.Inventory)
        {
            if (world.CreateItem(name) is { } item)
            {
                character.Inventory.Add(item);
            }
        }

        foreach (var condition in snapshot.Conditions) character.Conditions.Add(condition);
        foreach (var id in snapshot.DisabledInteractions) character.DisabledInteractions.Add(id);
        foreach (var id in snapshot.FiredInteractions) character.FiredInteractions.Add(id);
        foreach (var (key, value) in snapshot.Attributes) character.Attributes[key] = value;

        return character;
    }

    private static Item? Resolve(World world, string? name) =>
        name is null ? null : world.CreateItem(name);

    // ---- world --------------------------------------------------------------

    public static WorldSnapshot ToSnapshot(this World world, IReadOnlyDictionary<int, string> authoredDescriptions)
    {
        var disabled = new List<string>();
        var enabled = new List<string>();

        foreach (var interaction in world.AllInteractions())
        {
            if (interaction.Id is not { Length: > 0 } id)
            {
                continue;
            }

            (interaction.DisabledForWorld ? disabled : enabled).Add(id);
        }

        return new WorldSnapshot
        {
            Conditions = [.. world.Conditions],
            DisabledInteractions = [.. disabled],
            EnabledInteractions = [.. enabled],
            LocationDescriptions = world.Locations
                .Where(location => !authoredDescriptions.TryGetValue(location.Id, out var authored)
                                   || authored != location.Description)
                .ToDictionary(location => location.Id, location => location.Description),
            GroundItems = world.Locations
                .Where(location => location.Items.Count > 0)
                .ToDictionary(location => location.Id,
                              location => location.Items.Select(item => item.Name).ToArray()),
            // Only locations the world file gave a monster are recorded. A place
            // that never had one is not mentioned, so writing a creature into it
            // later is not read as "this place is empty".
            Monsters = world.Locations
                .Where(location => location.AuthoredMonster is { Length: > 0 })
                .ToDictionary(
                    location => location.Id,
                    location => location.Monster is { IsAlive: true } monster
                        ? new MonsterState(monster.Name, monster.HitPoints)
                        : null),
            ClearedAt = world.Locations
                .Where(location => location.AuthoredMonster is { Length: > 0 } && location.ClearedAt is not null)
                .ToDictionary(
                    location => location.Id,
                    location => location.ClearedAt!.Value.ToUnixTimeSeconds())
        };
    }

    /// <summary>
    /// Carries a running world's state onto a freshly loaded one.
    ///
    /// Where the world file has been rewritten since the snapshot was taken, the
    /// file wins: whoever wrote it spoke more recently than whatever happened in
    /// the world. That is what <paramref name="previouslyAuthored"/> is for —
    /// without it, a description changed by an interaction would silently swallow
    /// every later rewrite of that room.
    /// </summary>
    public static void Apply(this WorldSnapshot snapshot, World world,
                             IReadOnlyDictionary<int, string>? previouslyAuthored)
    {
        if (previouslyAuthored is not null)
        {
            foreach (var (locationId, authored) in previouslyAuthored)
            {
                if (world.AuthoredDescriptions.TryGetValue(locationId, out var current)
                    && current != authored)
                {
                    snapshot.LocationDescriptions.Remove(locationId);
                }
            }
        }

        snapshot.Apply(world);
    }

    public static void Apply(this WorldSnapshot snapshot, World world)
    {
        world.Conditions.Clear();

        foreach (var condition in snapshot.Conditions)
        {
            world.Conditions.Add(condition);
        }

        foreach (var id in snapshot.DisabledInteractions)
        {
            if (world.FindInteraction(id) is { } interaction) interaction.DisabledForWorld = true;
        }

        foreach (var id in snapshot.EnabledInteractions)
        {
            if (world.FindInteraction(id) is { } interaction) interaction.DisabledForWorld = false;
        }

        foreach (var (locationId, description) in snapshot.LocationDescriptions)
        {
            if (world.FindLocation(locationId) is { } location) location.Description = description;
        }

        foreach (var location in world.Locations)
        {
            location.Items.Clear();
        }

        foreach (var (locationId, items) in snapshot.GroundItems)
        {
            if (world.FindLocation(locationId) is not { } location)
            {
                continue;
            }

            foreach (var name in items)
            {
                if (world.CreateItem(name) is { } item) location.Items.Add(item);
            }
        }

        // A location absent from the snapshot keeps whatever the world file gave it,
        // so a monster newly written into a place survives the overlay.
        foreach (var (locationId, state) in snapshot.Monsters)
        {
            if (world.FindLocation(locationId) is not { } location)
            {
                continue;
            }

            // The world file may have changed which creature belongs here. If it
            // has, the new one stands, rather than the old one's corpse persisting.
            if (state is not null && !string.Equals(state.Name, location.AuthoredMonster,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (state is null)
            {
                if (snapshot.ClearedAt.TryGetValue(locationId, out var cleared))
                {
                    location.ClearedAt = DateTimeOffset.FromUnixTimeSeconds(cleared);
                }

                location.Monster = null;
                continue;
            }

            if (world.SpawnMonster(state.Name) is { } monster)
            {
                monster.HitPoints = Math.Clamp(state.HitPoints, 0, monster.MaxHitPoints);
                location.Monster = monster.IsAlive ? monster : null;
            }
        }
    }
}
