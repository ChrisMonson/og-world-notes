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
}
