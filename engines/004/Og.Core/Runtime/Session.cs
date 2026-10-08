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

    public void Write(string message) => Output.Write(message);
}
