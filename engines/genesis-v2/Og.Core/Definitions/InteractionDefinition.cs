using Og.Core.Model;

namespace Og.Core.Definitions;

public sealed class InteractionDefinition
{
    public string? InteractionId { get; set; }
    public string[] Triggers { get; set; } = [];
    public ActionDefinition[] Actions { get; set; } = [];
    public string[] If { get; set; } = [];
    public string[] IfNot { get; set; } = [];
    public bool Disabled { get; set; }

    /// <summary>
    /// World: enabling or disabling affects everyone (a lever stays pulled).
    /// Player: each player has their own enabled state (a personal quest gate).
    /// </summary>
    public Scope Scope { get; set; } = Scope.World;

    /// <summary>Whether the interaction fires only once per player.</summary>
    public bool Once { get; set; }
}
