using Og.Core.Model;

namespace Og.Core.Runtime;

/// <summary>
/// The verbs the engine knows. A handler returns Done only when something
/// actually happened; when it recognises the words but can do nothing with them
/// it returns Futile, and the command carries on to the place the player is in.
/// </summary>
public static class DefaultCommands
{
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

                    return CommandResult.Futile("You can't go that way.");
                }

                session.PreviousLocationId = location.Id;
                engine.MoveTo(session, target);
                return CommandResult.Done();
            }, forms);

        // "go back" is how people say it when they have just walked somewhere and
        // want out again. Lazer typed it on the quay and the world said nothing.
        // It only retraces a door that is genuinely open both ways.
        registry.RegisterExact((engine, session, _) =>
        {
            var location = engine.CurrentLocation(session);

            if (location is null)
            {
                return CommandResult.NotHandled;
            }

            var previous = session.PreviousLocationId;

            if (previous <= 0 || previous == location.Id)
            {
                return CommandResult.Futile("You haven't come from anywhere yet.");
            }

            var directions = new[] { Direction.North, Direction.East, Direction.South, Direction.West };
            var way = directions.FirstOrDefault(candidate => location.Exit(candidate) == previous, (Direction)(-1));

            if ((int)way < 0)
            {
                return CommandResult.Futile("The way you came is not open from this side.");
            }

            session.PreviousLocationId = location.Id;
            engine.MoveTo(session, previous);
            return CommandResult.Done();
        }, "back", "go back", "return", "retrace", "turn back", "go back the way i came");
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
            // "health" was the only word for this, and it is not the word anybody
            // reaches for while they are losing. Malcolm fought the heron down to
            // 5 of 20 typing nothing but "attack", and "status" — the first thing
            // a person tries when they want to know how bad it is — answered
            // "Nothing happens." A player is allowed to know the state of their
            // own body without guessing the noun for it.
        }, "health", "hit points", "hp", "status", "condition", "score", "stats",
           "how am i", "how am i doing", "me", "myself", "check myself", "wounds");

        // Somebody who has just arrived has no way to find out what words work.
        // Without this they guess, get "Nothing happens", and conclude the world
        // is deaf. It isn't, but it needs to say so once.
        registry.RegisterExact((_, session, _) =>
        {
            session.Write(
                "The engine knows these: look, north/east/south/west (n/e/s/w), back, take a thing, "
                + "drop a thing, inventory, equip a thing, unequip a thing, use a thing, examine a thing, attack, "
                + "health, who, say something, yell something, whisper <name> something, pray, leave.\n\n"
                + "Everything else, type as you would actually do it — ring the bell, read the names, "
                + "lower the bucket, touch the water. The places here listen for plain sentences, and "
                + "some of them are listening for one in particular.\n\n"
                + "'Nothing happens' means nothing happened. It does not mean you were wrong to try.");

            return CommandResult.Done();
        }, "help", "commands", "verbs", "?");

        registry.RegisterExact((engine, session, _) =>
        {
            var names = engine.Sessions.Select(other => other.Character.Name).Order();
            session.Write($"Here in this world: {string.Join(", ", names)}");
            return CommandResult.Done();
        }, "who", "players");

        // Looking at a thing is not an inventory operation, and for a long time
        // this handler treated it as one. Malcolm typed "look at the heron",
        // "look at hook", "look at board" — every noun out of the description he
        // had just been shown — and was told four times that he wasn't carrying
        // them. It reads as a world that does not know its own furniture.
        registry.RegisterPrefix((engine, session, argument) =>
        {
            // You are a thing that is here. "look at me" told me I couldn't see
            // a me here and wasn't carrying one, which is a sentence about a
            // missing object where a person was standing.
            if (argument is "me" or "myself" or "self")
            {
                var self = session.Character;
                session.Write($"{self.Name}. HP: {self.HitPoints}/{self.MaxHitPoints}");
                session.Write($"Weapon: {self.Weapon?.Name ?? "none equipped"}");
                session.Write($"Armor: {self.Armor?.Name ?? "none equipped"}");
                return CommandResult.Done();
            }

            if (session.Character.FindItemOrEquipped(argument) is { } carried)
            {
                session.Write(carried.Description);
                return CommandResult.Done();
            }

            var location = engine.CurrentLocation(session);

            // A thing lying on the floor of the room you are standing in is a
            // thing you can look at without picking it up first.
            if (location?.Items.FirstOrDefault(candidate => candidate.Matches(argument)) is { } onGround)
            {
                session.Write(onGround.Description);
                return CommandResult.Done();
            }

            // Something alive and in front of you. Futile rather than Done, so a
            // room that has written a better look-at than the creature's one-line
            // description still wins — and so that a creature nobody has written
            // one for shows up in the unanswered log, which is where I find the
            // holes in my own prose.
            if (location?.Monster is { IsAlive: true } creature && creature.Matches(argument))
            {
                return CommandResult.Futile(creature.Description);
            }

            // Last resort, printed only when the room had nothing either. It has
            // to be true about both of the places a thing could have been.
            return CommandResult.Futile(
                $"You can't see a {Item.Bare(argument)} here, and you aren't carrying one.");
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
                : "Inventory: " + string.Join(", ", character.Inventory.Select(item => item.Name)));

            return CommandResult.Done();
        }, "inventory", "items", "i");

        registry.RegisterPrefix((_, session, argument) =>
        {
            var character = session.Character;
            var item = character.FindItem(argument);

            if (item is null)
            {
                return CommandResult.Futile($"You aren't carrying a {Item.Bare(argument)}.");
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

        // Taking a thing off is the other half of equip, and the engine only ever
        // had the first half. Malcolm tried "unequip knife", "unequip clasp-knife",
        // "unequip coat" inside thirty seconds and the word was not recognised at
        // all. A verb you teach people in `help` must have its opposite.
        registry.RegisterPrefix((_, session, argument) =>
        {
            var character = session.Character;

            var item = character.Weapon is { } weapon && weapon.Matches(argument) ? weapon
                : character.Armor is { } armor && armor.Matches(argument) ? armor
                : null;

            if (item is null)
            {
                return character.FindItem(argument) is { } carried
                    ? CommandResult.Futile($"The {carried.Name} isn't equipped — you are already just carrying it.")
                    : CommandResult.Futile($"You don't have a {Item.Bare(argument)} equipped.");
            }

            if (character.Weapon == item) character.Weapon = null;
            else character.Armor = null;

            character.Inventory.Add(item);
            session.Write($"You put the {item.Name} away.");
            return CommandResult.Done();
        }, "unequip", "remove", "take off", "sheathe", "sheath", "put away", "stow", "unwield");

        // Said bare, with nothing named: it means the thing in your hand.
        registry.RegisterExact((_, session, _) =>
        {
            var character = session.Character;
            var item = character.Weapon ?? character.Armor;

            if (item is null)
            {
                return CommandResult.Futile("You have nothing equipped.");
            }

            if (character.Weapon == item) character.Weapon = null;
            else character.Armor = null;

            character.Inventory.Add(item);
            session.Write($"You put the {item.Name} away.");
            return CommandResult.Done();
        }, "unequip", "put it away", "sheathe", "stow", "unwield");

        registry.RegisterPrefix((_, session, argument) =>
        {
            var character = session.Character;
            var item = character.FindItem(argument);

            if (item is null)
            {
                return CommandResult.Futile($"You aren't carrying a {Item.Bare(argument)}.");
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
            var item = location?.Items.FirstOrDefault(candidate => candidate.Matches(argument));

            if (location is null || item is null)
            {
                return CommandResult.Futile($"There is no {Item.Bare(argument)} here to take.");
            }

            location.Items.Remove(item);
            session.Character.Inventory.Add(item);
            session.Write($"You take the {item.Name}.");
            engine.BroadcastToLocation(location.Id, $"{session.Character.Name} takes the {item.Name}.", except: session);
            engine.Emit(GameEvent.For(session, EventType.ItemPickedUp, ("item", item.Name)));
            return CommandResult.Done();
        }, "take", "pick up", "get", "grab");

        registry.RegisterPrefix((engine, session, argument) =>
        {
            var location = engine.CurrentLocation(session);
            var character = session.Character;
            var item = character.FindItemOrEquipped(argument);

            if (location is null || item is null)
            {
                return CommandResult.Futile($"You aren't carrying a {Item.Bare(argument)}.");
            }

            // The thing in your hand is a thing you are carrying. Malcolm had the
            // clasp-knife equipped, typed "drop knife", and was told he wasn't
            // carrying one while it was open in his hand. Putting a held thing
            // down means letting go of it first.
            if (character.Weapon == item) character.Weapon = null;
            else if (character.Armor == item) character.Armor = null;

            character.Inventory.Remove(item);
            location.Items.Add(item);
            session.Write($"You drop the {item.Name}.");
            engine.BroadcastToLocation(location.Id, $"{session.Character.Name} drops the {item.Name}.", except: session);
            engine.Emit(GameEvent.For(session, EventType.ItemDropped, ("item", item.Name)));
            return CommandResult.Done();
        }, "drop");
    }

    /// <summary>
    /// The combat verbs, in the form the engine sees after normalising. Kept
    /// here beside their registration so the two cannot drift apart: adding a
    /// verb below without adding it here would quietly put that verb out of
    /// the room's reach.
    /// </summary>
    private static readonly string[] ViolentVerbs = ["attack", "fight", "kill", "stab"];

    /// <summary>
    /// Whether these words are somebody swinging at something. The engine asks
    /// before it rolls, because combat writes and prints before it could fall
    /// through to the place the player is standing in. See GameEngine.HandleInput.
    /// </summary>
    public static bool IsViolence(string command) =>
        ViolentVerbs.Any(verb =>
            command == verb || command.StartsWith(verb + " ", StringComparison.Ordinal));

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
            if (argument.Length > 0 && !monster.Matches(argument))
            {
                return CommandResult.Futile($"There is no {Item.Bare(argument)} here to attack.");
            }

            engine.Attack(session);
            return CommandResult.Done();
        }

        registry.RegisterExact(Strike, "attack", "fight", "kill", "stab");
        registry.RegisterPrefix(Strike, "attack", "fight", "kill", "stab", "attack the", "kill the");

        registry.RegisterExact((engine, session, _) =>
        {
            var location = engine.CurrentLocation(session);

            if (location is null || !location.IsExit)
            {
                return CommandResult.Futile("Sadly, this is not the exit.");
            }

            if (!session.Character.HasKey)
            {
                return CommandResult.Futile("You don't have a key.");
            }

            // A way out can be shut by the state of the world and not only by
            // what you are carrying. Falling through Futile lets the place say
            // why in its own voice, which it has already written down.
            if (location.ExitRequires.Any(condition => !engine.World.HasCondition(session, condition)))
            {
                return CommandResult.Futile("You can't leave that way yet.");
            }

            // A door you have already gone through does not open twice from the
            // same side. Malcolm arrived, typed "exit", was congratulated on
            // escaping, and was still standing on the quay; he typed it four more
            // times in forty-five seconds. Nothing was broken in the sense of
            // throwing — the handler succeeded every time, wrote a farewell, and
            // changed nothing a player could see. That is worse than an error.
            //
            // Escaping cannot remove anybody: the session belongs to the socket,
            // not to the world. So the honest thing is to stop pretending, and to
            // say what is actually true — that leaving here is something you do by
            // stopping, and the place is not holding you. Futile, so the room can
            // say it better in its own voice, which the quay does.
            if (session.Character.Conditions.Contains("has_left"))
            {
                return CommandResult.Futile(
                    "You have already gone out that way once, and you came back. "
                    + "Nothing here is holding you: you leave this place by stopping, "
                    + "and it will be here when you don't.");
            }

            engine.Emit(GameEvent.For(session, EventType.Escaped));
            engine.BroadcastToWorld($"{session.Character.Name} has escaped.", except: session);

            // The last sentence a player reads should be the world's, not the
            // engine's, wherever the world has bothered to write one.
            session.Write(string.IsNullOrWhiteSpace(location.Farewell)
                ? "You have escaped! Congratulations!"
                : location.Farewell);

            // Leaving is a door, not a wall. Lazer escaped and walked back in two
            // minutes later, and nothing in the world could tell the difference
            // between them and somebody who had just arrived over the hill. This
            // is how a place gets to know its own returners.
            session.Character.Conditions.Add("has_left");

            return CommandResult.Done();
            // The exit here is a boat, and what a person types when they are
            // sitting in a boat with an oar is "push off". Lazer did, got
            // nothing, and had to guess "leave" instead. These all fall through
            // as Futile when the way out is not open, so the quay keeps its own
            // answer for somebody trying to sail under a standing wave.
        }, "exit", "escape", "leave", "push off", "shove off", "cast off",
           "row out", "row away", "row off", "set out", "set off", "sail",
           "sail away", "put to sea", "depart", "go to sea", "leave ambroke");
    }

    private static void RegisterSpeech(CommandRegistry registry)
    {
        // Speech is recorded and delivered. Saying and whispering go to people
        // and the world stays out of it; yelling is aimed at the place, and the
        // place is allowed to answer (see GameEngine.Overhear).
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

            // A shout is aimed at the place, not at the people. Let the place hear it.
            engine.Overhear(session, argument);

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
