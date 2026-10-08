namespace Og.Core.Runtime;

public sealed record ValidationIssue(string Severity, string Message)
{
    public const string Error = "error";
    public const string Warning = "warning";

    public override string ToString() => $"[{Severity}] {Message}";
}

/// <summary>
/// Structural checks over a loaded world. Cheap enough to run on every change,
/// and needs no build.
/// </summary>
public static class WorldValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(World world)
    {
        var issues = new List<ValidationIssue>();
        var ids = world.Locations.Select(location => location.Id).ToHashSet();

        if (ids.Count == 0)
        {
            issues.Add(new ValidationIssue(ValidationIssue.Error, "World has no locations."));
            return issues;
        }

        if (!ids.Contains(world.SpawnLocation))
        {
            issues.Add(new ValidationIssue(ValidationIssue.Error,
                $"Spawn location {world.SpawnLocation} does not exist."));
        }

        var known = KnownConditions(world);

        foreach (var location in world.Locations)
        {
            ValidateConditionNames($"Location {location.Id} exitRequires",
                location.ExitRequires, known, ValidationIssue.Error, issues);

            foreach (var direction in Enum.GetValues<Direction>())
            {
                var target = location.Exit(direction);

                if (target != 0 && !ids.Contains(target))
                {
                    issues.Add(new ValidationIssue(ValidationIssue.Error,
                        $"Location {location.Id} exits {direction} to {target}, which does not exist."));
                }
            }

            ValidateProse($"Location {location.Id} ('{location.Name}') description",
                location.Description, issues);

            ValidateUnknownKeys($"Location {location.Id}",
                location.UnknownKeys, LocationKeys, issues);
            ValidateUnknownKeys($"Location {location.Id} connections",
                location.UnknownConnectionKeys, ConnectionKeys, issues);

            foreach (var interaction in location.Interactions)
            {
                if (interaction.Triggers.Length == 0)
                {
                    issues.Add(new ValidationIssue(ValidationIssue.Error,
                        $"Location {location.Id} has an interaction with no triggers."));
                }

                ValidateTriggerArticles(location, interaction, issues);
                ValidateIdentity(location, interaction, issues);

                var where = $"Location {location.Id}, interaction '{interaction.Id ?? "(unnamed)"}'";
                ValidateConditionNames($"{where} if", interaction.If, known, ValidationIssue.Error, issues);
                ValidateConditionNames($"{where} ifNot", interaction.IfNot, known, ValidationIssue.Warning, issues);
                ValidateUnknownKeys(where, interaction.UnknownKeys, InteractionKeys, issues);

                foreach (var action in interaction.Actions)
                {
                    ValidateAction(world, ids, location.Id, action, issues);
                    ValidateActionProse(location.Id, interaction, action, issues);
                    ValidateUnknownKeys($"{where}, action '{action.AsWritten}'",
                        action.UnknownKeys, ActionKeys, issues);
                }
            }

            ValidateShadowing(location, issues);
            ValidateStrangerCoverage(location, issues);
            ValidateMonsterHasWords(location, issues);
        }

        ValidateReachability(world, issues);
        return issues;
    }

    private static void ValidateAction(
        World world,
        HashSet<int> locationIds,
        int locationId,
        WorldAction action,
        List<ValidationIssue> issues)
    {
        // An action nobody implements does nothing at all, quietly, forever. The
        // world file is the only place that would ever say otherwise.
        if (!ActionKinds.All.Contains(action.Kind))
        {
            issues.Add(new ValidationIssue(ValidationIssue.Error,
                $"Location {locationId} uses '{action.AsWritten}', which is not something an "
                + $"interaction can do. It would be read and then ignored. "
                + $"What there is: {string.Join(", ", ActionKinds.All.Order())}."));
            return;
        }

        switch (action.Kind)
        {
            case ActionKind.AddItems:
            case ActionKind.RemoveItems:
                foreach (var name in action.Parameters)
                {
                    if (world.FindItemDefinition(name) is null)
                    {
                        issues.Add(new ValidationIssue(ValidationIssue.Error,
                            $"Location {locationId} references unknown item '{name}'."));
                    }
                }
                break;

            case ActionKind.ChangeLocation:
            case ActionKind.SetLocationDescription:
                if (!locationIds.Contains(action.FirstParameterAsInt))
                {
                    issues.Add(new ValidationIssue(ValidationIssue.Error,
                        $"Location {locationId} targets location {action.FirstParameterAsInt}, which does not exist."));
                }
                break;

            case ActionKind.EnableInteraction:
            case ActionKind.DisableInteraction:
                if (action.Scope == Model.Scope.World && world.FindInteraction(action.FirstParameter) is null)
                {
                    issues.Add(new ValidationIssue(ValidationIssue.Error,
                        $"Location {locationId} toggles unknown interaction '{action.FirstParameter}'."));
                }
                break;
        }
    }

    private static void ValidateReachability(World world, List<ValidationIssue> issues)
    {
        var reached = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(world.SpawnLocation);

        while (queue.Count > 0)
        {
            var id = queue.Dequeue();

            if (!reached.Add(id) || world.FindLocation(id) is not { } location)
            {
                continue;
            }

            foreach (var direction in Enum.GetValues<Direction>())
            {
                if (location.Exit(direction) is var target and > 0)
                {
                    queue.Enqueue(target);
                }
            }

            // Portals count as connectivity.
            foreach (var action in location.Interactions.SelectMany(interaction => interaction.Actions))
            {
                if (action.Kind == ActionKind.ChangeLocation)
                {
                    queue.Enqueue(action.FirstParameterAsInt);
                }
            }
        }

        foreach (var location in world.Locations.Where(candidate => !reached.Contains(candidate.Id)))
        {
            issues.Add(new ValidationIssue(ValidationIssue.Warning,
                $"Location {location.Id} ('{location.Name}') is unreachable from spawn."));
        }

        if (!world.Locations.Any(location => location.IsExit && reached.Contains(location.Id)))
        {
            issues.Add(new ValidationIssue(ValidationIssue.Warning, "No reachable exit in this world."));
        }
    }

    /// <summary>
    /// Everything in an action that a player will read with their own eyes, as
    /// opposed to the parameters that are really identifiers.
    /// </summary>
    private static void ValidateActionProse(
        int locationId,
        Interaction interaction,
        WorldAction action,
        List<ValidationIssue> issues)
    {
        var where = $"Location {locationId}, interaction '{interaction.Id ?? "(unnamed)"}'";

        switch (action.Kind)
        {
            case ActionKind.Describe:
            case ActionKind.Announce:
                foreach (var parameter in action.Parameters)
                {
                    ValidateProse($"{where} ({action.Kind})", parameter, issues);
                }
                break;

            case ActionKind.SetLocationDescription:
                foreach (var parameter in action.Parameters.Skip(1))
                {
                    ValidateProse($"{where} ({action.Kind})", parameter, issues);
                }
                break;
        }
    }

    /// <summary>
    /// There is nothing here but sentences, and the validator cannot tell a good
    /// one from a bad one. It can tell that a backslash reached the page. In JSON
    /// a doubled backslash makes the two characters a reader actually sees, which
    /// has now happened twice mid-sentence in text a player was relying on.
    /// </summary>
    private static void ValidateProse(string where, string text, List<ValidationIssue> issues)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var index = text.IndexOf('\\');

        if (index < 0)
        {
            return;
        }

        var start = Math.Max(0, index - 24);
        var length = Math.Min(text.Length - start, 56);
        var excerpt = text.Substring(start, length).Replace("\n", " ");

        issues.Add(new ValidationIssue(ValidationIssue.Error,
            $"{where} has a backslash in text a player reads: \"...{excerpt}...\". "
            + "In the world file a doubled backslash puts a literal backslash on the page; "
            + "a real line break is a single one."));
    }

    /// <summary>
    /// Three separate players have now been stopped by knowing exactly the right
    /// words and leaving out "the". Every trigger is checked against its own
    /// interaction with the articles taken out: if the stripped phrase would no
    /// longer reach it, the bare form is missing.
    /// </summary>
    private static void ValidateTriggerArticles(
        Location location,
        Interaction interaction,
        List<ValidationIssue> issues)
    {
        var reported = new HashSet<string>();

        foreach (var trigger in interaction.Triggers)
        {
            var phrase = TriggerMatcher.Normalize(trigger.Replace("*", " "));

            if (phrase.Length == 0)
            {
                continue;
            }

            var bare = string.Join(' ', phrase
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(word => word != "the"));

            if (bare.Length == 0 || bare == phrase || !reported.Add(bare))
            {
                continue;
            }

            if (interaction.MatchesTrigger(bare))
            {
                continue;
            }

            issues.Add(new ValidationIssue(ValidationIssue.Warning,
                $"Location {location.Id}, interaction '{interaction.Id ?? "(unnamed)"}': answers "
                + $"\"{phrase}\" but not \"{bare}\". Add \"{bare}\" or \"*{bare}*\" to its triggers."));
        }
    }

    /// <summary>
    /// Conditions the engine sets by itself, with no action anywhere in the world
    /// file to account for them. Anything added here must really be set in code.
    /// </summary>
    private static readonly string[] EngineConditions = ["has_left"];

    /// <summary>
    /// Every condition name this world is capable of holding: the ones its own
    /// actions write, plus the ones the engine writes. A name outside this set is
    /// one nobody will ever set, which makes whatever waits on it wait forever.
    /// </summary>
    private static HashSet<string> KnownConditions(World world)
    {
        var known = new HashSet<string>(EngineConditions, StringComparer.OrdinalIgnoreCase);

        // Killing something sets a condition that no action anywhere writes: the
        // engine does it, in GameEngine.Attack, and takes it away again when the
        // creature returns. Without this the validator says 'slain_grey_heron' is
        // a name nothing in this world ever sets, which is false. I believed it,
        // on 2026-09-22, and threw away a finished piece of writing on the
        // strength of it. An instrument that is wrong about the world costs more
        // than the hole it was pointing at.
        //
        // Taken from what the file declares belongs here, not from what is
        // standing here now, so the name is known whether or not the creature is
        // currently alive.
        foreach (var name in world.Locations.Select(location => location.AuthoredMonster))
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                known.Add(GameEngine.SlainCondition(name));
                known.Add(GameEngine.KilledCondition(name));
            }
        }

        foreach (var action in world.Locations
                     .SelectMany(location => location.Interactions)
                     .SelectMany(interaction => interaction.Actions))
        {
            if (action.Kind is ActionKind.AddCondition or ActionKind.RemoveCondition)
            {
                foreach (var name in action.Parameters)
                {
                    known.Add(name);
                }
            }
        }

        return known;
    }

    /// <summary>
    /// A gate whose key does not exist. One mistyped character in an 'if' does not
    /// fail loudly — it makes the interaction silently unreachable for everyone
    /// forever, and the fallback underneath it answers instead, which reads like
    /// the world working.
    /// </summary>
    private static void ValidateConditionNames(
        string where,
        IEnumerable<string> conditions,
        HashSet<string> known,
        string severity,
        List<ValidationIssue> issues)
    {
        foreach (var condition in conditions)
        {
            if (string.IsNullOrWhiteSpace(condition) || known.Contains(condition))
            {
                continue;
            }

            var nearest = known
                .Where(candidate => Near(candidate, condition))
                .Order()
                .FirstOrDefault();

            var hint = nearest is null
                ? $" Conditions this world sets: {string.Join(", ", known.Order())}."
                : $" Did you mean '{nearest}'?";

            issues.Add(new ValidationIssue(severity,
                $"{where} waits on '{condition}', which nothing in this world ever sets."
                + hint));
        }
    }

    /// <summary>Every key each authored thing actually reads, spelled as the file spells it.</summary>
    private static readonly string[] LocationKeys =
    [
        "index", "name", "description", "connections", "isExit",
        "farewell", "exitRequires", "monster", "interactions"
    ];

    private static readonly string[] ConnectionKeys = ["north", "east", "south", "west"];

    private static readonly string[] InteractionKeys =
        ["interactionId", "triggers", "actions", "if", "ifNot", "once", "scope", "disabled"];

    private static readonly string[] ActionKeys = ["action", "parameters", "scope"];

    /// <summary>
    /// The quietest failure this world has. An unknown key is not rejected when the
    /// file is loaded — it is dropped without a word, and everything downstream
    /// behaves as though the line was never written. So 'ifNot' spelled 'ifnot2'
    /// does not fail: it deletes the gate, and the interaction fires for everyone,
    /// forever, while the file still reads to its author exactly as intended. No
    /// other mistake here is invisible in both directions at once.
    ///
    /// An error, not a warning. A dropped key never degrades gracefully; it changes
    /// what the world does to people and leaves the reason on the page, unread.
    /// </summary>
    private static void ValidateUnknownKeys(
        string where,
        IReadOnlyList<string> unknown,
        IReadOnlyCollection<string> expected,
        List<ValidationIssue> issues)
    {
        foreach (var key in unknown)
        {
            var nearest = expected
                .Where(candidate => Near(candidate, key))
                .Order()
                .FirstOrDefault();

            var hint = nearest is not null
                ? $" Did you mean '{nearest}'?"
                : $" What is read here: {string.Join(", ", expected)}."
                  + " A key starting with '_' is treated as a note and left alone.";

            issues.Add(new ValidationIssue(ValidationIssue.Error,
                $"{where} sets '{key}', which nothing reads. It was dropped in silence when "
                + $"the file was loaded, so whatever it was meant to do is not happening.{hint}"));
        }
    }

    /// <summary>Close enough that it is likelier a slip than a different word.</summary>
    private static bool Near(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 2)
        {
            return false;
        }

        var shared = a.Zip(b).Count(pair => char.ToLowerInvariant(pair.First) == char.ToLowerInvariant(pair.Second));
        return shared >= Math.Max(a.Length, b.Length) - 2;
    }

    /// <summary>
    /// An interaction with no id is anonymous: it cannot be enabled or disabled, it
    /// cannot be told apart in the event log, and 'once' does not hold it, because
    /// the engine tracks fired interactions by id. Mistyping 'interactionId' costs
    /// all of that and nothing complains.
    /// </summary>
    private static void ValidateIdentity(Location location, Interaction interaction, List<ValidationIssue> issues)
    {
        if (!string.IsNullOrWhiteSpace(interaction.Id) || interaction.Triggers.Length == 0)
        {
            return;
        }

        var first = interaction.Triggers[0];

        if (interaction.Once)
        {
            issues.Add(new ValidationIssue(ValidationIssue.Error,
                $"Location {location.Id} has an interaction answering \"{first}\" marked once, "
                + "but with no interactionId. Firing is remembered by id, so it would fire "
                + "every time instead of once. Give it an interactionId."));
            return;
        }

        issues.Add(new ValidationIssue(ValidationIssue.Warning,
            $"Location {location.Id} has an interaction answering \"{first}\" with no interactionId. "
            + "It cannot be enabled, disabled, or told apart in the log. "
            + "Check the spelling of 'interactionId'."));
    }

    /// <summary>
    /// Only the first eligible match fires, in file order. So an interaction with no
    /// conditions on it stands in front of every later one that answers the same
    /// words: they are unreachable, and the world quietly plays the general case
    /// where a particular one was written. The gated variant goes first.
    /// </summary>
    private static void ValidateShadowing(Location location, List<ValidationIssue> issues)
    {
        for (var i = 0; i < location.Interactions.Count; i++)
        {
            var earlier = location.Interactions[i];

            // These stop being eligible, so they do not stand in front of anything forever.
            if (earlier.If.Length > 0 || earlier.IfNot.Length > 0 || earlier.Once || earlier.DisabledForWorld)
            {
                continue;
            }

            for (var j = i + 1; j < location.Interactions.Count; j++)
            {
                var later = location.Interactions[j];

                foreach (var trigger in later.Triggers)
                {
                    var phrase = TriggerMatcher.Normalize(trigger.Replace("*", " "));

                    if (phrase.Length == 0 || !earlier.MatchesTrigger(phrase))
                    {
                        continue;
                    }

                    // Telling somebody to put the conditional one first when neither
                    // is conditional is advice they cannot take, from the instrument
                    // they are relying on to tell them the truth.
                    var advice = later.If.Length > 0 || later.IfNot.Length > 0 || later.Once
                        ? "Put the conditional one first."
                        : "Neither of them has a condition, so for these words the later one is "
                          + "simply unreachable: fold its triggers into the one above, or take it out.";

                    issues.Add(new ValidationIssue(ValidationIssue.Warning,
                        $"Location {location.Id}: '{later.Id ?? "(unnamed)"}' answers \"{phrase}\", "
                        + $"but '{earlier.Id ?? "(unnamed)"}' above it answers that too and has no "
                        + $"conditions, so it always wins. {advice}"));
                    break;
                }
            }
        }
    }

    /// <summary>
    /// The instrument the caretaker cannot otherwise have. `play` returns you to
    /// your most recent living character, so every hand-test of this world is run
    /// by somebody who has already done half of it, and the beginner's experience
    /// is the one thing that is never actually checked. On 2026-09-21 that cost a
    /// real piece of writing: a veteran's line was read by the veteran, mistaken
    /// for a lie told to a beginner, and deleted.
    ///
    /// A player holding no conditions can never be excluded by an 'ifNot', so an
    /// empty 'if' is the whole test of what they can reach. If a room advertises a
    /// phrase only from behind a condition, the beginner who types it gets
    /// "Nothing happens." — and silence is the answer that teaches them the room
    /// does not care, which is the one thing here that is not true.
    ///
    /// A warning, not an error. It is a missing kindness rather than a lie, and
    /// there is no world in which every gated branch needs an ungated twin.
    /// </summary>
    private static void ValidateStrangerCoverage(Location location, List<ValidationIssue> issues)
    {
        var open = location.Interactions
            .Where(interaction => interaction.If.Length == 0 && !interaction.DisabledForWorld)
            .ToList();

        var seen = new HashSet<string>();

        foreach (var interaction in location.Interactions)
        {
            foreach (var trigger in interaction.Triggers)
            {
                var phrase = TriggerMatcher.Normalize(trigger.Replace("*", " "));

                if (phrase.Length == 0 || !seen.Add(phrase))
                {
                    continue;
                }

                if (open.Any(candidate => candidate.MatchesTrigger(phrase)))
                {
                    continue;
                }

                issues.Add(new ValidationIssue(ValidationIssue.Warning,
                    $"Location {location.Id}: \"{phrase}\" is offered by "
                    + $"'{interaction.Id ?? "(unnamed)"}', which sits behind a condition, and nothing "
                    + "here answers it without one. Somebody who has done nothing types it and "
                    + "gets silence."));
            }
        }
    }

    /// <summary>
    /// A place with something alive in it and nothing written for hitting it.
    ///
    /// The engine will answer that on its own — "You missed!", "The X has died."
    /// — and those are the only words a player gets for the one irreversible
    /// thing this world lets them do to a living creature. Ambroke's quay offered
    /// the knife in three separate paragraphs of its own prose and then handed the
    /// moment to two lines of arithmetic, and nothing anywhere reported it: no
    /// refusal, no unanswered command, an interaction_fired count that looked
    /// healthy. It took typing it myself to find.
    ///
    /// The engine now offers violence to the room before it rolls, but only for
    /// triggers that name the command outright, so a room that never wrote them
    /// is silently skipped. This is the notice that it was skipped.
    ///
    /// A warning. Some creatures are furniture and dice are a fine answer for
    /// them — this exists so that is a decision and not an oversight.
    /// </summary>
    private static void ValidateMonsterHasWords(Location location, List<ValidationIssue> issues)
    {
        if (location.Monster is not { } monster)
        {
            return;
        }

        var blows = new[] { "attack", "kill", "fight", "stab" }
            .SelectMany(verb => new[] { verb, $"{verb} the {monster.Name}", $"{verb} {monster.Name}" })
            .ToArray();

        var answered = location.Interactions.Any(interaction =>
            interaction.If.Length == 0
            && !interaction.DisabledForWorld
            && blows.Any(interaction.NamesTrigger));

        if (answered)
        {
            return;
        }

        issues.Add(new ValidationIssue(ValidationIssue.Warning,
            $"Location {location.Id} has a {monster.Name} and nothing written for attacking it, "
            + "so a blow there is answered by the dice alone. The engine offers violence to the "
            + "room before it rolls, but only for triggers that name it outright — a wildcard "
            + $"like \"*{monster.Name}*\" does not count, or it would stand in front of the dice "
            + $"for ever. Add an interaction naming \"attack\" and \"attack the {monster.Name}\", "
            + "or decide the dice are the right answer here."));
    }

    public static bool HasErrors(IEnumerable<ValidationIssue> issues) =>
        issues.Any(issue => issue.Severity == ValidationIssue.Error);
}
