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
    /// Closes wounds. The parameter is a number of hit points, or "full".
    /// A place that can mend someone should be able to say so without my hand in it.
    /// </summary>
    public const string Heal = "heal";

    /// <summary>
    /// Adds a line to a list the room keeps in its own description, under a
    /// heading: heading, line, and optionally how many lines to keep. "{name}" in
    /// the line becomes whoever did it.
    ///
    /// The same machinery the ground uses to remember its dead and the door uses
    /// to remember who walked out, handed to the world file. It is the only way
    /// anything a character chose to do can outlast that character, and the
    /// reason it exists is that three lives have passed through this valley and
    /// no two of them were ever in it on the same day.
    /// </summary>
    public const string Inscribe = "inscribe";
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
        ActionKind.Heal, ActionKind.Inscribe
    };
}

public sealed class WorldAction(ActionDefinition definition)
{
    public string Kind { get; } = definition.Action.Trim().ToLowerInvariant();

    /// <summary>As it was written, for saying back to whoever wrote it.</summary>
    public string AsWritten { get; } = definition.Action.Trim();
    public string[] Parameters { get; } = definition.Parameters;
    public Scope Scope { get; } = definition.Scope;

    public string FirstParameter => Parameters.Length > 0 ? Parameters[0] : "";

    public int FirstParameterAsInt =>
        Parameters.Length > 0 && int.TryParse(Parameters[0], out var value) ? value : 0;
}
