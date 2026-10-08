using Og.Core.Definitions;
using Og.Core.Model;

namespace Og.Core.Runtime;

public sealed class Interaction
{
    public string? Id { get; }
    public string[] Triggers { get; }
    public WorldAction[] Actions { get; }
    public string[] If { get; }
    public string[] IfNot { get; }
    public Scope Scope { get; }
    public bool Once { get; }

    /// <summary>World-scoped disabled state. Player-scoped state lives on the session.</summary>
    public bool DisabledForWorld { get; set; }

    /// <summary>Keys the file set here that the loader threw away. Carried for the validator.</summary>
    public string[] UnknownKeys { get; }

    public Interaction(InteractionDefinition definition)
    {
        Id = definition.InteractionId;
        UnknownKeys = definition.UnknownKeys;
        Triggers = definition.Triggers;
        If = definition.If;
        IfNot = definition.IfNot;
        Scope = definition.Scope;
        Once = definition.Once;
        DisabledForWorld = definition.Disabled;
        Actions = [.. definition.Actions.Select(action => new WorldAction(action))];
    }

    public bool IsDisabledFor(Session session) =>
        Scope == Scope.World
            ? DisabledForWorld
            : session.Character.DisabledInteractions.Contains(Id ?? "");

    /// <summary>
    /// Whether this interaction is currently eligible for the given player.
    /// Conditions are checked against the union of world and player flags.
    /// </summary>
    public bool IsEligible(World world, Session session)
    {
        if (IsDisabledFor(session))
        {
            return false;
        }

        if (Once && Id is not null && session.Character.FiredInteractions.Contains(Id))
        {
            return false;
        }

        foreach (var condition in If)
        {
            if (!world.HasCondition(session, condition))
            {
                return false;
            }
        }

        foreach (var condition in IfNot)
        {
            if (world.HasCondition(session, condition))
            {
                return false;
            }
        }

        return true;
    }

    public bool MatchesTrigger(string command) =>
        Triggers.Any(trigger => TriggerMatcher.Matches(trigger, command));

    /// <summary>
    /// Whether this interaction names the command outright instead of catching it
    /// in a wildcard.
    ///
    /// Only matters where a room is allowed to speak in front of the engine
    /// rather than after it. A catch-all like "*heron*" is a room saying "anything
    /// about the bird is mine", which is not the same as having written an answer
    /// for being attacked — and if it counted, it would stand in front of the dice
    /// for ever and no blow aimed at the bird could ever land. Naming the words is
    /// how a place says it meant this one.
    /// </summary>
    public bool NamesTrigger(string command) =>
        Triggers.Any(trigger =>
            !trigger.Contains('*') && TriggerMatcher.Matches(trigger, command));
}
