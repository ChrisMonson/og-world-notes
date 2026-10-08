namespace Og.Core.Runtime;

public delegate CommandResult CommandHandler(GameEngine engine, Session session, string argument);

/// <summary>
/// The verbs the engine itself knows. Registered rather than hardcoded, so new
/// ones can be added without editing this file.
/// </summary>
public sealed class CommandRegistry
{
    private readonly Dictionary<string, CommandHandler> _exact = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(string Prefix, CommandHandler Handler)> _prefixes = [];

    /// <summary>
    /// Verbs never reached by guesswork. The four directions are one letter away
    /// from words that mean something else entirely — "rest" is not "west" — and
    /// moving somebody who did not ask to move is worse than not understanding them.
    /// Running is a move as well, and "flea" is not "flee".
    ///
    /// Retracing is a move too, and it is the one most easily arrived at by
    /// accident: "back" is a single slip from "pack", "lack", "hack" and "bank",
    /// and a person who typed one of those and found themselves in the previous
    /// room would have no idea what they had done.
    ///
    /// The door words are here for the same reason, only more so: ending a life
    /// nobody asked to end is the worst thing a guess could do.
    /// </summary>
    private static readonly HashSet<string> NeverCorrectedTo =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "north", "south", "east", "west", "flee", "retreat", "withdraw",
            "back", "return", "backtrack", "retrace",
            "exit", "escape", "leave"
        };

    public IReadOnlyCollection<string> Verbs => _exact.Keys;

    public void RegisterExact(CommandHandler handler, params string[] forms)
    {
        foreach (var form in forms)
        {
            _exact[form] = handler;
        }
    }

    public void RegisterPrefix(CommandHandler handler, params string[] prefixes)
    {
        foreach (var prefix in prefixes)
        {
            _prefixes.Add((prefix, handler));
        }
    }

    /// <summary>
    /// Matches on the normalised command but hands the handler the argument as the
    /// player actually typed it, so speech keeps its punctuation and capitals.
    /// </summary>
    public CommandResult Execute(GameEngine engine, Session session, string command, string raw)
    {
        if (_exact.TryGetValue(command, out var exactHandler))
        {
            return exactHandler(engine, session, "");
        }

        foreach (var (prefix, handler) in _prefixes.OrderByDescending(entry => entry.Prefix.Length))
        {
            if (!command.StartsWith(prefix + " ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var argument = raw.Length > prefix.Length && raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? raw[prefix.Length..].Trim()
                : command[(prefix.Length + 1)..].Trim();

            return handler(engine, session, argument);
        }

        // A last chance for a slipped key. One word, one edit, and only when a
        // single verb is that close: "killl" is not a mystery, and answering it
        // with "Nothing happens" teaches people that the world is deaf.
        if (command.Length >= 4 && !command.Contains(' '))
        {
            var candidates = _exact.Keys.Where(verb =>
                verb.Length >= 4 && !verb.Contains(' ') && !NeverCorrectedTo.Contains(verb));

            if (TriggerMatcher.Nearest(command, candidates, 1) is { } corrected)
            {
                return _exact[corrected](engine, session, "");
            }
        }

        return CommandResult.NotHandled;
    }
}
