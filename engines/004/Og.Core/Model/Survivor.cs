namespace Og.Core.Model;

/// <summary>
/// Somebody who went out through the door alive, kept by the door afterwards.
///
/// This is the fourth thing in this world that outlives the person it belonged
/// to, and the first one that is not a kind of wreckage. The others are all
/// what is left when something goes wrong: a corpse spills its pockets onto the
/// floor (<see cref="Item"/> on the ground), a place remembers who died on it
/// (<see cref="Fallen"/>), and what somebody said with nobody there to hear it
/// stays on the wall (<see cref="Words"/>). Every permanent mark in this cave
/// was a failure.
///
/// Meanwhile twelve people have got out. Rook twice, Tanril after an hour and
/// three quarters, Wick in twenty-five seconds flat, Malcolm carrying most of
/// the movable world, and Simon — who killed the goblin, walked away, came back
/// forty-five minutes later after reading a stranger's carved note, fed it
/// instead, and said "thank you!" to an empty room. Not one of them left a
/// trace. The door they opened says exactly what it said before anybody had
/// ever opened it.
///
/// So the door keeps them: a name, how long they were down here, and what they
/// walked out carrying. It is not a scoreboard — there is no ranking and
/// nothing to beat — it is the world being able to tell the next person
/// standing in front of that lock that the lock opens, that people have gone
/// through it, and roughly what it cost them. Everything else this world
/// remembers is a reason to be careful. This is the one reason to keep going.
/// </summary>
/// <param name="Name">What they were called. Not their id — a life is not linked to any other.</param>
/// <param name="LifetimeSeconds">Birth to the door. The whole of it.</param>
/// <param name="Carried">What went out with them, already written as a person would say it.</param>
/// <param name="At">When.</param>
public sealed record Survivor(
    string Name,
    int LifetimeSeconds,
    string Carried,
    DateTimeOffset At)
{
    public bool Matches(string name) =>
        name.Trim().Length > 0
        && string.Equals(Name, name.Trim(), StringComparison.OrdinalIgnoreCase);
}
