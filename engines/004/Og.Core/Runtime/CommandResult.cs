namespace Og.Core.Runtime;

public enum CommandStatus
{
    /// <summary>No handler wanted it.</summary>
    NotHandled,

    /// <summary>A handler did something. The command is spent.</summary>
    Done,

    /// <summary>
    /// A handler recognised the words and could do nothing with them: nothing to
    /// take, nothing to attack, nothing of that name being carried.
    ///
    /// This is not the same as being handled. The command carries on to the place
    /// the player is standing in, which may well have an answer the engine does
    /// not — someone typing "take belongings" in a room that describes belongings
    /// should reach that room rather than being told they are carrying nothing.
    /// </summary>
    Futile
}

/// <param name="AlreadyAnswered">
/// Set when the handler has already written something and is passing the
/// sentence on anyway. The room still gets its turn, but if the room has
/// nothing to say the player is told nothing further and the command is not
/// recorded as unanswered — because it was answered.
/// </param>
public readonly record struct CommandResult(
    CommandStatus Status,
    string? Message = null,
    bool AlreadyAnswered = false)
{
    public static readonly CommandResult NotHandled = new(CommandStatus.NotHandled);

    public static CommandResult Done() => new(CommandStatus.Done);

    /// <summary>
    /// Nothing came of it. The message is held back rather than written, and is
    /// used only if the place has nothing to say either.
    /// </summary>
    public static CommandResult Futile(string message) => new(CommandStatus.Futile, message);

    /// <summary>
    /// The engine answered, and the place should answer too.
    ///
    /// There is a whole class of thing in this world that the engine can only
    /// describe shallowly and the room can describe properly. A cave troll is
    /// two lines of monster definition to the engine; to the passage it stands
    /// in, it is slow enough that you get the first blow, it does not mend, it
    /// has a key and a dead man's whistle on the cord at its throat, and it is
    /// hungry. Every one of those facts is load-bearing, and every one of them
    /// lived in the room.
    ///
    /// Before this, "look at the troll" succeeded — and succeeding is what shut
    /// the room up, because a Done command never carries on. The natural way of
    /// asking got the shallow answer and the awkward way (bare "troll") got the
    /// real one. Now the engine says its piece and stands aside.
    /// </summary>
    public static CommandResult AndTheRoomAsWell() =>
        new(CommandStatus.Futile, null, AlreadyAnswered: true);
}
