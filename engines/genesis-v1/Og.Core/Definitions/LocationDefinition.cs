using System.Text.Json;
using System.Text.Json.Serialization;

namespace Og.Core.Definitions;

public sealed class ConnectionsDefinition
{
    public int North { get; set; }
    public int East { get; set; }
    public int South { get; set; }
    public int West { get; set; }

    /// <summary>Whatever the file said here that this type does not read.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    [JsonIgnore]
    public string[] UnknownKeys => Unknown.KeysOf(Extra);
}

public sealed class LocationDefinition
{
    public int Index { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ConnectionsDefinition Connections { get; set; } = new();
    public bool IsExit { get; set; }

    /// <summary>
    /// What a player reads at the instant they leave the world by this exit.
    /// The engine's own "You have escaped! Congratulations!" is a dungeon's
    /// voice, and it lands on the last sentence anybody will ever read here.
    /// A place that has spent an hour in its own register should be allowed to
    /// finish in it. Null falls back to the engine's line.
    /// </summary>
    public string? Farewell { get; set; }

    /// <summary>
    /// Conditions that must all hold before this exit will let anybody out.
    /// When one is missing the leave-command goes Futile and falls through, so
    /// the place gets to refuse in its own words instead of the engine's.
    /// The quay needs this: holding the oar was the only test, so a player who
    /// untied the boat and then rang the bell again could row out from under a
    /// standing wall of water the room had just told them nobody leaves under.
    /// </summary>
    public string[] ExitRequires { get; set; } = [];

    public string? Monster { get; set; }
    public InteractionDefinition[] Interactions { get; set; } = [];

    /// <summary>Whatever the file said here that this type does not read.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    [JsonIgnore]
    public string[] UnknownKeys => Unknown.KeysOf(Extra);
}
