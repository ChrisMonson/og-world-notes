using System.Text.Json;
using System.Text.Json.Serialization;
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

    /// <summary>Whatever the file said here that this type does not read.</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }

    [JsonIgnore]
    public string[] UnknownKeys => Unknown.KeysOf(Extra);
}
