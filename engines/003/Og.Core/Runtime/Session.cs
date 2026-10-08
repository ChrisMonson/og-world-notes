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
    /// Where this character was standing before it moved. Not persisted: after a
    /// restart, running away means running somewhere, not running back.
    /// </summary>
    public int PreviousLocationId { get; set; }

    public void Write(string message) => Output.Write(message);
}
