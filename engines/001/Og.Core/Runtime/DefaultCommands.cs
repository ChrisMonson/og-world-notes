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

                engine.MoveTo(session, target);
                return CommandResult.Done();
            }, forms);
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

        registry.RegisterPrefix((_, session, argument) =>
        {
            var found = session.Character.FindItemOrEquipped(argument);

            if (found is null)
            {
                return CommandResult.Futile($"You aren't carrying a {argument}.");
            }

            session.Write(found.Description);
            return CommandResult.Done();
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
                    return CommandResult.Futile($"You can't equip the {item.Name}.");
            }

            character.Inventory.Remove(item);
            session.Write($"You equipped the {item.Name}.");
            return CommandResult.Done();
        }, "equip", "wield", "wear");

        registry.RegisterPrefix((_, session, argument) =>
        {
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

        registry.RegisterPrefix((engine, session, argument) =>
        {
            var location = engine.CurrentLocation(session);
            var item = location?.Items.FirstOrDefault(candidate => candidate.Matches(argument));

            if (location is null || item is null)
            {
                return CommandResult.Futile($"There is no {argument} here to take.");
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

    private static void RegisterCombat(CommandRegistry registry)
    {
        registry.RegisterExact((engine, session, _) =>
        {
            var location = engine.CurrentLocation(session);

            if (location?.Monster is not { IsAlive: true })
            {
                return CommandResult.Futile("There is nothing to attack here.");
            }

            engine.Attack(session);
            return CommandResult.Done();
        }, "attack", "fight", "kill", "stab");

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

            engine.Emit(GameEvent.For(session, EventType.Escaped));
            engine.BroadcastToWorld($"{session.Character.Name} has escaped.", except: session);
            session.Write("You have escaped! Congratulations!");
            return CommandResult.Done();
        }, "exit", "escape", "leave");
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
