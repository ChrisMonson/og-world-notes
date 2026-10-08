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

    /// <summary>
    /// The line a room keeps its dead under. Parsed back out of the description
    /// each time, so the paragraph is rewritten rather than piled up.
    /// </summary>
    private const string MemorialHeading = "Someone died here:";

    /// <summary>
    /// The same thing for the other ending, kept apart from the dead because the
    /// difference is the whole point of it.
    /// </summary>
    private const string LeavingHeading = "Walked out of here:";

    /// <summary>How many of a place's dead it goes on naming. The oldest fade.</summary>
    private const int MemorialLength = 3;

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

    /// <summary>
    /// Raised when a character is finished — killed, or walked out — so the host
    /// can close out its session and let whoever is there name another.
    /// </summary>
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
    /// and its session ends. The place it fell in keeps its name.
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
            Remember(location, MemorialHeading, $"{character.Name}, killed by {cause}.", MemorialLength);
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

    /// <summary>
    /// Walking out. The other ending, and the only one anybody chooses.
    ///
    /// It is as final as being killed — one life, no way back in — but it is not
    /// a death, and the differences are the whole point of it: nothing is
    /// dropped, because what you earned goes out of the world with you; the door
    /// keeps your name where the ground would have kept your corpse; and what is
    /// written against you afterwards says you left on your own feet.
    ///
    /// Before this existed, finishing this world got you a line of congratulation
    /// and left you standing in it with nothing further to do. Someone did that
    /// twice in one night and then asked, out loud, what happens now.
    /// </summary>
    public void Leave(Session session)
    {
        var character = session.Character;
        var location = CurrentLocation(session);
        var carried = character.AllPossessions().Select(item => item.Name).ToList();
        var lifetime = (int)(DateTimeOffset.UtcNow - character.BornAt).TotalSeconds;

        character.MarkLeft();

        if (location is not null)
        {
            Remember(location, LeavingHeading, $"{character.Name}, who got out.", MemorialLength);
        }

        Emit(GameEvent.For(session, EventType.Escaped,
            ("name", character.Name),
            ("lifetime_seconds", lifetime.ToString()),
            ("carried", string.Join(", ", carried))));

        BroadcastToWorld($"{character.Name} has walked out of the valley.", except: session);

        session.Write(
            "You go through. The daylight is flat and grey and enormous, and the door swings to behind you "
            + "with a sound the rock takes and keeps.\n\n"
            + (carried.Count > 0
                ? $"You are still carrying: {string.Join(", ", carried)}. Nobody takes it off you.\n\n"
                : "You are carrying nothing at all, and you are still going.\n\n")
            + $"That is the end of {character.Name}, who was not killed.");

        _sessions.Remove(session);
        CharacterDied?.Invoke(session);
    }

    /// <summary>
    /// Writes what happened to people into the ground it happened on. A pile of
    /// gear on a floor says nothing about whose it was; whoever walks in next
    /// should be able to find that out from the room rather than from a god. Kept
    /// to the last few entries so a busy room does not turn into a ledger, and
    /// stored in the description because that is the one thing a place carries
    /// across restarts.
    ///
    /// A room may hold more than one of these lists — a heap people leave things
    /// on is not a grave, and the hill has both kinds of history in it — so a
    /// block runs from its heading to the first blank line, and everything after
    /// that is put back exactly as it was found.
    /// </summary>
    private static void Remember(Location location, string heading, string entry, int keep)
    {
        var description = location.Description;
        var entries = new List<string>();
        var tail = string.Empty;

        var index = description.IndexOf(heading, StringComparison.Ordinal);

        if (index >= 0)
        {
            var after = description[(index + heading.Length)..].TrimStart('\r', '\n');
            var lines = after.Split('\n');
            var taken = 0;

            while (taken < lines.Length && !string.IsNullOrWhiteSpace(lines[taken]))
            {
                entries.Add(lines[taken].Trim());
                taken++;
            }

            tail = string.Join("\n", lines.Skip(taken)).Trim('\r', '\n', ' ');
            description = description[..index].TrimEnd();
        }

        entries.Add(entry);

        if (keep > 0 && entries.Count > keep)
        {
            entries.RemoveRange(0, entries.Count - keep);
        }

        var rebuilt = $"{description}\n\n{heading}\n{string.Join("\n", entries)}";

        location.Description = tail.Length > 0 ? $"{rebuilt}\n\n{tail}" : rebuilt;
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

    private bool TryInteractions(Session session, string command)
    {
        var location = CurrentLocation(session);

        if (location is null)
        {
            return false;
        }

        // Snapshot: an action may enable or disable interactions mid-iteration.
        foreach (var interaction in location.Interactions.ToArray())
        {
            if (!interaction.IsEligible(World, session) || !interaction.MatchesTrigger(command))
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

            // A place that can mend someone. "full" or a number of hit points.
            case ActionKind.Heal:
            {
                var character = session.Character;
                var missing = character.MaxHitPoints - character.HitPoints;

                var wanted = string.Equals(action.FirstParameter, "full", StringComparison.OrdinalIgnoreCase)
                    ? missing
                    : action.FirstParameterAsInt;

                var healed = Math.Clamp(wanted, 0, missing);
                character.HitPoints += healed;

                session.Write(healed > 0
                    ? $"HP: {character.HitPoints}/{character.MaxHitPoints}"
                    : "You were not carrying any hurt worth mending.");
                break;
            }

            // Leaving a mark. The one thing a character can do that outlives it:
            // the room keeps the line, the line keeps the name, and whoever comes
            // through next reads it without being told by a god that it is there.
            case ActionKind.Inscribe:
            {
                if (CurrentLocation(session) is not { } here || action.Parameters.Length < 2)
                {
                    break;
                }

                var heading = action.Parameters[0];
                var entry = action.Parameters[1]
                    .Replace("{name}", session.Character.Name, StringComparison.OrdinalIgnoreCase);

                var keep = action.Parameters.Length > 2 && int.TryParse(action.Parameters[2], out var limit)
                    ? limit
                    : MemorialLength;

                Remember(here, heading, entry, keep);

                Emit(GameEvent.For(session, EventType.Inscribed,
                    ("heading", heading), ("entry", entry)));
                break;
            }

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
        session.PreviousLocationId = from;
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
                location.Monster = null;
                location.ClearedAt = DateTimeOffset.UtcNow;

                if (location.Items.Count > 0)
                {
                    session.Write($"On the ground: {string.Join(", ", location.Items.Select(item => item.Name))}.");
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
    /// Brings back creatures whose time has passed. Called on the world's own
    /// thread, so nothing else is mid-way through reading the map.
    /// </summary>
    public void Repopulate()
    {
        foreach (var location in World.Repopulate(DateTimeOffset.UtcNow))
        {
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
            parts.Add($"On the ground: {string.Join(", ", location.Items.Select(item => item.Name))}.");
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

        if (location.IsExit)
        {
            parts.Add("This is the way out. Walking through it ends this life; the word is exit.");
        }

        return string.Join("\n\n", parts);
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
