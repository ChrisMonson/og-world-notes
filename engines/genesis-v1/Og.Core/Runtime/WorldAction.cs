using Og.Core.Definitions;
using Og.Core.Model;

namespace Og.Core.Runtime;

/// <summary>
/// The verbs an interaction can perform. Named constants rather than an enum so the
/// set is not closed at compile time and handlers can be added.
/// </summary>
public static class ActionKind
{
    public const string Describe = "describe";
    public const string AddItems = "additems";
    public const string RemoveItems = "removeitems";
    public const string ChangeLocation = "changelocation";
    public const string SetLocationDescription = "setlocationdescription";
    public const string EnableInteraction = "enableinteraction";
    public const string DisableInteraction = "disableinteraction";
    public const string AddCondition = "addcondition";
    public const string RemoveCondition = "removecondition";

    /// <summary>
    /// Says something to everyone in the world except the one who caused it.
    /// World-scale events are otherwise invisible to anyone not in the room:
    /// the place changes under them and nothing tells them so.
    /// </summary>
    public const string Announce = "announce";
}

/// <summary>Everything an interaction can be told to do.</summary>
public static class ActionKinds
{
    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ActionKind.Describe, ActionKind.AddItems, ActionKind.RemoveItems,
        ActionKind.ChangeLocation, ActionKind.SetLocationDescription,
        ActionKind.EnableInteraction, ActionKind.DisableInteraction,
        ActionKind.AddCondition, ActionKind.RemoveCondition,
        ActionKind.Announce
    };
}

public sealed class WorldAction(ActionDefinition definition)
{
    public string Kind { get; } = definition.Action.Trim().ToLowerInvariant();

    /// <summary>As it was written, for saying back to whoever wrote it.</summary>
    public string AsWritten { get; } = definition.Action.Trim();
    public string[] Parameters { get; } = definition.Parameters;
    public Scope Scope { get; } = definition.Scope;

    /// <summary>Keys the file set here that the loader threw away. Carried for the validator.</summary>
    public string[] UnknownKeys { get; } = definition.UnknownKeys;

    public string FirstParameter => Parameters.Length > 0 ? Parameters[0] : "";

    public int FirstParameterAsInt =>
        Parameters.Length > 0 && int.TryParse(Parameters[0], out var value) ? value : 0;
}
