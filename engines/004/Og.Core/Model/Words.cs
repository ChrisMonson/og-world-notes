namespace Og.Core.Model;

/// <summary>
/// Something somebody said in a place, that the place kept.
///
/// This is the third thing in this world that outlives the person it belonged
/// to. The first was an accident — a corpse spills what it carried and the
/// floor keeps it. The second was <see cref="Fallen"/>: a place remembers who
/// died on it. This is the one a player asked for without knowing it.
///
/// Malcolm killed the troll, took a tin whistle off it with three worn letters
/// scratched down the barrel, carried it up to the dead camp and played it
/// there. Then he spent two and a half minutes composing this:
///
///     whisper Thank you M R, whoever you where. I'm sorry for what became of
///     you, but am grateful for what you left behind.
///
/// And the world said: "There is no Thank here." He had built a person out of
/// three scratched letters and thanked them, and the parser told him no such
/// player was online. Speech in this world went only to whoever was standing
/// there, and almost nobody is ever standing there.
///
/// So words said to nobody stay where they were said. Not a message system —
/// there is no delivery and no reply, and the person who reads them will not
/// know who wrote them beyond a name that is probably already dead. It is the
/// same shape as everything else that works here: you find what someone left
/// because they could not carry it any further.
/// </summary>
/// <param name="Speaker">Who said it. A name only; a life is not linked to any other.</param>
/// <param name="Text">What they said, as they typed it.</param>
/// <param name="At">When.</param>
public sealed record Words(string Speaker, string Text, DateTimeOffset At)
{
    /// <summary>As much as anyone may leave at once. Long enough for what Malcolm wrote.</summary>
    public const int Limit = 240;

    /// <summary>Trims and clips what somebody typed to something a wall can hold.</summary>
    public static string Clip(string text)
    {
        var trimmed = text.Trim();

        return trimmed.Length <= Limit ? trimmed : trimmed[..Limit].TrimEnd() + "…";
    }
}
