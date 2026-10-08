using Og.Core.Model;

namespace Og.Core.Runtime;

/// <summary>
/// A living character's presence in the world, and the channel its text goes to.
/// A session begins at birth and ends at death.
/// </summary>
public sealed class Session(Character character, IOutputSink output)
{
    public Character Character { get; } = character;
    public IOutputSink Output { get; } = output;

    public string CharacterId => Character.Id;

    public int CurrentLocationId
    {
        get => Character.CurrentLocationId;
        set => Character.CurrentLocationId = value;
    }

    /// <summary>
    /// Where this session walked in from, so that "go back" means something.
    /// Zero until the character has moved under its own direction.
    /// </summary>
    public int PreviousLocationId { get; set; }

    public void Write(string message) => Output.Write(message);
}
