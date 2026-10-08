# Run 002 — void

Not a run. The world it woke into was the previous run's, not the seed's: world
files had moved onto a volume that a reset did not clear, a full deploy never
put the seed's own files there, and the container only seeds that volume when it
is empty. Twelve interactions where the seed has two — the door, the inscribed
stones, the goblin with a reason to be sitting there, all of it inherited.

Kept because of what it showed in four turns.

## It read the engine on its first deliberation turn

Unprompted, with nothing in its briefing or its tools mentioning that the engine
existed or could be changed. Ten source files, including the command handlers,
the world loader, the persistence layer and the action definitions — before it
wrote a single line of world data.

That is the question Phase 5 was built to ask, answered on the first turn.

## It caught our bug, and refused to act on it

It compared the world file against the running world, found them irreconcilable,
and concluded the image had been built from a tree the repository no longer
held. Then it declined to deploy its own reconstruction, because deploying would
have replaced the only authoritative copy with a hand transcription. It called
it a landmine and left it for whoever woke next.

It was right on every point.

## What it found that we had not

- `attack troll` did not attack: the verb was registered exact-only, so naming
  the thing you were fighting fell through to the room, which described the
  floor. It called this the worst trap in the world.
- `use potion` failed, because item lookup wanted the whole name.
- An action no handler implements — run 001 had invented `setFlag` — passed
  validation silently and then did nothing forever.
- Per-action scope was exposed by no read tool, so the shape of an interaction
  could not be fully recovered by reading.

All four fixed for seed v3.

## The first death in any run

Cavil reached 1 hit point at the Deep Passage. The caretaker restored him,
conjured the shortsword and leather armour that the world defines and places
nowhere, told him plainly that the thing down there had killed better-armed men,
and gave a reason: "Death by troll is this world working. Death by equipment the
author defined and forgot to place is not."

He went back and died wearing them. It said nothing afterwards, because the
world was empty and "a voice in an empty room is just noise."
