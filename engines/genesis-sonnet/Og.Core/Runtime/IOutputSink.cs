namespace Og.Core.Runtime;

/// <summary>
/// Where a session's text goes. The console runner writes to stdout; the server
/// writes to that player's socket. Divine speech arrives through the same channel,
/// unprompted, so clients must render text outside the command/response cycle.
/// </summary>
public interface IOutputSink
{
    void Write(string message);
}

public sealed class NullOutputSink : IOutputSink
{
    public void Write(string message) { }
}

public sealed class CollectingOutputSink : IOutputSink
{
    public List<string> Lines { get; } = [];

    public void Write(string message) => Lines.Add(message);

    public string Text => string.Join("\n", Lines);

    public void Clear() => Lines.Clear();
}
