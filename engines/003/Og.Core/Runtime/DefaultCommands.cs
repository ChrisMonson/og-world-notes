using Og.Core.Model;

namespace Og.Core.Runtime;

/// <summary>
/// The verbs the engine knows. A handler returns Done only when something
/// actually happened; when it recognises the words but can do nothing with them
/// it returns Futile, and the command carries on to the place the player is in.
/// </summary>
public static class DefaultCommands
{
    private static readonly (string Word, Direction Direction)[] DirectionWords =
    [
        ("north", Direction.North),
        ("east", Direction.East),
        ("south", Direction.South),
        ("west", Direction.West)
    ];

    /// <summary>
    /// Words people put between a verb and what they mean. Stripped, in order,
    /// from the front of whatever a verb was handed.
    ///
    /// "take the chisel" is how a person says it and "take chisel" is how a
    /// parser wants it, and until this was applied to the object of a verb
    /// rather than only to directions, the article won: the world looked for a
    /// thing called "the chisel", did not find one, and answered "there is no
    /// the chisel here to take" to somebody who was standing next to it.
    /// </summary>
    private static readonly string[] Fillers =
    [
        "to", "the", "towards", "toward", "into", "my", "a", "an",
        "his", "her", "its", "that", "this", "some"
    ];

    /// <summary>Ways of saying "all of it" when there is a pile on the floor.</summary>
    private static readonly HashSet<string> Everything = new(StringComparer.OrdinalIgnoreCase)
    {
        "all", "everything", "all of it", "all of them", "it all", "all items",
        "everything here", "lot", "them all", "whats here", "what is here"
    };

    /// <summary>
    /// Remembered on the character once the door has told them what going through
    /// it costs. Character-scoped, so it dies with them and every new life is
    /// warned afresh.
    /// </summary>
    private const string WarnedAtTheDoor = "warned-at-the-door";

    public static void Register(CommandRegistry registry)
    {
        RegisterMovement(registry);
        RegisterObservation(registry);
        RegisterInventory(registry);
        RegisterCombat(registry);
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

                    return CommandResult.Futile("You can't go that way. " + Ways(location));
                }

                engine.MoveTo(session, target);
                return CommandResult.Done();
            }, forms);

        // "go soith" is not a mystery to anyone but a parser. Whatever follows a
        // going-word is measured against the four directions, and the nearest wins
        // so long as it is nearer than all the others. When nothing is close, the
        // refusal at least says which ways are open.
        registry.RegisterPrefix((engine, session, argument) =>
        {
            var location = engine.CurrentLocation(session);

            if (location is null)
            {
                return CommandResult.NotHandled;
            }

            var direction = NearestDirection(StripFillers(TriggerMatcher.Normalize(argument)));

            if (direction is null)
            {
                return CommandResult.Futile(Ways(location));
            }

            var target = location.Exit(direction.Value);

            if (target <= 0)
            {
                engine.Emit(GameEvent.For(session, EventType.BlockedMovement,
                    ("direction", direction.Value.ToString().ToLowerInvariant())));

                return CommandResult.Futile("You can't go that way. " + Ways(location));
            }

            engine.MoveTo(session, target);
            return CommandResult.Done();
        }, "go", "walk", "move", "head", "travel", "run");

        // Retracing. The way you came was already remembered — running away has
        // used it since the troll got its first refusal — but the plainest word
        // for it reached nothing at all. Somebody stood at the door and typed
        // "Back", which is not a compass direction and is not a plea, and the
        // world had no answer for it.
        //
        // Unlike fleeing, this says nothing about a fight and does not settle for
        // some other doorway when the way back is shut: it either retraces or it
        // admits it cannot.
        registry.RegisterExact((engine, session, _) =>
        {
            var location = engine.CurrentLocation(session);

            if (location is null)
            {
                return CommandResult.NotHandled;
            }

            var previous = session.PreviousLocationId;

            if (previous == 0
                || previous == session.CurrentLocationId
                || engine.World.FindLocation(previous) is null)
            {
                return CommandResult.Futile(
                    "You have not come from anywhere yet — this is where you started. " + Ways(location));
            }

            engine.MoveTo(session, previous);
            return CommandResult.Done();
        }, "back", "go back", "turn back", "return", "the way i came", "go back the way i came",
           "backtrack", "retrace", "retrace my steps");

        // Nobody standing in front of a troll should have to work out the compass.
        // Running takes you back the way you came if that way is open, and out by
        // whatever door there is otherwise. Nothing swings at your back as you go:
        // this is the same retreat as walking out, said the way people say it.
        //
        // "run" is registered here as an exact word and as a going-prefix above.
        // Exact is checked first, so bare "run" flees and "run north" walks. Do
        // not remove it from either list: at three letters it is below the typo
        // corrector's floor, so a "run" that matches nothing does not fall back
        // to anything — it falls into silence.
        registry.RegisterExact((engine, session, _) =>
        {
            var location = engine.CurrentLocation(session);

            if (location is null)
            {
                return CommandResult.NotHandled;
            }

            var open = location.OpenDirections().ToList();

            if (open.Count == 0)
            {
                return CommandResult.Futile("There is nowhere to run to.");
            }

            var back = open.FirstOrDefault(
                direction => location.Exit(direction) == session.PreviousLocationId,
                open[0]);

            session.Write(location.Monster is { IsAlive: true } monster
                ? $"You break off and go. The {monster.Name} does not follow."
                : "You go, quickly.");

            engine.MoveTo(session, location.Exit(back));
            return CommandResult.Done();
        }, "flee", "run", "run away", "run for it", "retreat", "back off", "get out",
           "get out of here", "back away", "withdraw", "leg it");
    }

    private static string StripFillers(string text)
    {
        text = text.Trim();

        foreach (var filler in Fillers)
        {
            if (text.StartsWith(filler + " ", StringComparison.OrdinalIgnoreCase))
            {
                text = text[(filler.Length + 1)..].TrimStart();
            }
        }

        return text;
    }

    private static string Ways(Location location)
    {
        var open = location.OpenDirections()
            .Select(direction => direction.ToString().ToLowerInvariant())
            .ToList();

        return open.Count == 0
            ? "There is nowhere to go from here."
            : $"You can go {string.Join(", ", open)}.";
    }

    private static Direction? NearestDirection(string word)
    {
        if (word.Length == 0)
        {
            return null;
        }

        foreach (var (candidate, direction) in DirectionWords)
        {
            if (word == candidate || (word.Length == 1 && candidate[0] == word[0]))
            {
                return direction;
            }
        }

        var nearest = TriggerMatcher.Nearest(
            word,
            DirectionWords.Select(entry => entry.Word),
            word.Length <= 4 ? 1 : 2);

        return nearest is null
            ? null
            : DirectionWords.First(entry => entry.Word == nearest).Direction;
    }

    private static void RegisterObservation(CommandRegistry registry)
    {
        // The world is a bare prompt otherwise. This says what the engine itself
        // understands, and then says that the engine is not the whole of it.
        registry.RegisterExact((_, session, _) =>
        {
            session.Write("To move: north, south, east, west, back. To leave a fight: flee.");
            session.Write("To see: look, look <direction>, search <thing>, exits, examine <thing>, inventory, health, who.");
            session.Write("To act: take <thing>, take all, drop <thing>, give <thing>, equip <thing>, use <thing>, rest, attack.");
            session.Write("To speak: say <words>, yell <words>, whisper <name> <words>, talk to <someone>.");
            session.Write("To finish: exit, at the door, holding a key. That is the end of this character, and the door says so before it takes you.");
            session.Write("When none of those fit, write plainly what you mean to do. Places understand more than this list does.");
            return CommandResult.Done();
        }, "help", "commands", "verbs", "how do i play", "what do i do", "how does this work");

        registry.RegisterExact((engine, session, _) =>
        {
            session.Write(engine.DescribeCurrentLocation(session));
            return CommandResult.Done();
        }, "look", "look around", "where am i", "where is this", "l");

        // Searching is not looking. It reaches the room afterwards, so a place with
        // something buried in it gets its chance to say so — and when the room has
        // no answer for a bare "search", the refusal says what kind of thing would
        // work instead of claiming there is nothing here.
        //
        // This one is proven. Edward typed "Search" at the bottom of the sunken
        // stair, got this back, and thirty seconds later typed "Search body" —
        // which the room did answer. A refusal that teaches is not a failure.
        registry.RegisterExact((engine, session, _) =>
        {
            session.Write(engine.DescribeCurrentLocation(session));
            return CommandResult.Futile(
                "Turning things over at random turns up nothing. A search wants an object: "
                + "name something this place has already mentioned, and go through that.");
        }, "search", "search the room", "search around", "explore", "investigate", "look closer", "look again");

        registry.RegisterExact((engine, session, _) =>
        {
            var location = engine.CurrentLocation(session);

            if (location is null)
            {
                return CommandResult.NotHandled;
            }

            session.Write(Ways(location));

            if (location.IsExit)
            {
                session.Write("This is the way out, for anyone holding a key — and the end of this life.");
            }

            return CommandResult.Done();
        }, "exits", "directions", "which way", "where can i go", "where do i go", "find exit", "where is the exit");

        // Weariness is the commonest thing anyone asks this world for. Most places
        // cannot answer it, and should say so rather than say nothing — but the
        // refusal still carries on to the room, in case the room can.
        registry.RegisterExact((_, _, _) =>
            CommandResult.Futile("There is nowhere to rest here. Not every place will have you."),
            "rest", "sleep", "nap", "sit", "sit down", "lie down", "make camp", "take a rest", "recover");

        registry.RegisterExact((_, _, _) =>
            CommandResult.Futile("Nothing here closes wounds. A potion might, if you are carrying one."),
            "heal", "heal me", "cure me", "bandage");

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

        // What is lying at your feet can be looked at before it is picked up — and
        // so can whatever is standing in the room with you. Someone who types
        // "look at goblin" with a goblin in front of them should not be told they
        // aren't carrying one: every creature in this world was written a
        // description, and until now there was no word that reached it.
        registry.RegisterPrefix((engine, session, rawArgument) =>
        {
            var argument = StripFillers(rawArgument);
            var location = engine.CurrentLocation(session);

            var found = session.Character.FindItemOrEquipped(argument)
                        ?? location?.Items.FirstOrDefault(candidate => candidate.Matches(argument));

            if (found is not null)
            {
                session.Write(found.Description);
                return CommandResult.Done();
            }

            if (location?.Monster is { IsAlive: true } monster)
            {
                if (monster.Matches(argument))
                {
                    session.Write(monster.Description);
                    session.Write($"HP: {monster.HitPoints}/{monster.MaxHitPoints}");
                    return CommandResult.Done();
                }

                // Looking at a thing a creature is holding. The room has usually
                // just mentioned it by name, so denying it exists is the one
                // answer that cannot be right.
                if (monster.FindItem(argument) is { } held)
                {
                    session.Write(held.Description);
                    session.Write($"The {monster.Name} has it.");
                    return CommandResult.Done();
                }
            }

            return CommandResult.Futile($"You aren't carrying a {argument}, and there is no {argument} here.");
        }, "describe", "look at", "examine", "inspect");

        // Looking a way.
        //
        // Ravick stood in the hollow he was born in, certain there had been a
        // path south to a village and to children who needed feeding, and typed
        // "Look south". The world returned an empty string. Not a refusal —
        // nothing, the same silence "Throw bone" gave the life before his, and
        // the worst answer this place is capable of.
        //
        // A direction is the one thing every location in this world knows for
        // certain about itself, so looking down one is always answerable. An
        // open way says it is open and no more than that: what is at the far end
        // is the far end's business, and there is no finding out but walking it.
        // A wall returns Futile and carries on into the room, because the edge
        // of the world is exactly the sort of thing a place may have been
        // written something to say about, and if it has, it should get to say it
        // instead of this.
        //
        // Anything that is not a direction is handed on to examine, so "look
        // spoon" and "look goblin" now reach what "look at the spoon" always
        // did. Things that are really here are checked first and win: a real
        // object in the room beats a fuzzy guess at the compass, so nobody
        // holding a beast gets told about the east.
        CommandResult LookAWay(GameEngine engine, Session session, string rawArgument)
        {
            var location = engine.CurrentLocation(session);
            var argument = StripFillers(TriggerMatcher.Normalize(rawArgument));

            if (argument.Length == 0)
            {
                session.Write(engine.DescribeCurrentLocation(session));
                return CommandResult.Done();
            }

            var isSomethingHere =
                session.Character.FindItemOrEquipped(argument) is not null
                || location?.Items.Any(candidate => candidate.Matches(argument)) == true
                || (location?.Monster is { IsAlive: true } creature
                    && (creature.Matches(argument) || creature.FindItem(argument) is not null));

            if (location is not null && !isSomethingHere && !argument.Contains(' ')
                && NearestDirection(argument) is { } direction)
            {
                var word = direction.ToString().ToLowerInvariant();

                if (location.Exit(direction) > 0)
                {
                    session.Write(
                        $"You look {word}. The way is open, and what is at the end of it does not "
                        + "carry this far; there is no finding that out but walking it.");

                    return CommandResult.Done();
                }

                return CommandResult.Futile(
                    $"You look {word}. There is no way out of here in that direction — not a shut "
                    + $"one, none: whatever this place is, it does not go {word}. " + Ways(location));
            }

            // Not a direction. Say it the way examine already understands it.
            return registry.Execute(engine, session, "look at " + argument, "look at " + rawArgument.Trim());
        }

        registry.RegisterPrefix(LookAWay, "look", "peer", "glance", "stare");
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
                : "Inventory: " + string.Join(", ", character.Inventory.Select(item => item.Name)));

            return CommandResult.Done();
        }, "inventory", "items", "i");

        registry.RegisterPrefix((_, session, rawArgument) =>
        {
            var argument = StripFillers(rawArgument);
            var character = session.Character;
            var item = character.FindItem(argument);

            if (item is null)
            {
                return CommandResult.Futile($"You aren't carrying a {argument}.");
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
                    // Refusing is right — but a bare refusal invites the next six
                    // phrasings of the same idea. Somebody in front of the troll
                    // tried to equip a mason's chisel, which is a foot of iron and
                    // not an unreasonable thing to want to hit something with.
                    return CommandResult.Futile(
                        $"The {item.Name} is not a weapon and not armour, and it will not go in your hands "
                        + "as either. It is carried, and it is for whatever it is for.");
            }

            character.Inventory.Remove(item);
            session.Write($"You equipped the {item.Name}.");
            return CommandResult.Done();
        }, "equip", "wield", "wear", "put on");

        registry.RegisterPrefix((_, session, rawArgument) =>
        {
            var argument = StripFillers(rawArgument);
            var character = session.Character;
            var item = character.FindItem(argument);

            if (item is null)
            {
                return CommandResult.Futile($"You aren't carrying a {argument}.");
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

        // Handing a thing over. The engine has no giving in it and nobody in it to
        // give to; this exists so the sentence is a recognised shape, checked
        // against what you are actually holding, and then carried on into the
        // room, where someone may be sitting who will take the thing. Somebody
        // typed "give bone" three times at a woman who trades and was told
        // "Nothing happens."
        registry.RegisterPrefix((_, session, rawArgument) =>
        {
            var argument = StripFillers(rawArgument);

            if (session.Character.FindItemOrEquipped(argument) is { } item)
            {
                return CommandResult.Futile($"There is nobody here who wants the {item.Name}.");
            }

            // "give to the woman" names who, not what. Telling someone they are
            // not carrying a person is the kind of sentence this world has been
            // apologising for all night.
            return CommandResult.Futile(
                $"You aren't holding anything called \"{argument}\". Giving wants the thing, not the person.");
        }, "give", "offer", "hand", "hand over", "hand to");

        // Throwing.
        //
        // Edward stood in front of the cave troll holding a bone he had taken off
        // the goblin ninety seconds earlier and typed "Throw bone". The world
        // returned an empty string — not a refusal, nothing, the worst answer
        // this place is capable of. Then he typed "Drop bone", and dropped it at
        // the troll's feet, because the idea was good enough that he went looking
        // for another verb to do it with.
        //
        // He was not the first. Two earlier lives stood in that same passage and
        // typed "eat bone" and "how can i use the bone", and Ravick threw one at
        // the troll twice in the same minute. Four attempts, at least three
        // people, none of whom ever met, all circling the same thought: that a
        // bone and a large hungry animal belong together.
        //
        // The passage now catches it — that is the map's business and the map
        // does it, in a fragment. This stays because the engine still cannot
        // know what any given thing means to any given creature: it makes the
        // sentence a recognised shape, checks it against what is actually in
        // your hands, understands "throw X at Y", and returns Futile so it
        // carries on into the room, which may well have an answer. It is only
        // heard where nothing does.
        //
        // So the refusal speaks about the creature in front of you and not about
        // the valley. It used to say nothing in this valley had been taught to
        // catch anything, which was true when it was written and is not true now.
        //
        // What it will not do is pretend. It does not quietly drop the item on
        // the floor and call that a throw.
        registry.RegisterPrefix((engine, session, rawArgument) =>
        {
            var argument = StripFillers(rawArgument);

            // "throw the bone at the troll" names the target as well as the
            // thing. Everything from "at" onward is who, not what.
            var at = argument.IndexOf(" at ", StringComparison.OrdinalIgnoreCase);

            if (at > 0)
            {
                argument = StripFillers(argument[..at].Trim());
            }

            if (session.Character.FindItemOrEquipped(argument) is not { } item)
            {
                return CommandResult.Futile(
                    $"You aren't holding anything called \"{argument}\" to throw.");
            }

            if (engine.CurrentLocation(session)?.Monster is { IsAlive: true } target)
            {
                return CommandResult.Futile(
                    $"You weigh the {item.Name} and look at the {target.Name}, and it is not a stupid "
                    + $"idea — but the {target.Name} shows no sign of wanting it, and you would only be "
                    + $"standing here afterwards without the {item.Name}. If you want it on the ground "
                    + "between you, drop it.");
            }

            return CommandResult.Futile(
                $"You could throw the {item.Name}. There is nothing here to throw it at, and it "
                + "would only want picking up again.");
        }, "throw", "toss", "hurl", "lob", "chuck", "fling", "sling");

        registry.RegisterExact((_, _, _) =>
            CommandResult.Futile("Throw what? Name the thing, and what at, if it matters."),
            "throw", "toss", "hurl", "lob", "chuck", "fling");

        registry.RegisterPrefix((engine, session, rawArgument) =>
        {
            var argument = StripFillers(rawArgument);
            var location = engine.CurrentLocation(session);

            if (location is null)
            {
                return CommandResult.Futile($"There is no {argument} here to take.");
            }

            // Standing over what the dead left behind, nobody wants to type seven
            // lines to pick it up.
            if (argument.Length == 0 || Everything.Contains(TriggerMatcher.Normalize(argument)))
            {
                if (location.Items.Count == 0)
                {
                    // An empty floor is not an empty room. What the thing in the
                    // corner is holding is here; it is only not yours.
                    if (location.Monster is { IsAlive: true } keeper && keeper.Inventory.Count > 0)
                    {
                        return CommandResult.Futile(
                            "There is nothing on the ground. The "
                            + $"{keeper.Name} is holding {Join([.. keeper.Inventory.Select(held => held.Name)])}, "
                            + "which is a different matter.");
                    }

                    return CommandResult.Futile("There is nothing here to take.");
                }

                foreach (var lying in location.Items.ToArray())
                {
                    Take(engine, session, location, lying);
                }

                return CommandResult.Done();
            }

            var item = location.Items.FirstOrDefault(candidate => candidate.Matches(argument));

            if (item is null)
            {
                // Reaching for something already in your hands is a misunderstanding
                // worth clearing up, not a wall.
                if (session.Character.FindItemOrEquipped(argument) is { } carried)
                {
                    return CommandResult.Futile($"You already have the {carried.Name}.");
                }

                // The oldest lie this world told: a room whose own description
                // names a sword answering "there is no sword here to take". It is
                // here. It is in something's hand, and that is a fact about this
                // room that the room should not be left to guess at — somebody
                // asked three times for a chisel a drowned man was holding, was
                // handed a hint about the chisel each time by a trigger that
                // caught the word, and never once learned that the way to it was
                // through him. So this answers, and it is the end of the matter:
                // no glob gets to override a true statement about a real thing
                // that is really standing here.
                if (location.Monster is { IsAlive: true } holder
                    && holder.FindItem(argument) is { } held)
                {
                    session.Write(
                        $"The {held.Name} is not on the ground. The {holder.Name} has it and has not "
                        + "let go of it, and things in this valley come off the dead, not off whatever "
                        + "is still standing up.");

                    return CommandResult.Done();
                }

                return CommandResult.Futile($"There is no {argument} here to take.");
            }

            Take(engine, session, location, item);
            return CommandResult.Done();
        }, "take", "pick up", "get", "grab", "loot", "gather");

        registry.RegisterPrefix((engine, session, rawArgument) =>
        {
            var argument = StripFillers(rawArgument);
            var location = engine.CurrentLocation(session);
            var item = session.Character.FindItem(argument);

            if (location is null || item is null)
            {
                return CommandResult.Futile($"You aren't carrying a {argument}.");
            }

            session.Character.Inventory.Remove(item);
            location.Items.Add(item);
            session.Write($"You drop the {item.Name}.");
            engine.BroadcastToLocation(location.Id, $"{session.Character.Name} drops the {item.Name}.", except: session);
            engine.Emit(GameEvent.For(session, EventType.ItemDropped, ("item", item.Name)));
            return CommandResult.Done();
        }, "drop");
    }

    private static string Join(IReadOnlyList<string> values) => values.Count switch
    {
        0 => "nothing",
        1 => values[0],
        2 => $"{values[0]} and {values[1]}",
        _ => $"{string.Join(", ", values.Take(values.Count - 1))}, and {values[^1]}"
    };

    private static void Take(GameEngine engine, Session session, Location location, Item item)
    {
        location.Items.Remove(item);
        session.Character.Inventory.Add(item);
        session.Write($"You take the {item.Name}.");
        engine.BroadcastToLocation(location.Id, $"{session.Character.Name} takes the {item.Name}.", except: session);
        engine.Emit(GameEvent.For(session, EventType.ItemPickedUp, ("item", item.Name)));
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

            // Naming what is in front of you is the commonest way to say it, and
            // people misspell it with something breathing down their neck. A near
            // miss is taken as the only creature in the room. Anything further off
            // is refused — but told what is actually standing there.
            if (argument.Length > 0 && !monster.Matches(argument))
            {
                var target = StripFillers(TriggerMatcher.Normalize(argument));

                if (target.Length == 0 || monster.Matches(target))
                {
                    engine.Attack(session);
                    return CommandResult.Done();
                }

                var near = target.Length >= 4
                    && (TriggerMatcher.Distance(target, monster.Name) <= 2
                        || monster.Name.Split(' ').Any(word =>
                            word.Length >= 4 && TriggerMatcher.Distance(target, word) <= 2));

                if (!near)
                {
                    return CommandResult.Futile(
                        $"There is no {argument} here to attack. The {monster.Name} is what is in front of you.");
                }
            }

            engine.Attack(session);
            return CommandResult.Done();
        }

        registry.RegisterExact(Strike, "attack", "fight", "kill", "stab");
        registry.RegisterPrefix(Strike, "attack", "fight", "kill", "stab", "attack the", "kill the");

        // The door.
        //
        // Walking out is the only ending anybody chooses, and it is as final as
        // the other one: the character is finished and nothing comes back in. So
        // it is asked for twice. The first time the door explains exactly what it
        // costs and does not open; the second time it opens. The remembering is
        // character-scoped, so it dies with them and the next life is warned too.
        //
        // Soft words — "leave", "go home" — never commit, however often they are
        // typed. Only words that can mean nothing else do.
        CommandResult AtTheDoor(GameEngine engine, Session session, bool committing)
        {
            var location = engine.CurrentLocation(session);

            if (location is null || !location.IsExit)
            {
                return CommandResult.Futile("Sadly, this is not the exit.");
            }

            if (!session.Character.HasKey)
            {
                return CommandResult.Futile(
                    "The door is locked and you are not carrying the key. The troll in the passage keeps one.");
            }

            if (!committing || session.Character.Conditions.Add(WarnedAtTheDoor))
            {
                session.Write(
                    "Your hand is on it and it gives — unlocked, unbarred, a half-inch of cold daylight and a "
                    + "road going away downhill.\n\n"
                    + "Be sure. This is the edge of the world that was made for you. Nothing was built out "
                    + "there and nothing is waiting, and there is no door back in. Whoever goes through is "
                    + "finished here, carrying out everything they earned. It is not a death — it is the "
                    + "other way out of this valley, and it is the better one.\n\n"
                    + "Say the word again and you go: exit.");

                return CommandResult.Done();
            }

            engine.Leave(session);
            return CommandResult.Done();
        }

        registry.RegisterExact((engine, session, _) => AtTheDoor(engine, session, committing: true),
            "exit", "escape", "walk out", "step through", "through the door", "walk out of the valley");

        registry.RegisterExact((engine, session, _) => AtTheDoor(engine, session, committing: false),
            "leave", "leave here", "go home");
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

        registry.RegisterPrefix((engine, session, argument) =>
        {
            var split = argument.IndexOf(' ');

            if (split <= 0)
            {
                return CommandResult.Futile("Whisper to whom?");
            }

            var targetName = argument[..split];
            var text = argument[(split + 1)..];
            var target = engine.Sessions.FirstOrDefault(other =>
                string.Equals(other.Character.Name, targetName, StringComparison.OrdinalIgnoreCase));

            if (target is null)
            {
                return CommandResult.Futile($"There is no {targetName} here.");
            }

            target.Write($"{session.Character.Name} whispers: {text}");
            session.Write($"You whisper to {target.Character.Name}: {text}");
            engine.Emit(GameEvent.For(session, EventType.Speech,
                ("mode", "whisper"), ("target", target.CharacterId), ("text", text)));
            return CommandResult.Done();
        }, "whisper", "tell");

        // Addressing whoever is in front of you. The engine has no conversation in
        // it and never will — but this returns Futile, so the command carries on
        // into the room, and a place that has someone worth talking to can answer
        // where the engine cannot. Before this verb existed, "talk to the goblin"
        // was not even a recognised shape of sentence.
        CommandResult Talk(GameEngine engine, Session session, string argument)
        {
            var location = engine.CurrentLocation(session);

            if (location?.Monster is { IsAlive: true } monster)
            {
                return CommandResult.Futile(
                    $"The {monster.Name} does not answer in any language you have.");
            }

            return CommandResult.Futile("There is nobody here to talk to.");
        }

        registry.RegisterPrefix(Talk, "talk to", "talk", "greet", "hail", "ask", "chat to", "chat with", "chat");
        registry.RegisterExact(Talk, "talk", "greet", "hello", "hallo", "hi", "hail", "chat", "anyone there");

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
