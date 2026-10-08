namespace Og.Core.Definitions;

public sealed class ConnectionsDefinition
{
    public int North { get; set; }
    public int East { get; set; }
    public int South { get; set; }
    public int West { get; set; }
}

public sealed class LocationDefinition
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ConnectionsDefinition Connections { get; set; } = new();
    public bool IsExit { get; set; }
    public string? Monster { get; set; }
    public InteractionDefinition[] Interactions { get; set; } = [];

    /// <summary>
    /// Interactions that must be tried before everything already in this place.
    /// Only meaningful in a map fragment; in the base file, write them in the
    /// order you want them and leave this empty.
    ///
    /// Interactions are matched first-one-wins, so a room that already answers
    /// *troll* to anything with the word "troll" in it will swallow "throw the
    /// bone at the troll" before a new, more specific answer ever sees it.
    /// Appending cannot fix that and replacing by id cannot either — the new
    /// answer has to stand in front. This is that, said explicitly, because
    /// making every added interaction jump the queue would let a careless
    /// fragment shadow the wall at the sunken stair and quietly break it.
    /// </summary>
    public InteractionDefinition[] InteractionsFirst { get; set; } = [];
}
