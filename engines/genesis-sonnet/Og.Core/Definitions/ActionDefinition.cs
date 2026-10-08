using Og.Core.Model;

namespace Og.Core.Definitions;

public sealed class ActionDefinition
{
    public string Action { get; set; } = "";
    public string[] Parameters { get; set; } = [];

    /// <summary>
    /// For state-mutating actions: whether the effect is shared by all players
    /// or private to the acting player.
    /// </summary>
    public Scope Scope { get; set; } = Scope.World;
}
