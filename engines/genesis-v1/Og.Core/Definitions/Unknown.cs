using System.Text.Json;

namespace Og.Core.Definitions;

/// <summary>
/// System.Text.Json drops a property it has never heard of without a word. That
/// makes a mistyped key the one mistake in this whole world that cannot be seen:
/// the file says what the author meant, the loader ignores it, and the world runs
/// as though the line had never been written. A misspelled 'if' does not fail
/// loudly — it deletes the gate, and the interaction fires for everyone forever.
///
/// Every authored type therefore keeps what it did not understand, so the
/// validator can hand it back.
/// </summary>
public static class Unknown
{
    /// <summary>
    /// The keys an author wrote that the engine does not read. A key beginning
    /// with an underscore is deliberately left alone: there is nowhere else in
    /// this format to write a note to the next person, and refusing notes would
    /// only teach people to delete their reasons.
    /// </summary>
    public static string[] KeysOf(Dictionary<string, JsonElement>? extra) =>
        extra is null
            ? []
            : [.. extra.Keys.Where(key => !key.StartsWith('_')).Order()];
}
