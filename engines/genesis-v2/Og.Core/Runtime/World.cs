using System.Text.Json;
using System.Text.Json.Serialization;
using Og.Core.Definitions;
using Og.Core.Model;

namespace Og.Core.Runtime;

/// <summary>
/// Shared world state. Loaded from a definition file on disk, never from an
/// embedded resource, so the world can be edited and reloaded while running.
/// </summary>
public sealed class World
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string Name { get; }
    public int SpawnLocation { get; }
    public IReadOnlyList<string> StartingItems { get; }

    private readonly Dictionary<int, Location> _locations = [];
    private readonly Dictionary<string, ItemDefinition> _items = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MonsterDefinition> _monsters = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>World-scoped condition flags, shared by every player.</summary>
    public HashSet<string> Conditions { get; } = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<int, string> _authoredDescriptions = [];

    /// <summary>Descriptions as written in the world file, before anything changed them.</summary>
    public IReadOnlyDictionary<int, string> AuthoredDescriptions => _authoredDescriptions;

    public IReadOnlyCollection<Location> Locations => _locations.Values;
    public IReadOnlyCollection<ItemDefinition> ItemDefinitions => _items.Values;

    private World(WorldDefinition definition)
    {
        Name = definition.Name;
        SpawnLocation = definition.SpawnLocation;
        StartingItems = definition.StartingItems;

        foreach (var item in definition.Items)
        {
            _items[item.Name] = item;
        }

        foreach (var monster in definition.Monsters)
        {
            _monsters[monster.Name] = monster;
        }

        foreach (var locationDefinition in definition.Locations)
        {
            var location = new Location(locationDefinition);

            foreach (var interaction in locationDefinition.Interactions)
            {
                location.Interactions.Add(new Interaction(interaction));
            }

            if (locationDefinition.Monster is { Length: > 0 } monsterName)
            {
                location.Monster = SpawnMonster(monsterName);
                location.RespawnSeconds = RespawnSecondsFor(monsterName);
            }

            _locations[location.Id] = location;
            _authoredDescriptions[location.Id] = location.Description;
        }

        if (SpawnLocation == 0 && _locations.Count > 0)
        {
            SpawnLocation = _locations.Keys.Min();
        }
    }

    public static World FromJson(string json)
    {
        var definition = JsonSerializer.Deserialize<WorldDefinition>(json, JsonOptions)
            ?? throw new InvalidDataException("World definition did not deserialize.");

        return new World(definition);
    }

    public static World Load(string path) => FromJson(File.ReadAllText(path));

    public Location? FindLocation(int id) =>
        _locations.TryGetValue(id, out var location) ? location : null;

    public ItemDefinition? FindItemDefinition(string name) =>
        _items.TryGetValue(name.Trim(), out var definition) ? definition : null;

    /// <summary>Creates an item instance, or null if no definition by that name exists.</summary>
    public Item? CreateItem(string name) =>
        FindItemDefinition(name) is { } definition ? new Item(definition) : null;

    public int RespawnSecondsFor(string name) =>
        _monsters.TryGetValue(name.Trim(), out var definition) ? definition.RespawnSeconds : 0;

    /// <summary>
    /// Returns creatures to the places that have been empty long enough. Without
    /// this a world is emptied permanently by whoever passes through it first.
    /// </summary>
    public IReadOnlyList<Location> Repopulate(DateTimeOffset now)
    {
        var returned = new List<Location>();

        foreach (var location in _locations.Values)
        {
            if (location.Monster is { IsAlive: true }
                || location.AuthoredMonster is not { Length: > 0 } name
                || location.RespawnSeconds <= 0
                || location.ClearedAt is not { } cleared
                || now - cleared < TimeSpan.FromSeconds(location.RespawnSeconds))
            {
                continue;
            }

            if (SpawnMonster(name) is not { } monster)
            {
                continue;
            }

            location.Monster = monster;
            location.ClearedAt = null;
            returned.Add(location);
        }

        return returned;
    }

    public Monster? SpawnMonster(string name)
    {
        if (!_monsters.TryGetValue(name.Trim(), out var definition))
        {
            return null;
        }

        var inventory = definition.Inventory
            .Select(CreateItem)
            .OfType<Item>();

        return new Monster(definition, inventory);
    }

    /// <summary>A condition holds if it is set on the world or on the living character.</summary>
    /// <remarks>
    /// "carrying:item name" holds while the character has that item in hand or worn,
    /// so a room can answer to what someone brought rather than to a flag that was
    /// set when they picked it up and never unset when they gave it away.
    /// </remarks>
    public bool HasCondition(Session session, string condition)
    {
        const string carrying = "carrying:";

        if (condition.StartsWith(carrying, StringComparison.OrdinalIgnoreCase))
        {
            var wanted = condition[carrying.Length..].Trim();

            return session.Character.AllPossessions().Any(item =>
                string.Equals(item.Name, wanted, StringComparison.OrdinalIgnoreCase));
        }

        // Holds while something alive shares the place with them.
        if (string.Equals(condition, "creature-here", StringComparison.OrdinalIgnoreCase))
        {
            return FindLocation(session.CurrentLocationId)?.Monster is { IsAlive: true };
        }

        return Conditions.Contains(condition) || session.Character.Conditions.Contains(condition);
    }

    public IEnumerable<Interaction> AllInteractions() =>
        _locations.Values.SelectMany(location => location.Interactions);

    public Interaction? FindInteraction(string id) =>
        AllInteractions().FirstOrDefault(interaction =>
            string.Equals(interaction.Id, id, StringComparison.OrdinalIgnoreCase));
}
