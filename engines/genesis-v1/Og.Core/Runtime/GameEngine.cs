using Og.Core.Model;

namespace Og.Core.Runtime;

/// <summary>
/// The world's single mutator. All commands funnel through <see cref="HandleInput"/>
/// on one thread; nothing else writes world state.
/// </summary>
public sealed class GameEngine
{
    private readonly Random _random;
    private readonly IEventSink _events;
    private readonly CommandRegistry _commands = new();
    private readonly List<Session> _sessions = [];

    public World World { get; private set; }

    /// <summary>
    /// Puts a different world under the people already in it.
    ///
    /// Safe because nothing holds on to a place: characters carry a location
    /// number and every command resolves it afresh. Anyone standing somewhere the
    /// new world does not have is moved to where people begin.
    ///
    /// Must be called on the world's own thread, with nothing mid-command.
    /// </summary>
    public IReadOnlyList<Session> SwapWorld(World replacement)
    {
        World = replacement;

        var displaced = new List<Session>();

        foreach (var session in _sessions)
        {
            if (World.FindLocation(session.CurrentLocationId) is not null)
            {
                continue;
            }

            session.CurrentLocationId = World.SpawnLocation;
            displaced.Add(session);
        }

        return displaced;
    }

    /// <summary>Sessions of living characters only. The dead are removed immediately.</summary>
    public IReadOnlyList<Session> Sessions => _sessions;

    /// <summary>Raised when a character dies, so the host can close out its session.</summary>
    public event Action<Session>? CharacterDied;

    public GameEngine(World world, IEventSink events, int seed = 0)
    {
        World = world;
        _events = events;
        _random = seed == 0 ? new Random() : new Random(seed);
        DefaultCommands.Register(_commands);
    }

    public void Emit(GameEvent gameEvent) => _events.Emit(gameEvent);

    // ---- life and death -----------------------------------------------------

    /// <summary>Births a new character into the world at the spawn location.</summary>
    public Session Enter(string name, IOutputSink output)
    {
        var character = new Character(Guid.NewGuid().ToString("n")[..12], name)
        {
            CurrentLocationId = World.SpawnLocation
        };

        GiveStartingItems(character);

        var session = new Session(character, output);
        _sessions.Add(session);

        Emit(GameEvent.For(session, EventType.CharacterBorn, ("name", name)));
        Emit(GameEvent.For(session, EventType.Arrived));

        BroadcastToLocation(session.CurrentLocationId, $"{name} appears.", except: session);
        session.Write(DescribeCurrentLocation(session));

        return session;
    }

    /// <summary>Returns an existing character to the world, exactly as it was left.</summary>
    public Session Resume(Character character, IOutputSink output)
    {
        if (character.IsDead)
        {
            throw new InvalidOperationException($"Character {character.Id} is dead and cannot return.");
        }

        if (World.FindLocation(character.CurrentLocationId) is null)
        {
            character.CurrentLocationId = World.SpawnLocation;
        }

        var session = new Session(character, output);
        _sessions.Add(session);

        Emit(GameEvent.For(session, EventType.Arrived));
        BroadcastToLocation(session.CurrentLocationId, $"{character.Name} appears.", except: session);
        session.Write(DescribeCurrentLocation(session));

        return session;
    }

    /// <summary>A living character leaving the world without dying — a disconnect.</summary>
    public void Depart(Session session)
    {
        if (!_sessions.Remove(session))
        {
            return;
        }

        Emit(GameEvent.For(session, EventType.Departed));
        BroadcastToLocation(session.CurrentLocationId, $"{session.Character.Name} fades away.", except: session);
    }

    /// <summary>
    /// The character is marked dead, drops everything it carried where it fell,
    /// and its session ends.
    /// </summary>
    public void Die(Session session, string cause)
    {
        var character = session.Character;
        var location = CurrentLocation(session);

        character.MarkDead(cause);

        var dropped = character.AllPossessions().ToList();

        if (location is not null)
        {
            location.Items.AddRange(dropped);
        }

        character.Inventory.Clear();
        character.Weapon = null;
        character.Armor = null;

        Emit(GameEvent.For(session, EventType.Death,
            ("cause", cause),
            ("name", character.Name),
            ("lifetime_seconds", ((int)(DateTimeOffset.UtcNow - character.BornAt).TotalSeconds).ToString()),
            ("dropped", string.Join(", ", dropped.Select(item => item.Name)))));

        BroadcastToLocation(session.CurrentLocationId,
            $"{character.Name} is killed by {cause}.", except: session);

        if (dropped.Count > 0)
        {
            BroadcastToLocation(session.CurrentLocationId,
                $"Their belongings fall to the ground: {string.Join(", ", dropped.Select(item => item.Name))}.",
                except: session);
        }

        session.Write($"\nYou are killed by {cause}.\n\nThat is the end of {character.Name}.");

        _sessions.Remove(session);
        CharacterDied?.Invoke(session);
    }

    private void GiveStartingItems(Character character)
    {
        foreach (var name in World.StartingItems)
        {
            if (World.CreateItem(name) is not { } item)
            {
                continue;
            }

            switch (item.Kind)
            {
                case ItemKind.Weapon when character.Weapon is null:
                    character.Weapon = item;
                    break;

                case ItemKind.Armor when character.Armor is null:
                    character.Armor = item;
                    break;

                default:
                    character.Inventory.Add(item);
                    break;
            }
        }
    }

    public Location? CurrentLocation(Session session) => World.FindLocation(session.CurrentLocationId);

    // ---- input ---------------------------------------------------------------

    public void HandleInput(Session session, string rawCommand)
    {
        if (string.IsNullOrWhiteSpace(rawCommand) || session.Character.IsDead)
        {
            return;
        }

        var raw = rawCommand.Trim();
        var command = TriggerMatcher.Normalize(rawCommand);

        Emit(GameEvent.For(session, EventType.Command, ("text", raw)));

        // The place gets first refusal on a blow, and only on a blow.
        //
        // Everywhere else the engine answers first and the room picks up what
        // it drops, which is the right way round: most verbs have a plain
        // mechanical meaning and the room is the exception. Combat cannot work
        // that way. Attack rolls, prints and mutates before it can return, so
        // by the time it fell through the dice would already be on the table
        // and whatever the place had written would arrive underneath "You
        // missed!". The only living thing in Ambroke is a heron, the quay has
        // words for what it is to put a knife into it, and two lines of
        // arithmetic printed over them is the engine shouting in a world that
        // has its own voice.
        //
        // Narrow on purpose. Only violence; only when there is something alive
        // in front of you to aim it at, so a room cannot answer for a fight
        // that has nothing to fight; and only when the place has actually
        // written for these words. A room with nothing to say about a fight is
        // unaffected, and the dice roll exactly as they did before.
        if (DefaultCommands.IsViolence(command)
            && CurrentLocation(session)?.Monster is { IsAlive: true }
            && TryInteractions(session, command, namedOnly: true))
        {
            return;
        }

        var result = _commands.Execute(this, session, command, raw);

        if (result.Status == CommandStatus.Done)
        {
            return;
        }

        // A verb that came to nothing still carries on to the place the player is
        // standing in: the room may know what the engine does not.
        if (TryInteractions(session, command))
        {
            return;
        }

        // Nothing anywhere had an answer. Recorded rather than discarded, with
        // whatever the engine was going to say about it.
        Emit(GameEvent.For(session, EventType.UnparsedCommand,
            ("text", raw),
            ("kind", result.Status == CommandStatus.Futile ? "no effect" : "unrecognised"),
            ("refusal", result.Message ?? "")));

        session.Write(result.Message ?? "Nothing happens.");
    }

    /// <summary>
    /// Lets the place overhear something shouted in it. Speech is delivered
    /// either way; this is the room answering as well, when it has an answer.
    ///
    /// Only yelling gets this. Saying and whispering are aimed at people, and a
    /// room that talks back over a conversation is a nuisance; a yell is aimed
    /// at the place itself. Somebody stood at a well they had just been told
    /// held a drowned man and shouted HELLO down it, and the world said only
    /// "You yell: HELLO?". That is what this is for.
    /// </summary>
    public bool Overhear(Session session, string spoken)
    {
        if (string.IsNullOrWhiteSpace(spoken))
        {
            return false;
        }

        return TryInteractions(session, TriggerMatcher.Normalize(spoken));
    }

    /// <summary>
    /// <paramref name="namedOnly"/> makes wildcards not count, so only an
    /// interaction that spells the command out can answer. Used where the room
    /// speaks before the engine rather than after it; see HandleInput.
    /// </summary>
    private bool TryInteractions(Session session, string command, bool namedOnly = false)
    {
        var location = CurrentLocation(session);

        if (location is null)
        {
            return false;
        }

        // Snapshot: an action may enable or disable interactions mid-iteration.
        foreach (var interaction in location.Interactions.ToArray())
        {
            var matches = namedOnly
                ? interaction.NamesTrigger(command)
                : interaction.MatchesTrigger(command);

            if (!interaction.IsEligible(World, session) || !matches)
            {
                continue;
            }

            Emit(GameEvent.For(session, EventType.InteractionFired,
                ("interaction", interaction.Id ?? "(anonymous)"),
                ("command", command)));

            foreach (var action in interaction.Actions)
            {
                Execute(session, action);
            }

            if (interaction.Once && interaction.Id is not null)
            {
                session.Character.FiredInteractions.Add(interaction.Id);
            }

            return true;
        }

        return false;
    }

    // ---- interaction actions --------------------------------------------------

    private void Execute(Session session, WorldAction action)
    {
        switch (action.Kind)
        {
            case ActionKind.Describe:
                session.Write(action.FirstParameter);
                break;

            case ActionKind.AddItems:
                foreach (var name in action.Parameters)
                {
                    if (World.CreateItem(name) is { } item)
                    {
                        session.Character.Inventory.Add(item);
                        session.Write($"You received {item.Name}.");
                        Emit(GameEvent.For(session, EventType.ItemPickedUp, ("item", item.Name)));
                    }
                }
                break;

            case ActionKind.RemoveItems:
                foreach (var name in action.Parameters)
                {
                    if (session.Character.FindItem(name) is { } item)
                    {
                        session.Character.Inventory.Remove(item);
                        Emit(GameEvent.For(session, EventType.ItemDropped, ("item", item.Name)));
                    }
                }
                break;

            case ActionKind.ChangeLocation:
                MoveTo(session, action.FirstParameterAsInt);
                break;

            case ActionKind.SetLocationDescription:
                if (World.FindLocation(action.FirstParameterAsInt) is { } target
                    && action.Parameters.Length > 1)
                {
                    target.Description = action.Parameters[1];
                }
                break;

            case ActionKind.EnableInteraction:
                SetInteractionDisabled(session, action, disabled: false);
                break;

            case ActionKind.DisableInteraction:
                SetInteractionDisabled(session, action, disabled: true);
                break;

            case ActionKind.AddCondition:
                ConditionSet(session, action.Scope).Add(action.FirstParameter);
                break;

            case ActionKind.RemoveCondition:
                ConditionSet(session, action.Scope).Remove(action.FirstParameter);
                break;

            case ActionKind.Announce:
                // The doer already has their own account of it; this is for
                // everyone else, wherever they are standing.
                if (action.FirstParameter.Length > 0)
                {
                    BroadcastToWorld(action.FirstParameter, except: session);
                    Emit(GameEvent.For(session, EventType.Announcement, ("text", action.FirstParameter)));
                }
                break;
        }
    }

    private HashSet<string> ConditionSet(Session session, Scope scope) =>
        scope == Scope.World ? World.Conditions : session.Character.Conditions;

    private void SetInteractionDisabled(Session session, WorldAction action, bool disabled)
    {
        var id = action.FirstParameter;

        if (action.Scope == Scope.Player)
        {
            if (disabled)
            {
                session.Character.DisabledInteractions.Add(id);
            }
            else
            {
                session.Character.DisabledInteractions.Remove(id);
            }

            return;
        }

        if (World.FindInteraction(id) is { } interaction)
        {
            interaction.DisabledForWorld = disabled;
        }
    }

    // ---- movement --------------------------------------------------------------

    public void MoveTo(Session session, int locationId)
    {
        var destination = World.FindLocation(locationId);

        if (destination is null)
        {
            Emit(GameEvent.For(session, EventType.BlockedMovement, ("target", locationId.ToString())));
            session.Write("You can't go that way.");
            return;
        }

        var from = session.CurrentLocationId;

        BroadcastToLocation(from, $"{session.Character.Name} leaves.", except: session);
        session.CurrentLocationId = destination.Id;
        BroadcastToLocation(destination.Id, $"{session.Character.Name} arrives.", except: session);

        Emit(GameEvent.For(session, EventType.Movement,
            ("from", from.ToString()), ("to", destination.Id.ToString())));

        session.Write(DescribeCurrentLocation(session));
    }

    // ---- combat ------------------------------------------------------------------

    public void Attack(Session session)
    {
        var location = CurrentLocation(session);

        if (location?.Monster is not { } monster || !monster.IsAlive)
        {
            session.Write("There is nothing to attack here.");
            return;
        }

        var character = session.Character;
        var attackRoll = _random.Next(1, 21) + character.AttackStrength;
        var damage = _random.Next(1, character.AttackDice + 1) + character.AttackStrength;

        if (monster.ReceiveAttack(attackRoll, damage))
        {
            session.Write("You hit!");
            Emit(GameEvent.For(session, EventType.Combat,
                ("target", monster.Name), ("result", "hit"), ("damage", damage.ToString())));

            if (!monster.IsAlive)
            {
                session.Write($"The {monster.Name} has died.");
                BroadcastToLocation(location.Id, $"{character.Name} slays the {monster.Name}.", except: session);

                location.Items.AddRange(monster.Inventory);
                Emit(GameEvent.For(session, EventType.MonsterSlain, ("monster", monster.Name)));

                // The world is told, in the passive voice, that this creature is
                // not standing there any more. Without it a room has no way to
                // know: interactions can test conditions and nothing else, so a
                // quay whose heron has been killed goes on describing it alive
                // to whoever walks in next. Removed again when it returns.
                World.Conditions.Add(SlainCondition(monster.Name));

                // And the scar, which nothing ever takes off again. See KilledCondition.
                World.Conditions.Add(KilledCondition(monster.Name));
                location.Monster = null;
                location.ClearedAt = DateTimeOffset.UtcNow;

                if (location.Items.Count > 0)
                {
                    session.Write(GroundLine(location));
                }

                return;
            }
        }
        else
        {
            session.Write("You missed!");
            Emit(GameEvent.For(session, EventType.Combat, ("target", monster.Name), ("result", "miss")));
        }

        var monsterRoll = _random.Next(1, 21) + monster.AttackStrength;
        var monsterDamage = _random.Next(1, monster.AttackDice + 1) + monster.AttackStrength;

        if (character.ReceiveAttack(monsterRoll, monsterDamage))
        {
            session.Write($"You were hit! HP: {character.HitPoints}/{character.MaxHitPoints}");

            if (!character.IsAlive)
            {
                Die(session, $"a {monster.Name}");
            }
        }
        else
        {
            session.Write($"The {monster.Name} missed!");
        }
    }

    /// <summary>
    /// What a place's own words can test to find out whether something that
    /// lived there is dead. "grey heron" becomes "slain_grey_heron".
    /// </summary>
    public static string SlainCondition(string monsterName) => "slain_" + Slug(monsterName);

    /// <summary>
    /// The scar, as against the state.
    ///
    /// SlainCondition is true only while the creature is not standing there; the
    /// moment it comes back, fifteen minutes later, the condition is taken away
    /// and every sentence hanging off it goes quiet. That is right for describing
    /// an empty quay. It is wrong for describing what was done on it.
    ///
    /// This one is set at the same moment and removed by nothing, ever. It says
    /// a creature of this kind was killed here once, which stays true after the
    /// respawn, after the killer has gone, and for everybody who arrives long
    /// afterwards and was not there.
    ///
    /// I am keeping the 900 seconds — a world whose only living thing can be
    /// permanently deleted by the first person through the door is a worse world
    /// for the second person. But a world where the only irreversible thing
    /// anybody can do is undone by a timer is one where nothing anybody does
    /// lands. So: the creature comes back, and the fact does not go away.
    /// </summary>
    public static string KilledCondition(string monsterName) => "killed_" + Slug(monsterName);

    private static string Slug(string name) =>
        new(name.Trim().ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : '_').ToArray());

    /// <summary>
    /// Brings back creatures whose time has passed. Called on the world's own
    /// thread, so nothing else is mid-way through reading the map.
    /// </summary>
    public void Repopulate()
    {
        foreach (var location in World.Repopulate(DateTimeOffset.UtcNow))
        {
            World.Conditions.Remove(SlainCondition(location.Monster!.Name));

            Emit(new GameEvent(Actor.Engine, EventType.MonsterReturned, null, location.Id,
                new Dictionary<string, string> { ["monster"] = location.Monster!.Name }));

            BroadcastToLocation(location.Id,
                $"Something moves. {location.Monster!.Description}");
        }
    }

    // ---- description ----------------------------------------------------------------

    public string DescribeCurrentLocation(Session session)
    {
        var location = CurrentLocation(session);

        if (location is null)
        {
            return "You are nowhere.";
        }

        var parts = new List<string> { location.Description };

        if (location.Monster is { IsAlive: true } monster)
        {
            parts.Add(monster.Description);
        }

        if (location.Items.Count > 0)
        {
            parts.Add(GroundLine(location));
        }

        var others = PlayersAt(location.Id).Where(other => other != session).ToList();

        if (others.Count > 0)
        {
            parts.Add($"Also here: {string.Join(", ", others.Select(other => other.Character.Name))}.");
        }

        var directions = location.OpenDirections()
            .Select(direction => direction.ToString().ToLowerInvariant())
            .ToList();

        if (directions.Count > 0)
        {
            parts.Add($"There is a passage to the {Join(directions)}.");
        }

        // Worth knowing the first time and a nuisance forever after. This line is
        // the engine shouting in a world that has its own voice, and it is also
        // the thing that invites the loop: look, read "You've found the exit!",
        // type exit, be told you escaped, still be standing here, look again.
        // Once you have been out that way, you know where the door is.
        if (location.IsExit && !session.Character.Conditions.Contains("has_left"))
        {
            parts.Add("You've found the exit!");
        }

        return string.Join("\n\n", parts);
    }

    /// <summary>
    /// What is lying here, with repeats counted instead of listed one by one.
    ///
    /// Everything a character carried drops where they fall and nothing decays,
    /// so a room people die in silts up. The quay reached thirteen entries —
    /// "clasp-knife, oilskin coat, grey feather, grey feather, oar, oar,
    /// windfall apple, stopped apple, oar, windfall apple, clasp-knife, oilskin
    /// coat, grey feather" — printed underneath every paragraph the place has,
    /// every time anybody looked at it.
    ///
    /// This is not tidying. Nothing is removed and every one of them is still
    /// there to pick up. It is the difference between a room saying what is on
    /// it and a room reading out a manifest.
    /// </summary>
    private static string GroundLine(Location location)
    {
        var counted = location.Items
            .GroupBy(item => item.Name)
            .Select(group => group.Count() == 1 ? group.Key : $"{group.Key} ×{group.Count()}");

        return $"On the ground: {string.Join(", ", counted)}.";
    }

    private static string Join(IReadOnlyList<string> values) => values.Count switch
    {
        1 => values[0],
        2 => $"{values[0]} and {values[1]}",
        _ => $"{string.Join(", ", values.Take(values.Count - 1))}, and {values[^1]}"
    };

    // ---- broadcast --------------------------------------------------------------------

    public IEnumerable<Session> PlayersAt(int locationId) =>
        _sessions.Where(session => session.CurrentLocationId == locationId);

    public void BroadcastToLocation(int locationId, string message, Session? except = null)
    {
        foreach (var session in PlayersAt(locationId).ToArray())
        {
            if (session != except)
            {
                session.Write(message);
            }
        }
    }

    public void BroadcastToWorld(string message, Session? except = null)
    {
        foreach (var session in _sessions.ToArray())
        {
            if (session != except)
            {
                session.Write(message);
            }
        }
    }
}
