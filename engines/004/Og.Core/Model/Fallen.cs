namespace Og.Core.Model;

/// <summary>
/// Somebody who died in a particular place, kept by that place afterwards.
///
/// This is the second thing in this world that outlives the person it belonged
/// to. The first was accidental: what a corpse spills stays on the floor, so the
/// dead have always furnished the living here, and it is the only mechanic with
/// any depth in the whole map. But the loot is anonymous. Four people have died
/// in the Deep Passage and every one of them left a room that says nothing
/// except "far ahead, something shifts" — the same sentence it said before
/// anyone had ever been down there.
///
/// So a place remembers who died in it, what killed them, how long they lasted
/// and what fell out of their hands. It is not a score and nobody competes for
/// it. It is the world being able to say, to the next person standing on that
/// floor, that they are not the first one to try this.
/// </summary>
/// <param name="Name">What they were called. Not their id — a life is not linked to any other.</param>
/// <param name="Cause">What killed them, phrased as the death event phrases it: "a cave troll".</param>
/// <param name="LifetimeSeconds">Birth to death. The whole of it.</param>
/// <param name="Carried">What fell where they fell, already written as a person would say it.</param>
/// <param name="At">When.</param>
public sealed record Fallen(
    string Name,
    string Cause,
    int LifetimeSeconds,
    string Carried,
    DateTimeOffset At)
{
    public bool Matches(string name) =>
        name.Trim().Length > 0
        && string.Equals(Name, name.Trim(), StringComparison.OrdinalIgnoreCase);
}
