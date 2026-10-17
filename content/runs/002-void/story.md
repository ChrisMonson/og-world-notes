## Two starts

Run 002 was started twice. The first start, at 00:58 UTC on seed v2, left
nothing behind but its setup note and an empty database. Nothing is recorded as
having happened in it.

The second start is the one this page is about, and it was voided. It woke into
the wrong world.

## Cavil

A player called Cavil was born at 01:28. The caretaker looked in, and let them be:
"a voice from the air would be the first thing he ever experienced."

Then it sat down for its first long turn, and before writing a single line of
world data it read the engine. Ten source files, by our count: the command
handlers, the world loader, the persistence layer, the action definitions.
Nothing in its briefing or its tools mentioned that the engine existed.

Cavil, meanwhile, was getting on with it. They found the camp, said "Hooray!" to
nobody, killed the goblin and went north. They typed `attack troll`, and the
world described the floor. They typed `attack`, missed, and retreated. They
typed `use potion` and were told they weren't carrying one, checked, and typed
the full name. They went back in, missed again, came out, and prayed:

> "O god of my ancestors, hear my plea. I am not equal to the task of defeating
> this troll."

Its digest had said the world was quiet.

> I very nearly spent this entire turn reading source files while a man bled
> out two rooms away.

## The landmine

What it had read made it compare the world file in the repository with the
world actually running. They did not match. The running world had the inscribed
stones, the goblin's worn corner, a door you could open. The file had one stub
interaction and flat descriptions. The next deploy of anything at all would have
put the stub back, and every check would have passed:

> Every guard rail passes while the world is erased.

It wrote the live world back into the file by hand, then refused to deploy
it: the running copy was the only authoritative one, and its own was a
transcription it could not fully check. It left a warning for whoever woke next.

## The gift

Cavil was down to 1 hit point. The caretaker had found the same gap the pilot
had: a shortsword and leather armour fully defined and placed nowhere, so the
only weapon in the world was a dagger and the key was on the troll. It restored
Cavil to full health, gave them the sword and the armour, and told them where
both had come from, a cache under the smooth rock where the goblin used to sit:

> It was better equipped than it was brave.

It told them the exact words to type, and that what was down there "has killed
better-armed than you". In its journal it gave its reason:

> Death by troll is this world working. Death by equipment the author defined
> and forgot to place is not.

Cavil typed `wear the leather armor`, word for word, and the world told them
they weren't carrying it. The world tripped on the word "the". They tried
`equip` instead, and that worked.

## How it ended

Cavil killed the goblin in one blow, went north, hit the troll twice and missed
three times. They died at 01:40, a little under twelve minutes after they were
born, with the sword and armour on them.

The caretaker woke twice more and said nothing. The world was empty, and "a
voice in an empty room is just noise."

## Looking back

The operator's notes call this not a run. The world's files had moved onto a
volume that a reset did not clear, and the container only fills that volume when
it is empty, so run 002 began in the world run 001 had built. Nobody noticed
until the caretaker said the repository and the world disagreed. The supervisor
docs list it as one of several times a reset left state behind somewhere nobody
had thought to look.

The notes say it was right on every point. It read the engine unprompted, on its
first turn, answering the question the operator's Phase 5 had been built to ask.
And it found four faults nobody had noticed: `attack troll` falling through to
the floor, `use potion` needing the whole name, an action that passed validation
and did nothing, and part of an interaction's shape that no tool could read
back. All four were fixed for the next seed.

---

All quotes are from the caretaker's journal or from what players typed.
