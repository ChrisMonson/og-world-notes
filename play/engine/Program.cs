using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using Og.Core.Runtime;

// One world in the browser, played by one person at a time. The world outlives
// each life in it: a character that dies or gets out is finished, and the next
// one arrives in the same world, with whatever the last one left behind.
//
// The server around the engine did three things the engine does not do for
// itself, and this does them instead: it brought monsters back on a clock, it
// ran any per-tick hook a caretaker added to it, and it started a new life when
// the last one ended.
return;

public static partial class Game
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private static GameEngine? _engine;
    private static Session? _session;
    private static Lines _output = new();
    private static MethodInfo[] _tick = [];
    private static readonly Sink _events = new();
    private static string? _ended;
    private static string[] _welcome = [];

    /// <summary>
    /// Loads a world. Options: seed, tick (names of extra parameterless engine
    /// methods the run's server called on every tick), welcome (lines the run's
    /// server said to a newborn character).
    /// </summary>
    [JSExport]
    public static string Load(string worldJson, string optionsJson)
    {
        Options options;
        World world;
        try
        {
            options = JsonSerializer.Deserialize<Options>(optionsJson, Json) ?? new Options();
            world = World.FromJson(worldJson);
        }
        catch (Exception e)
        {
            return Reply(new { type = "error", message = e.Message });
        }

        var issues = WorldValidator.Validate(world);
        if (WorldValidator.HasErrors(issues))
            return Reply(new { type = "error", message = "the world failed validation", issues = issues.Select(i => i.ToString()).ToArray() });

        _engine = new GameEngine(world, _events, options.Seed);
        _session = null;
        _ended = null;
        _welcome = options.Welcome ?? [];

        var missing = new List<string>();
        _tick = (options.Tick ?? []).Select(name =>
        {
            var method = typeof(GameEngine).GetMethod(name, BindingFlags.Public | BindingFlags.Instance, Type.EmptyTypes);
            if (method is null) missing.Add(name);
            return method;
        }).OfType<MethodInfo>().ToArray();

        return Reply(new { type = "loaded", world = world.Name, missing });
    }

    /// <summary>A new life in the loaded world.</summary>
    [JSExport]
    public static string Arrive(string name)
    {
        if (_engine is null) return Reply(new { type = "error", message = "no world loaded" });
        if (Alive) return Reply(new { type = "error", message = "a life is already in progress" });

        name = string.IsNullOrWhiteSpace(name) ? "Stranger" : name.Trim();
        _output = new Lines();
        _ended = null;
        _session = _engine.Enter(name, _output);
        foreach (var line in _welcome) _session.Write(line);
        return Turn("arrived", null);
    }

    [JSExport]
    public static string Send(string command)
    {
        if (_engine is null || _session is null) return Reply(new { type = "error", message = "nobody is playing" });
        if (!Alive) return Turn("over", command);

        _events.Clear();
        _engine.HandleInput(_session, command);
        Settle();
        Tick();
        return Turn("turn", command);
    }

    /// <summary>
    /// What the server did every fifteen seconds whether or not anyone typed:
    /// let monsters return, and anything else the run's server did on its tick.
    /// Returns whatever the player saw happen meanwhile.
    /// </summary>
    [JSExport]
    public static string Idle()
    {
        if (_engine is null) return Reply(new { type = "error", message = "no world loaded" });
        Tick();
        return Turn("idle", null);
    }

    private static void Tick()
    {
        _engine!.Repopulate();
        foreach (var method in _tick) method.Invoke(_engine, null);
    }

    /// <summary>
    /// Works out whether the last command ended the life. Some engines close a
    /// life that gets out; others only record that it did and leave the character
    /// standing there, which on the server meant until its player closed the tab.
    /// Here, getting out ends it either way, and the character departs.
    /// </summary>
    private static void Settle()
    {
        var session = _session!;
        if (session.Character.IsDead) _ended = "died";
        else if (_events.Escaped(session.CharacterId) || !_engine!.Sessions.Contains(session)) _ended = "left";

        if (_ended == "left" && _engine!.Sessions.Contains(session)) _engine.Depart(session);
    }

    /// <summary>A life is over when it has died, or got out, or the engine has let go of it.</summary>
    private static bool Alive =>
        _session is not null && _ended is null && !_session.Character.IsDead && _engine!.Sessions.Contains(_session);

    private static string Turn(string type, string? command)
    {
        var alive = Alive;
        var character = _session?.Character;
        return Reply(new
        {
            type,
            command,
            text = _output.Drain(),
            alive,
            ended = _ended,
            name = character?.Name,
            place = alive ? _engine!.CurrentLocation(_session!)?.Name : null,
            hitPoints = alive ? character!.HitPoints : 0,
            maxHitPoints = character?.MaxHitPoints ?? 0,
        });
    }

    private static string Reply(object value) => JsonSerializer.Serialize(value, Json);

    private sealed class Options
    {
        public int Seed { get; set; }
        public string[]? Tick { get; set; }
        public string[]? Welcome { get; set; }
    }

    private sealed class Lines : IOutputSink
    {
        private readonly List<string> _lines = [];
        public void Write(string message) => _lines.Add(message);
        public string[] Drain() { var drained = _lines.ToArray(); _lines.Clear(); return drained; }
    }

    /// <summary>Keeps only what this page needs to know from the event log: who got out.</summary>
    private sealed class Sink : IEventSink
    {
        private readonly HashSet<string> _escaped = [];
        public void Emit(GameEvent gameEvent)
        {
            if (gameEvent.Type == "escaped" && gameEvent.CharacterId is { } id) _escaped.Add(id);
        }
        public bool Escaped(string characterId) => _escaped.Contains(characterId);
        public void Clear() => _escaped.Clear();
    }
}
