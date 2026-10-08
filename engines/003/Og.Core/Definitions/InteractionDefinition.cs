using Og.Core.Model;

namespace Og.Core.Definitions;

public sealed class InteractionDefinition
{
    public string? InteractionId { get; set; }
    public string[] Triggers { get; set; } = [];
    public ActionDefinition[] Actions { get; set; } = [];
    public string[] If { get; set; } = [];
    public string[] IfNot { get; set; } = [];

    /// <summary>
    /// Items that must be in the player's hands for this to fire, and items that
    /// must not be.
    ///
    /// Conditions are flags: they say what has happened. These say what is being
    /// carried, which the world file had no way of asking about before, so an
    /// interaction triggered on the word "spoon" fired just as happily for
    /// somebody who did not have one — and took nothing, and gave a flask, and
    /// wrote a spoon they never owned onto the cairn under their name.
    /// </summary>
    public string[] IfCarrying { get; set; } = [];
    public string[] IfNotCarrying { get; set; } = [];

    public bool Disabled { get; set; }

    /// <summary>
    /// World: enabling or disabling affects everyone (a lever stays pulled).
    /// Player: each player has their own enabled state (a personal quest gate).
    /// </summary>
    public Scope Scope { get; set; } = Scope.World;

    /// <summary>Whether the interaction fires only once per player.</summary>
    public bool Once { get; set; }
}
