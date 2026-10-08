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

    /// <summary>
    /// A condition that asks what the character is holding rather than what flags
    /// they have set: "carrying:bone".
    /// </summary>
    public const string CarryingPrefix = "carrying:";

    /// <summary>
    /// A condition that asks what is alive in the room they are standing in:
    /// "monster:cave troll", or bare "monster:" for anything at all.
    /// </summary>
    public const string MonsterPrefix = "monster:";

    /// <summary>
    /// A condition that asks who has died where they are standing. Bare
    /// "fallen:" for anyone at all, "fallen:3" for at least three, and
    /// "fallen:Marrow" for one particular person.
    /// </summary>
    public const string FallenPrefix = "fallen:";

    /// <summary>
    /// A condition that asks what state the person asking is in. Bare "hurt:"
    /// for any wound at all, "hurt:half" for at or below half, and "hurt:11"
    /// for at or below that many hit points.
    ///
    /// Here because the one strong tactic this world has — hit it, walk back to
    /// the fire, sleep, return — is invisible to anybody who has not already
    /// found the fire, and the moment it needs saying is the moment somebody is
    /// bleeding. Nothing a place said could tell the difference between a person
    /// at full strength and a person one blow from the end of their only life.
    /// </summary>
    public const string HurtPrefix = "hurt:";

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
    /// How a place reads to this person right now.
    ///
    /// A room used to say one sentence forever. That sentence was written before
    /// anybody had ever been in the room, and it went on being said over the
    /// corpse of the creature it was written around. Now a place may be authored
    /// with alternatives, each guarded by the same conditions interactions use,
    /// and the first that holds is what it says. Nothing is required: a location
    /// with no variants reads exactly as it always did.
    /// </summary>
    public string DescriptionFor(Session session, Location location)
    {
        foreach (var variant in location.DescriptionVariants)
        {
            if (variant.Holds(this, session))
            {
                return variant.Description;
            }
        }

        return location.Description;
    }

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

    /// <summary>
    /// Whether a condition holds for this player right now.
    ///
    /// Five kinds. A plain name is a flag, set on the world or on the living
    /// character. "carrying:bone" asks what is in their hands. "monster:cave
    /// troll" asks what is alive in the room they are standing in. "fallen:"
    /// asks who has died on the floor they are standing on. "hurt:" asks what
    /// state they are in themselves.
    ///
    /// The last four are here because without them nothing a place said could
    /// depend on anything except flags it had set itself. A room could not tell
    /// whether you had brought the thing it was about, or whether the creature
    /// the whole scene was written around was still breathing — so anything
    /// written for a living troll would fire just as happily over its corpse.
    /// Flags cannot stand in for any of them: an item can be dropped, a monster
    /// comes back on its own, the dead belong to the place rather than to any
    /// character, and a wound closes at a fire.
    /// </summary>
    public bool HasCondition(Session session, string condition)
    {
        var asked = condition.Trim();

        if (asked.StartsWith(CarryingPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var wanted = Naming.Strip(asked[CarryingPrefix.Length..]);

            return wanted.Length > 0 && session.Character.FindItemOrEquipped(wanted) is not null;
        }

        if (asked.StartsWith(MonsterPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var wanted = Naming.Strip(asked[MonsterPrefix.Length..]);

            return FindLocation(session.CurrentLocationId) is { Monster: { IsAlive: true } monster }
                && monster.Matches(wanted);
        }

        if (asked.StartsWith(FallenPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return FallenHere(session, asked[FallenPrefix.Length..].Trim());
        }

        if (asked.StartsWith(HurtPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return Hurt(session, asked[HurtPrefix.Length..].Trim());
        }

        return Conditions.Contains(asked) || session.Character.Conditions.Contains(asked);
    }

    /// <summary>
    /// The three ways of asking about a place's dead: at all, at least this many,
    /// or this person in particular.
    /// </summary>
    private bool FallenHere(Session session, string asked)
    {
        if (FindLocation(session.CurrentLocationId) is not { } location)
        {
            return false;
        }

        if (asked.Length == 0)
        {
            return location.Dead.Count > 0;
        }

        return int.TryParse(asked, out var atLeast)
            ? location.Dead.Count >= atLeast
            : location.FindDead(asked) is not null;
    }

    /// <summary>
    /// The three ways of asking how badly somebody is hurt: at all, at or below
    /// half, or at or below a given number of hit points. A number is the useful
    /// one, because the number worth knowing is the hardest blow of whatever is
    /// standing in front of you.
    /// </summary>
    private static bool Hurt(Session session, string asked)
    {
        var character = session.Character;

        if (asked.Length == 0)
        {
            return character.HitPoints < character.MaxHitPoints;
        }

        if (string.Equals(asked, "half", StringComparison.OrdinalIgnoreCase))
        {
            return character.HitPoints * 2 <= character.MaxHitPoints;
        }

        return int.TryParse(asked, out var atMost) && character.HitPoints <= atMost;
    }

    public IEnumerable<Interaction> AllInteractions() =>
        _locations.Values.SelectMany(location => location.Interactions);

    public Interaction? FindInteraction(string id) =>
        AllInteractions().FirstOrDefault(interaction =>
            string.Equals(interaction.Id, id, StringComparison.OrdinalIgnoreCase));
}
