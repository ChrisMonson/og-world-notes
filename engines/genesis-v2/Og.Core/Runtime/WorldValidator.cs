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

        foreach (var location in world.Locations)
        {
            foreach (var direction in Enum.GetValues<Direction>())
            {
                var target = location.Exit(direction);

                if (target != 0 && !ids.Contains(target))
                {
                    issues.Add(new ValidationIssue(ValidationIssue.Error,
                        $"Location {location.Id} exits {direction} to {target}, which does not exist."));
                }
            }

            foreach (var interaction in location.Interactions)
            {
                if (interaction.Triggers.Length == 0)
                {
                    issues.Add(new ValidationIssue(ValidationIssue.Error,
                        $"Location {location.Id} has an interaction with no triggers."));
                }

                foreach (var action in interaction.Actions)
                {
                    ValidateAction(world, ids, location.Id, action, issues);
                }
            }
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

    public static bool HasErrors(IEnumerable<ValidationIssue> issues) =>
        issues.Any(issue => issue.Severity == ValidationIssue.Error);
}
