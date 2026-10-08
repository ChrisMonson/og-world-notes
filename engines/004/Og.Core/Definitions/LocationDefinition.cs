namespace Og.Core.Definitions;

public sealed class ConnectionsDefinition
{
    public int North { get; set; }
    public int East { get; set; }
    public int South { get; set; }
    public int West { get; set; }
}

/// <summary>
/// One way a place can read, and what has to be true for it to read that way.
///
/// Every room in this world has said exactly one sentence since it was written,
/// whatever has happened in it. The Deep Passage opens with "far ahead,
/// something shifts" over the troll's corpse. That is a lie, and it is the same
/// lie a room tells when the thing it was written around has been killed, taken
/// from, fed, or buried under five people's worth of dead.
///
/// The conditions are the ones interactions already use, so a place can see
/// exactly as much of the world as the things inside it can: what is alive here,
/// what you are carrying, who has died on this floor, how badly you are hurt.
/// </summary>
public sealed class DescriptionVariantDefinition
{
    public string[] If { get; set; } = [];
    public string[] IfNot { get; set; } = [];
    public string Description { get; set; } = "";
}

public sealed class LocationDefinition
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";

    /// <summary>
    /// Alternative descriptions, in order. The first whose conditions hold is
    /// what the place says; if none do, it says <see cref="Description"/>.
    /// </summary>
    public DescriptionVariantDefinition[] DescriptionWhen { get; set; } = [];

    public ConnectionsDefinition Connections { get; set; } = new();
    public bool IsExit { get; set; }
    public string? Monster { get; set; }
    public InteractionDefinition[] Interactions { get; set; } = [];
}
