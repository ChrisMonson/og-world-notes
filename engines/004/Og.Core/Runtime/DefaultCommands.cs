using Og.Core.Model;

namespace Og.Core.Runtime;

/// <summary>
/// The verbs the engine knows. A handler returns Done only when something
/// actually happened; when it recognises the words but can do nothing with them
/// it returns Futile, and the command carries on to the place the player is in.
///
/// Anything that names a thing — an item to take, a creature to hit — looks it up
/// through <see cref="Naming.Strip"/>, so "drink the potion" and "drink potion"
/// are the same sentence. Speech does not: what someone says is passed through
/// exactly as they typed it, articles and all.
/// </summary>
public static class DefaultCommands
{
    public static void Register(CommandRegistry registry)
    {
        RegisterMovement(registry);
        RegisterObservation(registry);
        RegisterInventory(registry);
        RegisterGiving(registry);
        RegisterCombat(registry);
        RegisterRest(registry);
        RegisterSpeech(registry);
    }

    private static void RegisterMovement(CommandRegistry registry)
    {
        Move(Direction.North, "go north", "north", "n");
        Move(Direction.East, "go east", "east", "e");
        Move(Direction.South, "go south", "south", "s");
        Move(Direction.West, "go west", "west", "w");

        void Move(Direction direction, params string[] forms) =>
            registry.RegisterExact((engine, session, _) =>
            {
                var location = engine.CurrentLocation(session);

                if (location is null)
                {
                    return CommandResult.NotHandled;
                }

                var target = location.Exit(direction);

                // A wall is worth knowing about, but the room may still answer.
                if (target <= 0)
                {
                    engine.Emit(GameEvent.For(session, EventType.BlockedMovement,
                        ("direction", direction.ToString().ToLowerInvariant())));

                    return CommandResult.Futile("You can't go that way.");
                }

                engine.MoveTo(session, target);
                return CommandResult.Done();
            }, forms);
    }

    /// <summary>
    /// The words people use for a place's dead all together rather than for one
    /// of them by name. Found by playing: a character was told plainly that Sable
    /// had died on that floor and the next thing typed was "look at the bodies",
    /// which the world answered with "You don't see a bodies here."
    /// </summary>
    private static bool IsAskingAboutTheDead(string wanted) =>
        wanted is "bodies" or "body" or "dead" or "dead body" or "dead bodies"
            or "dead people" or "corpse" or "corpses" or "remains" or "fallen"
            or "grave" or "graves" or "who died" or "who has died";

    /// <summary>The words people use for what has been left written in a place.</summary>
    private static bool IsAskingAboutWords(string wanted) =>
        wanted is "words" or "writing" or "writings" or "message" or "messages"
            or "inscription" or "inscriptions" or "note" or "notes" or "carving"
            or "carvings" or "graffiti" or "what was said";

    /// <summary>
    /// The words people use for the people who got out. The other half of asking
    /// a place who has been here — and until now the half it could not answer.
    /// </summary>
    private static bool IsAskingAboutTheEscaped(string wanted) =>
        wanted is "survivors" or "survivor" or "escaped" or "the escaped"
            or "who got out" or "who has got out" or "who escaped" or "who left"
            or "who made it" or "roll" or "the roll" or "names" or "the names"
            or "record" or "the record" or "tally";

    /// <summary>
    /// A place's dead, asked about collectively. Where nobody has died the answer
    /// is Futile rather than Done, so the room still gets its turn: a floor with
    /// no dead on it may well have something else to say about being searched.
    /// </summary>
    private static CommandResult TheDeadHere(GameEngine engine, Session session, string argument)
    {
        var wanted = Naming.Strip(argument);

        if (IsAskingAboutWords(wanted))
        {
            if (engine.CurrentLocation(session) is { } here
                && GameEngine.WhatWasSaid(here) is { } said)
            {
                session.Write(said);
                return CommandResult.Done();
            }

            return CommandResult.Futile("Nobody has left anything here to read.");
        }

        if (IsAskingAboutTheEscaped(wanted))
        {
            if (engine.CurrentLocation(session) is { } doorway
                && GameEngine.WhoGotOut(doorway) is { } gotOut)
            {
                session.Write(gotOut);
                return CommandResult.Done();
            }

            return CommandResult.Futile("Nobody has ever got out from here.");
        }

        if (!IsAskingAboutTheDead(wanted))
        {
            return CommandResult.NotHandled;
        }

        if (engine.CurrentLocation(session) is { } location
            && GameEngine.Remembrance(location) is { } remembered)
        {
            session.Write(remembered);
            return CommandResult.Done();
        }

        return CommandResult.Futile("Nobody has died here. Not yet.");
    }

    private static void RegisterObservation(CommandRegistry registry)
    {
        registry.RegisterExact((engine, session, _) =>
        {
            session.Write(engine.DescribeCurrentLocation(session));
            return CommandResult.Done();
        }, "look", "look around", "explore", "investigate", "search", "l");

        registry.RegisterExact((_, session, _) =>
        {
            var character = session.Character;
            session.Write($"HP: {character.HitPoints}/{character.MaxHitPoints}");
            return CommandResult.Done();
        }, "health", "hit points", "hp");

        registry.RegisterExact((engine, session, _) =>
        {
            var names = engine.Sessions.Select(other => other.Character.Name).Order();
            session.Write($"Here in this world: {string.Join(", ", names)}");
            return CommandResult.Done();
        }, "who", "players");

        // Asking a place who got out of it. Said on its own, because "who got
        // out" is a whole sentence and nobody prefixes it with a verb. At the
        // door this is the only good news the world has ever been able to give
        // anybody, and before this it could not be asked for at all.
        registry.RegisterExact((engine, session, _) =>
        {
            if (engine.CurrentLocation(session) is { } location
                && GameEngine.WhoGotOut(location) is { } gotOut)
            {
                session.Write(gotOut);
                return CommandResult.Done();
            }

            return CommandResult.Futile("Nobody has ever got out from here.");
        }, "who got out", "who has got out", "who escaped", "who made it", "who survived",
           "survivors", "the survivors", "roll", "the roll");

        // Searching, counting or looting "the bodies" is only ever about the
        // dead. Anything else said with those verbs is handed straight back so
        // the room answers it exactly as it did before.
        registry.RegisterPrefix(TheDeadHere, "search", "check", "count", "read", "loot", "look for");

        // Looking at a thing means looking at any thing you can see: what you
        // carry, what is lying here, whatever is standing in front of you, and
        // whoever died on this floor. Only when none of those is what was meant
        // does the room get its turn.
        registry.RegisterPrefix((engine, session, argument) =>
        {
            var wanted = Naming.Strip(argument);

            if (wanted.Length == 0)
            {
                return CommandResult.Futile("Look at what?");
            }

            if (session.Character.FindItemOrEquipped(wanted) is { } carried)
            {
                session.Write(carried.Description);
                return CommandResult.Done();
            }

            var location = engine.CurrentLocation(session);

            if (location?.Items.FirstOrDefault(item => item.Matches(wanted)) is { } onTheGround)
            {
                session.Write(onTheGround.Description);
                return CommandResult.Done();
            }

            // A creature is the one thing here the engine always describes worse
            // than the room does. The monster definition is a line of stats with
            // a sentence attached; the passage knows the troll is slow, does not
            // mend, wears a key and a dead man's whistle, and is hungry — which
            // is the entire tactical content of this world. So the engine says
            // what it can see and hands the sentence on. See
            // CommandResult.AndTheRoomAsWell.
            if (location?.Monster is { IsAlive: true } monster && monster.Matches(wanted))
            {
                session.Write(monster.Description);
                return CommandResult.AndTheRoomAsWell();
            }

            var other = engine.PlayersAt(session.CurrentLocationId).FirstOrDefault(candidate =>
                candidate != session &&
                string.Equals(candidate.Character.Name, wanted, StringComparison.OrdinalIgnoreCase));

            if (other is not null)
            {
                var them = other.Character;
                session.Write($"{them.Name}, carrying {(them.Weapon?.Name ?? "nothing")} and wearing {(them.Armor?.Name ?? "nothing")}.");
                return CommandResult.Done();
            }

            // The living are asked about before the dead, so a new arrival of the
            // same name is who you mean. After them: whoever this floor kept.
            if (location?.FindDead(wanted) is { } fallen)
            {
                session.Write(GameEngine.Describe(fallen));
                return CommandResult.Done();
            }

            // And after them, whoever this door let out. A name is only ever on
            // one of the two lists.
            if (location?.FindSurvivor(wanted) is { } survivor)
            {
                session.Write(GameEngine.Describe(survivor));
                return CommandResult.Done();
            }

            if (IsAskingAboutTheDead(wanted) || IsAskingAboutWords(wanted)
                || IsAskingAboutTheEscaped(wanted))
            {
                return TheDeadHere(engine, session, wanted);
            }

            return CommandResult.Futile($"You don't see a {wanted} here.");
        }, "describe", "look at", "examine", "inspect");
    }

    private static void RegisterInventory(CommandRegistry registry)
    {
        registry.RegisterExact((_, session, _) =>
        {
            var character = session.Character;

            session.Write($"Weapon: {character.Weapon?.Name ?? "none equipped"}");
            session.Write($"Armor: {character.Armor?.Name ?? "none equipped"}");
            session.Write(character.Inventory.Count == 0
                ? "Inventory: empty"
                : "Inventory: " + GameEngine.Manifest(character.Inventory.Select(item => item.Name)));

            return CommandResult.Done();
        }, "inventory", "items", "i");

        registry.RegisterPrefix((_, session, argument) =>
        {
            var character = session.Character;
            var wanted = Naming.Strip(argument);
            var item = character.FindItem(wanted);

            if (item is null)
            {
                return CommandResult.Futile($"You aren't carrying a {wanted}.");
            }

            switch (item.Kind)
            {
                case ItemKind.Weapon:
                    if (character.Weapon is { } oldWeapon) character.Inventory.Add(oldWeapon);
                    character.Weapon = item;
                    break;

                case ItemKind.Armor:
                    if (character.Armor is { } oldArmor) character.Inventory.Add(oldArmor);
                    character.Armor = item;
                    break;

                default:
                    return CommandResult.Futile($"You can't equip the {item.Name}.");
            }

            character.Inventory.Remove(item);
            session.Write($"You equipped the {item.Name}.");
            return CommandResult.Done();
        }, "equip", "wield", "wear");

        registry.RegisterPrefix((_, session, argument) =>
        {
            var character = session.Character;
            var wanted = Naming.Strip(argument);
            var item = character.FindItem(wanted);

            if (item is null)
            {
                return CommandResult.Futile($"You aren't carrying a {wanted}.");
            }

            if (item.Kind != ItemKind.Consumable)
            {
                // The room may well know what this thing is for, even though the
                // engine does not.
                return CommandResult.Futile($"Nothing happens when you use the {item.Name}.");
            }

            var healed = Math.Min(item.Definition.HealAmount, character.MaxHitPoints - character.HitPoints);
            character.HitPoints += healed;

            if (item.Definition.ConsumedOnUse)
            {
                character.Inventory.Remove(item);
            }

            session.Write($"You used the {item.Name}. HP: {character.HitPoints}/{character.MaxHitPoints}");
            return CommandResult.Done();
        }, "use", "drink", "eat");

        registry.RegisterPrefix((engine, session, argument) =>
        {
            var location = engine.CurrentLocation(session);
            var wanted = Naming.Strip(argument);

            if (location is null)
            {
                return CommandResult.Futile($"There is no {wanted} here to take.");
            }

            // "take all" is how people empty a floor. Worth knowing, because the
            // floor is where the dead leave everything they had.
            if (string.Equals(wanted, "all", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(wanted, "everything", StringComparison.OrdinalIgnoreCase))
            {
                if (location.Items.Count == 0)
                {
                    return CommandResult.Futile("There is nothing here to take.");
                }

                foreach (var lying in location.Items.ToArray())
                {
                    location.Items.Remove(lying);
                    session.Character.Inventory.Add(lying);
                    session.Write($"You take the {lying.Name}.");
                    engine.BroadcastToLocation(location.Id, $"{session.Character.Name} takes the {lying.Name}.", except: session);
                    engine.Emit(GameEvent.For(session, EventType.ItemPickedUp, ("item", lying.Name)));
                }

                return CommandResult.Done();
            }

            var item = location.Items.FirstOrDefault(candidate => candidate.Matches(wanted));

            if (item is null)
            {
                return CommandResult.Futile($"There is no {wanted} here to take.");
            }

            location.Items.Remove(item);
            session.Character.Inventory.Add(item);
            session.Write($"You take the {item.Name}.");
            engine.BroadcastToLocation(location.Id, $"{session.Character.Name} takes the {item.Name}.", except: session);
            engine.Emit(GameEvent.For(session, EventType.ItemPickedUp, ("item", item.Name)));
            return CommandResult.Done();
        }, "take", "pick up", "get", "grab");

        // Putting a thing down, including the thing in your hand.
        //
        // This used to look only in the pack, so "drop dagger" while holding the
        // dagger answered "You aren't carrying a dagger." — which is both false
        // and, to anyone standing there with it, faintly insulting. A weapon or a
        // jacket you are wearing is still something you own and can set down, so
        // dropping it takes it off you first.
        registry.RegisterPrefix((engine, session, argument) =>
        {
            var location = engine.CurrentLocation(session);
            var character = session.Character;
            var wanted = Naming.Strip(argument);
            var item = character.FindItemOrEquipped(wanted);

            if (location is null || item is null)
            {
                return CommandResult.Futile($"You aren't carrying a {wanted}.");
            }

            if (character.Weapon == item)
            {
                character.Weapon = null;
            }
            else if (character.Armor == item)
            {
                character.Armor = null;
            }
            else
            {
                character.Inventory.Remove(item);
            }

            location.Items.Add(item);
            session.Write($"You drop the {item.Name}.");
            engine.BroadcastToLocation(location.Id, $"{character.Name} drops the {item.Name}.", except: session);
            engine.Emit(GameEvent.For(session, EventType.ItemDropped, ("item", item.Name)));
            return CommandResult.Done();
        }, "drop", "put down", "leave");
    }

    /// <summary>
    /// Holding a thing out to something, or throwing it at it.
    ///
    /// This is the most-asked-for verb in this world's whole history and until
    /// now it did not exist. Three separate people — Tanril, Kaleb, Krasus —
    /// tried to throw a bone, and all three were told "Nothing happens", which
    /// was true and useless. What they were reaching for is the obvious one: a
    /// world where an object can be used on something, rather than only eaten or
    /// worn or swung.
    ///
    /// The engine deliberately does not decide what throwing achieves. It checks
    /// the one thing it is qualified to check — that you have the thing in your
    /// hands — and then returns Futile, which hands the sentence on to the room.
    /// A place that has something to say about being given a bone says it; a
    /// place that has not gives an honest shrug instead of a lie. Nothing is
    /// consumed here, because the interaction that answers has to be able to see
    /// the item still in your inventory in order to know you had it.
    /// </summary>
    private static void RegisterGiving(CommandRegistry registry)
    {
        registry.RegisterExact((_, _, _) => CommandResult.Futile("Throw what?"),
            "throw", "toss", "hurl", "fling", "lob", "give", "offer");

        registry.RegisterPrefix((_, session, argument) =>
        {
            var wanted = NamedThing(argument);

            if (wanted.Length == 0)
            {
                return CommandResult.Futile("Throw what?");
            }

            return session.Character.FindItemOrEquipped(wanted) is { } item
                ? CommandResult.Futile(
                    $"You could throw the {item.Name}, but nothing here would care where it landed.")
                : CommandResult.Futile($"You aren't carrying a {wanted}.");
        }, "throw", "toss", "hurl", "fling", "lob", "give", "offer");
    }

    /// <summary>
    /// The thing being thrown, out of a sentence that may also name what it is
    /// being thrown at. "throw the bone to the troll" is about a bone.
    /// </summary>
    private static string NamedThing(string argument)
    {
        var spoken = Naming.Strip(argument);

        foreach (var joiner in (string[])[" at ", " to ", " toward ", " towards ", " into "])
        {
            var split = spoken.IndexOf(joiner, StringComparison.OrdinalIgnoreCase);

            if (split > 0)
            {
                return Naming.Strip(spoken[..split]);
            }
        }

        return spoken;
    }

    private static void RegisterCombat(CommandRegistry registry)
    {
        CommandResult Strike(GameEngine engine, Session session, string argument)
        {
            var location = engine.CurrentLocation(session);

            if (location?.Monster is not { IsAlive: true } monster)
            {
                return CommandResult.Futile("There is nothing to attack here.");
            }

            // Naming what is in front of you is the commonest way to say it.
            var wanted = Naming.Strip(argument);

            if (wanted.Length > 0 && !monster.Matches(wanted))
            {
                return CommandResult.Futile($"There is no {wanted} here to attack.");
            }

            engine.Attack(session);
            return CommandResult.Done();
        }

        registry.RegisterExact(Strike, "attack", "fight", "kill", "stab");
        registry.RegisterPrefix(Strike, "attack", "fight", "kill", "stab", "attack the", "kill the");

        RegisterLeaving(registry);
    }

    /// <summary>
    /// The way out. Nobody standing in front of a locked door types "exit" first:
    /// they open it, unlock it, turn the key in it. The first person ever to reach
    /// this world's door with the key in hand tried six such sentences, was told
    /// nothing by any of them, and walked away from a door that would have opened.
    /// So every reasonable way of saying it is the same command now, and the
    /// refusals say what is actually wrong rather than that nothing happened.
    ///
    /// What lies past it is <see cref="GameEngine.Escape"/>: the end of that life,
    /// alive.
    /// </summary>
    private static void RegisterLeaving(CommandRegistry registry)
    {
        CommandResult Leave(GameEngine engine, Session session, string _)
        {
            var location = engine.CurrentLocation(session);

            if (location is null || !location.IsExit)
            {
                return CommandResult.Futile("There is no way out here. Sadly, this is not the exit.");
            }

            if (!session.Character.HasKey)
            {
                return CommandResult.Futile(
                    "It is locked, and there is a keyhole in it. Nothing you are carrying fits.");
            }

            engine.Escape(session);
            return CommandResult.Done();
        }

        registry.RegisterExact(Leave,
            "exit", "escape", "leave", "out", "get out", "go out", "way out",
            "open door", "open the door", "open it", "open gate", "open the gate",
            "unlock door", "unlock the door", "unlock it", "unlock",
            "turn key", "turn the key", "use key", "use the key", "use door", "use the door",
            "insert key", "insert the key", "put key in lock", "put the key in the lock",
            "key", "unlock door with key", "unlock the door with the key",
            "open door with key", "open the door with the key",
            "go through door", "go through the door", "enter door", "enter the door",
            "push door", "push the door", "pull door", "pull the door", "knock");

        // Anything of the shape "<verb> the heavy iron door" — but only where
        // there is a door to mean. Elsewhere the room gets its turn untouched.
        registry.RegisterPrefix((engine, session, argument) =>
        {
            var wanted = Naming.Strip(argument);
            var location = engine.CurrentLocation(session);

            if (location is not { IsExit: true })
            {
                return CommandResult.NotHandled;
            }

            return wanted.Contains("door", StringComparison.OrdinalIgnoreCase)
                || wanted.Contains("key", StringComparison.OrdinalIgnoreCase)
                || wanted.Contains("lock", StringComparison.OrdinalIgnoreCase)
                || wanted.Contains("gate", StringComparison.OrdinalIgnoreCase)
                    ? Leave(engine, session, argument)
                    : CommandResult.NotHandled;
        }, "open", "unlock", "turn", "push", "pull", "pry", "force", "enter", "go through", "try");
    }

    /// <summary>
    /// Wanting to stop and mend is one of the first things anyone asks a world
    /// for. The engine does not decide where that is allowed — a place does, with
    /// a heal action — but it recognises the asking everywhere, so the answer is
    /// "not here" rather than silence.
    /// </summary>
    private static void RegisterRest(CommandRegistry registry)
    {
        registry.RegisterExact((_, _, _) => CommandResult.Futile(
                "Not here. You would need somewhere out of the wind, with the makings of a fire."),
            "rest", "sleep", "nap", "recover", "heal", "sit", "sit down", "lie down", "camp", "make camp");
    }

    private static void RegisterSpeech(CommandRegistry registry)
    {
        // Speech is recorded and delivered. The world itself never answers.
        registry.RegisterPrefix((engine, session, argument) =>
        {
            engine.BroadcastToLocation(session.CurrentLocationId,
                $"{session.Character.Name} says: {argument}", except: session);
            session.Write($"You say: {argument}");
            engine.Emit(GameEvent.For(session, EventType.Speech, ("mode", "say"), ("text", argument)));
            return CommandResult.Done();
        }, "say", "speak");

        registry.RegisterPrefix((engine, session, argument) =>
        {
            engine.BroadcastToWorld($"{session.Character.Name} yells: {argument}", except: session);
            session.Write($"You yell: {argument}");
            engine.Emit(GameEvent.For(session, EventType.Speech, ("mode", "yell"), ("text", argument)));
            return CommandResult.Done();
        }, "yell", "shout");

        // Whispering to somebody who is not here.
        //
        // Malcolm played a dead stranger's whistle over the place they died and
        // then typed: "whisper Thank you M R, whoever you where. I'm sorry for
        // what became of you, but am grateful for what you left behind." The
        // world answered "There is no Thank here." — because the first word of
        // anything whispered is read as a player's name, and no player was on.
        //
        // He was not addressing a player. Almost nobody here ever is: this world
        // is empty most hours of the day and the only other people in it are
        // dead. So a whisper with nobody to receive it is not a mistake to be
        // refused. It is left where it was said, for whoever stands there next.
        registry.RegisterPrefix((engine, session, argument) =>
        {
            var split = argument.IndexOf(' ');

            if (split > 0)
            {
                var targetName = argument[..split];
                var text = argument[(split + 1)..];
                var target = engine.Sessions.FirstOrDefault(other =>
                    string.Equals(other.Character.Name, targetName, StringComparison.OrdinalIgnoreCase));

                if (target is not null)
                {
                    target.Write($"{session.Character.Name} whispers: {text}");
                    session.Write($"You whisper to {target.Character.Name}: {text}");
                    engine.Emit(GameEvent.For(session, EventType.Speech,
                        ("mode", "whisper"), ("target", target.CharacterId), ("text", text)));
                    return CommandResult.Done();
                }
            }

            // Nobody of that name is alive and listening — so the whole of it,
            // first word included, was meant for the place.
            engine.LeaveWords(session, argument);
            return CommandResult.Done();
        }, "whisper", "tell");

        // Saying it on purpose to the place rather than to a person.
        registry.RegisterPrefix((engine, session, argument) =>
        {
            engine.LeaveWords(session, argument);
            return CommandResult.Done();
        }, "carve", "scratch", "inscribe", "write", "engrave");

        registry.RegisterExact((_, _, _) => CommandResult.Futile(
                "You could leave something here for whoever comes next. Carve it: carve <whatever you have to say>."),
            "carve", "scratch", "inscribe", "write", "engrave");

        registry.RegisterPrefix((engine, session, argument) =>
        {
            session.Write("You pray.");
            engine.Emit(GameEvent.For(session, EventType.Speech, ("mode", "pray"), ("text", argument)));
            return CommandResult.Done();
        }, "pray");

        registry.RegisterExact((engine, session, _) =>
        {
            session.Write("You pray.");
            engine.Emit(GameEvent.For(session, EventType.Speech, ("mode", "pray"), ("text", "")));
            return CommandResult.Done();
        }, "pray");
    }
}
