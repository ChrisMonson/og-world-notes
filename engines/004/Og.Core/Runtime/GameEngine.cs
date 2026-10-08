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

    /// <summary>
    /// Raised when a character's life is over and its session must be closed out.
    /// There are two ways that happens — killed, or walked out of the door alive —
    /// and from the host's point of view they are the same: this person does not
    /// come back, and whoever is at the other end may name somebody new.
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

        if (character.HasEscaped)
        {
            throw new InvalidOperationException($"Character {character.Id} left this world and cannot return.");
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
    /// The way out, taken.
    ///
    /// This is the only end to a life here that is not death, and until now it was
    /// not an end at all: the door said congratulations and the character went on
    /// standing in front of it with nothing left to do. The first person ever to
    /// open it asked, in as many words, "what happens now?" — and the honest
    /// answer was nothing. So now it finishes them, the way dying does: they go
    /// through, alive, keeping everything they carried, and they do not come back.
    /// What they walked out with is written into the record, because in a world
    /// where every life ends that is the whole of what anyone achieves.
    ///
    /// And the door keeps them. Twelve people got out before this was written and
    /// the door they opened did not know any of their names; see
    /// <see cref="Survivor"/>.
    /// </summary>
    public void Escape(Session session)
    {
        var character = session.Character;
        var location = CurrentLocation(session);
        var manifest = Manifest(character.AllPossessions().Select(item => item.Name));
        var lifetime = (int)(DateTimeOffset.UtcNow - character.BornAt).TotalSeconds;

        character.MarkEscaped(manifest);

        // Before the session goes. The door remembers this the way a floor
        // remembers whoever died on it — a name, how long, and what went with them.
        location?.RecordEscape(new Survivor(character.Name, lifetime, manifest, DateTimeOffset.UtcNow));

        Emit(GameEvent.For(session, EventType.Escaped,
            ("name", character.Name),
            ("lifetime_seconds", lifetime.ToString()),
            ("carried_out", manifest)));

        BroadcastToWorld(
            $"Somewhere far above, a door opens and closes again. {character.Name} is out.",
            except: session);

        session.Write(
            "The key turns over hard, and the door gives.\n\n" +
            "Beyond it is daylight, and cold air, and country this world does not know the shape of. " +
            $"You walk out into it carrying {manifest}, after {Spent(lifetime)} underground.\n\n" +
            $"That is the end of {character.Name} — the only ending here that is not dying. " +
            "Almost everything that has ever left this place left it on its back. You did not.\n\n" +
            "The door keeps your name on this side of it. Whoever stands here next will be told "
            + "that you came through, how long it took you, and what you were carrying when you did.");

        _sessions.Remove(session);
        CharacterDied?.Invoke(session);
    }

    private static string Spent(int seconds) => seconds switch
    {
        < 120 => "barely a minute",
        < 3600 => $"{Math.Max(2, seconds / 60)} minutes",
        < 7200 => "an hour",
        _ => $"{seconds / 3600} hours"
    };

    /// <summary>
    /// A pile of things said the way a person would say it.
    ///
    /// Every list this world shows anybody — what is lying on the floor, what a
    /// corpse spilled, and above all the manifest a character walks out of the
    /// door carrying — was a raw enumeration of item names with the duplicates
    /// left in. Wick escaped carrying "dagger, key, dagger, key, tin whistle,
    /// shortsword, leather armor". That is the last sentence anybody ever reads
    /// here, the summing-up of an entire life, and it read like a database dump.
    ///
    /// So: like things are counted together and the list is joined with an "and".
    /// Nothing about the world changes. It is only that the record of what someone
    /// managed is now in a language.
    /// </summary>
    public static string Manifest(IEnumerable<string> names)
    {
        var grouped = names
            .GroupBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Count() == 1
                ? group.Key
                : $"{Counted(group.Count())} {Plural(group.Key)}")
            .ToList();

        return grouped.Count == 0 ? "nothing at all" : Join(grouped);
    }

    private static string Counted(int count) => count switch
    {
        2 => "two",
        3 => "three",
        4 => "four",
        5 => "five",
        6 => "six",
        7 => "seven",
        8 => "eight",
        9 => "nine",
        _ => count.ToString()
    };

    private static string Capitalised(string word) =>
        word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..];

    private static string Plural(string name) =>
        name.EndsWith('s') || name.EndsWith('x') ||
        name.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("sh", StringComparison.OrdinalIgnoreCase)
            ? name + "es"
            : name + "s";

    /// <summary>
    /// The character is marked dead, drops everything it carried where it fell,
    /// and its session ends. The place keeps a record of them.
    /// </summary>
    public void Die(Session session, string cause)
    {
        var character = session.Character;
        var location = CurrentLocation(session);

        character.MarkDead(cause);

        var dropped = character.AllPossessions().ToList();
        var spilled = Manifest(dropped.Select(item => item.Name));
        var lifetime = (int)(DateTimeOffset.UtcNow - character.BornAt).TotalSeconds;

        if (location is not null)
        {
            location.Items.AddRange(dropped);

            // The floor keeps them. Not the character — that is finished and
            // nothing may reach back into it — only what the place saw: a name,
            // what did it, how long they lasted, and what fell.
            location.Remember(new Fallen(character.Name, cause, lifetime, spilled, DateTimeOffset.UtcNow));
        }

        character.Inventory.Clear();
        character.Weapon = null;
        character.Armor = null;

        Emit(GameEvent.For(session, EventType.Death,
            ("cause", cause),
            ("name", character.Name),
            ("lifetime_seconds", lifetime.ToString()),
            ("dropped", spilled)));

        BroadcastToLocation(session.CurrentLocationId,
            $"{character.Name} is killed by {cause}.", except: session);

        if (dropped.Count > 0)
        {
            BroadcastToLocation(session.CurrentLocationId,
                $"Their belongings fall to the ground: {spilled}.",
                except: session);
        }

        session.Write($"\nYou are killed by {cause}.\n\nThat is the end of {character.Name}.");

        _sessions.Remove(session);
        CharacterDied?.Invoke(session);
    }

    // ---- the dead ---------------------------------------------------------------

    /// <summary>
    /// What a place says about the people who have died on it, or null where
    /// nobody has.
    ///
    /// Every room in this world describes itself in exactly the sentence it was
    /// written with, whatever has happened in it since. The Deep Passage has
    /// killed four people and still opens with "far ahead, something shifts" —
    /// the same words it used before anybody had ever been down there. This is
    /// the one thing a place can now say that it was not authored with, and it
    /// is the only way one life here is ever visible to another.
    ///
    /// It names names, because the names are the whole of it, and it says out
    /// loud that they can be looked at — the lesson of the troll's bed being
    /// that a thing nobody is told about is a thing nobody finds.
    /// </summary>
    public static string? Remembrance(Location location)
    {
        var dead = location.Dead;

        if (dead.Count == 0)
        {
            return null;
        }

        if (dead.Count == 1)
        {
            return $"{dead[0].Name} died here, killed by {dead[0].Cause}. "
                + $"Look at {dead[0].Name} and this place will tell you who that was.";
        }

        var causes = dead
            .Select(fallen => fallen.Cause)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // "Two people have died here: Sable and Kaylee. All of them killed by a
        // cave troll." Two is not all-of-them, it is both of them, and the best
        // written room in this world said the wrong one every time anybody
        // walked into it.
        var shared = causes.Count != 1
            ? string.Empty
            : dead.Count == 2
                ? $" Both of them killed by {causes[0]}."
                : $" All of them killed by {causes[0]}.";

        var counted = Capitalised(Counted(dead.Count));

        if (dead.Count <= 4)
        {
            var names = dead.Select(fallen => fallen.Name).ToList();

            return $"{counted} people have died here: {Join(names)}.{shared} "
                + "Any of them can be looked at by name.";
        }

        var latest = dead.Skip(dead.Count - 3).Select(fallen => fallen.Name).ToList();

        return $"{counted} people have died here.{shared} "
            + $"The last of them were {Join(latest)}. Any of them can be looked at by name.";
    }

    /// <summary>What one of a place's dead was, told to somebody who asked by name.</summary>
    public static string Describe(Fallen fallen) =>
        $"{fallen.Name}, who lasted {Spent(fallen.LifetimeSeconds)} down here before {fallen.Cause} killed them. "
        + (string.Equals(fallen.Carried, "nothing at all", StringComparison.OrdinalIgnoreCase)
            ? "They had nothing on them at the end."
            : $"What they had fell where they fell: {fallen.Carried}. Whether any of it is still on this floor is another question.")
        + $"\n\nThere is no coming back from this, for them or for you. {fallen.Name} got exactly one life here, the same as you have."
        + "\n\nWhatever you have to say to them, this is the place to say it: whisper it, or carve it. It will still be here when you are not.";

    // ---- the ones who got out -------------------------------------------------------

    /// <summary>
    /// What a door says about the people who have gone out through it, or null
    /// where nobody has.
    ///
    /// The mirror of <see cref="Remembrance"/>, and the reason it exists: this
    /// world keeps an exact and permanent account of everyone it has killed and
    /// kept nothing whatever about anyone who beat it. Standing at the door with
    /// no key, the only thing the world could tell you was how many strangers had
    /// died on the way here. That is a true picture of the place and a badly
    /// incomplete one.
    ///
    /// The shortest time is named on purpose. It is the one number here that
    /// reads as a dare.
    /// </summary>
    public static string? WhoGotOut(Location location)
    {
        var survivors = location.Survivors;

        if (survivors.Count == 0)
        {
            return null;
        }

        if (survivors.Count == 1)
        {
            return $"One person has gone out through this door alive: {survivors[0].Name}, "
                + $"after {Spent(survivors[0].LifetimeSeconds)} down here. "
                + $"Look at {survivors[0].Name} and the door will tell you what they took with them.";
        }

        var quickest = survivors.MinBy(survivor => survivor.LifetimeSeconds)!;
        var latest = survivors.Skip(Math.Max(0, survivors.Count - 3))
            .Select(survivor => survivor.Name)
            .ToList();

        return $"{Capitalised(Counted(survivors.Count))} people have gone out through this door alive. "
            + $"The last of them were {Join(latest)}; the quickest was {quickest.Name}, "
            + $"who was down here {Spent(quickest.LifetimeSeconds)}. "
            + "Any of them can be looked at by name.";
    }

    /// <summary>What one of the people who got out was, told to somebody who asked by name.</summary>
    public static string Describe(Survivor survivor) =>
        $"{survivor.Name}, who was down here {Spent(survivor.LifetimeSeconds)} and then opened this door and walked out. "
        + (string.Equals(survivor.Carried, "nothing at all", StringComparison.OrdinalIgnoreCase)
            ? "They took nothing out with them at all."
            : $"What went out with them: {survivor.Carried}. None of it is coming back.")
        + "\n\nThey are not dead and they are not here. There is no third thing to know about them — "
        + "this world does not follow anybody out.";

    // ---- what was said here ------------------------------------------------------

    /// <summary>
    /// What has been left written or spoken in a place, or null where nothing has.
    ///
    /// See <see cref="Words"/> for the person this exists because of.
    /// </summary>
    public static string? WhatWasSaid(Location location)
    {
        var spoken = location.Spoken;

        if (spoken.Count == 0)
        {
            return null;
        }

        var lines = spoken.Select(words => $"  {words.Speaker}: \"{words.Text}\"");

        return (spoken.Count == 1
                ? "Somebody left words in this place.\n"
                : "People have left words in this place.\n")
            + string.Join("\n", lines);
    }

    /// <summary>
    /// Somebody saying something to a place rather than to a person.
    ///
    /// Said out loud to anyone standing there, because they are standing there —
    /// and then kept, because almost nobody ever is. There is no reply and no
    /// delivery: whoever reads it later gets a name and a sentence and nothing
    /// else, and the name is very likely already dead.
    /// </summary>
    public void LeaveWords(Session session, string text)
    {
        var clipped = Words.Clip(text);

        if (clipped.Length == 0)
        {
            session.Write("You do not say anything.");
            return;
        }

        var location = CurrentLocation(session);

        if (location is null)
        {
            session.Write("There is nowhere here to leave that.");
            return;
        }

        location.Keep(new Words(session.Character.Name, clipped, DateTimeOffset.UtcNow));

        BroadcastToLocation(location.Id,
            $"{session.Character.Name} leaves words here: \"{clipped}\"", except: session);

        // Who is actually standing here decides how this reads. Telling somebody
        // "nobody is listening" with another player three feet away is a small
        // lie, and this world's whole case for itself is that it does not tell
        // them.
        var listening = PlayersAt(location.Id).Count(other => other != session);

        session.Write($"You leave it here, where it was meant: \"{clipped}\"\n\n"
            + (listening > 0
                ? "Whoever is standing here heard you. It will still be here when they are not."
                : "Nobody is listening. It will still be here when you are not."));

        Emit(GameEvent.For(session, EventType.Speech, ("mode", "left"), ("text", clipped)));
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
        if (string.IsNullOrWhiteSpace(rawCommand) || session.Character.IsFinished)
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

        // A handler that had already answered and only wanted the room's turn as
        // well has nothing left to add when the room has nothing to say. It is
        // not an unanswered command — it was answered, twice over would simply
        // have been better. See CommandResult.AndTheRoomAsWell.
        if (result.AlreadyAnswered)
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

            case ActionKind.Heal:
                Mend(session, action);
                break;
        }
    }

    /// <summary>
    /// A place putting a character back together. "full" or a number of hit
    /// points; never past the maximum, and never a negative.
    /// </summary>
    private void Mend(Session session, WorldAction action)
    {
        var character = session.Character;
        var missing = character.MaxHitPoints - character.HitPoints;

        var wanted = action.Parameters.Length == 0
            || string.Equals(action.FirstParameter, "full", StringComparison.OrdinalIgnoreCase)
                ? missing
                : action.FirstParameterAsInt;

        var healed = Math.Clamp(wanted, 0, missing);
        character.HitPoints += healed;

        session.Write(healed > 0
            ? $"You are mended. HP: {character.HitPoints}/{character.MaxHitPoints}"
            : $"You had nothing that needed mending. HP: {character.HitPoints}/{character.MaxHitPoints}");

        Emit(GameEvent.For(session, EventType.Healed, ("amount", healed.ToString())));
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
            // What you did, and what it did to them. "You hit!" on its own left a
            // person five exchanges from a kill unable to tell a first blow from a
            // last one, and the whole shape of a fight here is deciding whether to
            // press on or walk out.
            session.Write(monster.WoundLook() is { } hurt
                ? $"You hit the {monster.Name} for {damage}. It is {hurt}."
                : $"You hit the {monster.Name} for {damage}.");

            Emit(GameEvent.For(session, EventType.Combat,
                ("target", monster.Name), ("result", "hit"), ("damage", damage.ToString()),
                ("target_hp", monster.HitPoints.ToString())));

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
                    session.Write($"On the ground: {Manifest(location.Items.Select(item => item.Name))}.");
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

        // The other half of the fight. Without this the record shows only the
        // swings a person took and never what was done back to them, and a death
        // cannot be read afterwards.
        if (character.ReceiveAttack(monsterRoll, monsterDamage))
        {
            Emit(GameEvent.Against(session, EventType.Combat,
                ("attacker", monster.Name), ("target", character.Name), ("result", "hit"),
                ("damage", monsterDamage.ToString()), ("target_hp", character.HitPoints.ToString())));

            session.Write($"You were hit! HP: {character.HitPoints}/{character.MaxHitPoints}");

            if (!character.IsAlive)
            {
                Die(session, $"a {monster.Name}");
            }
            else
            {
                WarnIfDying(session, monster);
            }
        }
        else
        {
            Emit(GameEvent.Against(session, EventType.Combat,
                ("attacker", monster.Name), ("target", character.Name), ("result", "miss"))); 

            session.Write($"The {monster.Name} missed!");
        }
    }

    /// <summary>
    /// Said whenever a character is standing inside the range of a single blow.
    ///
    /// Two true things about this world are worth more here than anywhere else and
    /// neither was ever stated: nothing pursues you out of a room, and a monster
    /// does not mend while you can. Everyone who has died to the troll so far died
    /// with an untouched potion on them and three rooms of open passage at their
    /// back. They were not beaten. They did not know they were allowed to leave.
    ///
    /// The threshold is the creature's own hardest hit, not a fraction of the
    /// character. It used to be a third, and that was wrong in the only case that
    /// matters: a cave troll swings for up to eleven, so a character on seven hit
    /// points was already one roll from dead and heard nothing. Marrow died in
    /// exactly that gap — seven, then nothing, then killed, with the key to the
    /// door already in his pocket. A warning that arrives after the danger does
    /// is not a warning. So it is the monster in front of you that decides when
    /// you are told, because it is the monster in front of you that decides.
    /// </summary>
    private static void WarnIfDying(Session session, Monster monster)
    {
        var character = session.Character;
        var hardestBlow = monster.AttackDice + monster.AttackStrength;

        if (character.HitPoints > hardestBlow)
        {
            return;
        }

        session.Write(
            $"You are badly hurt — the next one the {monster.Name} lands can kill you outright. " +
            "Nothing here follows you out of a room, and nothing here mends itself while you are gone.");

        // The thing most worth saying to somebody about to throw away the only
        // ending this world has that is not dying.
        if (character.HasKey)
        {
            session.Write(
                "You are carrying the key. Whatever you are still down here for, "
                + "it is worth less than walking out with it.");
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

        // How the place reads to this person, now — which is not necessarily how
        // it was authored. See World.DescriptionFor.
        var parts = new List<string> { World.DescriptionFor(session, location) };

        if (location.Monster is { IsAlive: true } monster)
        {
            parts.Add(monster.Description);

            // The keystone of the only tactic this world has. A creature that has
            // been fought before looks it, and is told plainly not to be mending,
            // because the one strong move available to anybody here — hit it, walk
            // back to the fire, sleep, return — is worth nothing if you cannot see
            // that last time counted.
            if (monster.WoundLook() is { } wounded)
            {
                parts.Add($"The {monster.Name} is {wounded}, and carrying every wound "
                    + "it has been given. It has not mended, and it will not.");
            }
        }

        // The dead come before the loot, because the loot is theirs. Then the ones
        // who got out, because that is the same account read the other way. Then
        // what was said, because it was said to both of them.
        if (Remembrance(location) is { } remembered)
        {
            parts.Add(remembered);
        }

        if (WhoGotOut(location) is { } gotOut)
        {
            parts.Add(gotOut);
        }

        if (WhatWasSaid(location) is { } said)
        {
            parts.Add(said);
        }

        if (location.Items.Count > 0)
        {
            parts.Add($"On the ground: {Manifest(location.Items.Select(item => item.Name))}.");
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

        // Going out is final, so it is said plainly before it is done rather than
        // discovered afterwards. Whatever is in your hands goes with you, and
        // whatever you have not done here you will not get back to.
        if (location.IsExit)
        {
            parts.Add(session.Character.HasKey
                ? "You've found the exit! The key you are carrying fits this lock. Open it and you are out — "
                  + "alive, which almost nothing here manages, and not coming back. Whatever you are carrying "
                  + "goes with you; whatever you have left undone stays undone."
                : "You've found the exit! It is locked, and whatever opens it is not in your hands yet. "
                  + "Look at the door itself and it will tell you what it wants and where that is found.");
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
