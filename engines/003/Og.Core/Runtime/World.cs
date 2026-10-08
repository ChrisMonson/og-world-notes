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

    public static World FromJson(string json) => new(Parse(json));

    private static WorldDefinition Parse(string json) =>
        JsonSerializer.Deserialize<WorldDefinition>(json, JsonOptions)
        ?? throw new InvalidDataException("World definition did not deserialize.");

    /// <summary>
    /// Loads a world file together with any fragments lying beside it, in a
    /// directory named after it: world/maps/starter.json is joined by every
    /// *.json in world/maps/starter.d/, applied in filename order.
    ///
    /// This exists because the starter map is fifty-six kilobytes in one file
    /// and the only way anyone tending this world can write to it is to send
    /// the whole thing back at once, from memory, having read it in pieces.
    /// Three caretakers in a row have written down that the map was effectively
    /// unreachable and pushed their fix into the engine instead — which was
    /// sometimes the right answer and sometimes only the reachable one. A room
    /// that has been asked the same question by three different people should
    /// be answerable in twenty lines, not in a rewrite of the whole valley with
    /// every inscription in it hand-copied and one of them dropped.
    ///
    /// A fragment adds and it changes; it does not blank. It may add locations,
    /// items and monsters; it may add interactions to a location that already
    /// exists, or replace one outright by reusing its interactionId, or stand
    /// one in front of everything already there with interactionsFirst; it may
    /// set a name, a description, a creature, or open an exit. What it cannot
    /// do is remove — a zero connection or an empty string in a fragment means
    /// "not mentioned", not "delete". To take something out, edit the base file.
    /// </summary>
    public static World Load(string path)
    {
        var definition = Parse(File.ReadAllText(path));

        foreach (var fragment in Fragments(path))
        {
            Merge(definition, Parse(File.ReadAllText(fragment)));
        }

        return new World(definition);
    }

    /// <summary>The fragment files that belong to a given world file, in order.</summary>
    public static IReadOnlyList<string> Fragments(string path)
    {
        var parent = Path.GetDirectoryName(path);

        var directory = Path.Combine(
            string.IsNullOrEmpty(parent) ? "." : parent,
            Path.GetFileNameWithoutExtension(path) + ".d");

        return Directory.Exists(directory)
            ? [.. Directory.GetFiles(directory, "*.json").OrderBy(name => name, StringComparer.Ordinal)]
            : [];
    }

    /// <summary>Folds a fragment into a world definition. See <see cref="Load"/>.</summary>
    public static void Merge(WorldDefinition into, WorldDefinition fragment)
    {
        into.Items = Combined(into.Items, fragment.Items, item => item.Name);
        into.Monsters = Combined(into.Monsters, fragment.Monsters, monster => monster.Name);

        var locations = into.Locations.ToList();

        foreach (var addition in fragment.Locations)
        {
            var existing = locations.FirstOrDefault(candidate => candidate.Index == addition.Index);

            if (existing is null)
            {
                // Nothing to stand in front of yet; order is just order.
                if (addition.InteractionsFirst.Length > 0)
                {
                    addition.Interactions = [.. addition.InteractionsFirst, .. addition.Interactions];
                    addition.InteractionsFirst = [];
                }

                locations.Add(addition);
                continue;
            }

            if (addition.Name.Length > 0)
            {
                existing.Name = addition.Name;
            }

            if (addition.Description.Length > 0)
            {
                existing.Description = addition.Description;
            }

            if (addition.IsExit)
            {
                existing.IsExit = true;
            }

            if (addition.Monster is { Length: > 0 } monster)
            {
                existing.Monster = monster;
            }

            if (addition.Connections.North != 0) existing.Connections.North = addition.Connections.North;
            if (addition.Connections.East != 0) existing.Connections.East = addition.Connections.East;
            if (addition.Connections.South != 0) existing.Connections.South = addition.Connections.South;
            if (addition.Connections.West != 0) existing.Connections.West = addition.Connections.West;

            existing.Interactions = Combined(existing.Interactions, addition.Interactions,
                interaction => interaction.InteractionId ?? "");

            existing.Interactions = Ahead(existing.Interactions, addition.InteractionsFirst,
                interaction => interaction.InteractionId ?? "");
        }

        into.Locations = [.. locations];
    }

    /// <summary>
    /// Appends, except where something of the same name is already there, in
    /// which case the newcomer takes its place — and keeps its place in the
    /// order, because the order interactions are tried in is the order they are
    /// written in, and a fragment that replaces a greedy trigger has to sit
    /// where the greedy one sat to be any use. Anything unnamed is only ever
    /// appended; it can never displace anything.
    /// </summary>
    private static T[] Combined<T>(T[] original, T[] additions, Func<T, string> key)
    {
        if (additions.Length == 0)
        {
            return original;
        }

        var merged = original.ToList();

        foreach (var addition in additions)
        {
            var name = key(addition);

            var at = name.Length == 0
                ? -1
                : merged.FindIndex(candidate =>
                    string.Equals(key(candidate), name, StringComparison.OrdinalIgnoreCase));

            if (at >= 0)
            {
                merged[at] = addition;
            }
            else
            {
                merged.Add(addition);
            }
        }

        return [.. merged];
    }

    /// <summary>
    /// Puts the additions at the front, in the order given, and takes any older
    /// namesake out of the line behind them. For the case where a specific
    /// answer has to be tried before a greedy one that is already in the room.
    /// </summary>
    private static T[] Ahead<T>(T[] original, T[] additions, Func<T, string> key)
    {
        if (additions.Length == 0)
        {
            return original;
        }

        var behind = original.ToList();

        foreach (var addition in additions)
        {
            var name = key(addition);

            if (name.Length == 0)
            {
                continue;
            }

            behind.RemoveAll(candidate =>
                string.Equals(key(candidate), name, StringComparison.OrdinalIgnoreCase));
        }

        return [.. additions, .. behind];
    }

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
    public bool HasCondition(Session session, string condition) =>
        Conditions.Contains(condition) || session.Character.Conditions.Contains(condition);

    public IEnumerable<Interaction> AllInteractions() =>
        _locations.Values.SelectMany(location => location.Interactions);

    public Interaction? FindInteraction(string id) =>
        AllInteractions().FirstOrDefault(interaction =>
            string.Equals(interaction.Id, id, StringComparison.OrdinalIgnoreCase));
}
