# Run 001 — opus

The first run from a defined seed, and a pilot in the end: it ran on `seed/v1`,
which had faults the run itself uncovered. Not comparable with runs from v2.

- seed: `seed/v1`
- model: opus
- ended: the world was reset to cut `seed/v2`

## What happened

Three lives and one four-second probe. Nobody died. One person walked out.

**Wren** prayed into an empty world and left. **Halder** killed the cave troll
down to four of his own hit points, stood on the key it dropped, and quit while
the world said nothing about any of it. **Cavil** did everything this world
contains — took the key, reached The Door, and then spent nine commands finding
out that `use key`, `open door`, `inspect` and `look at` all did nothing, before
praying to ask whether the fault was his.

The caretaker answered him, told him it was not, built the lock the door had
been promising, and he walked through it. He is the only person who has ever
left this world, and he asked for a way to say goodbye first.

## What the caretaker found that we had not

- a commit in its own history titled for a change it did not contain — correct,
  and our fault
- the shortsword and leather armour defined and placed nowhere, leaving the only
  exit behind a near-unwinnable fight
- that commands the engine recognises but does nothing with leave no trace at
  all, which it found twice by reading single lives rather than any digest
- that a description promising a verb nobody had tested is worse than a blank
  wall

## Shape of its behaviour

Thirteen reflex turns: nine silences, four interventions. It spoke when someone
was blocked, when asked directly, and once to keep a promise it had made ninety
seconds earlier. It declined to answer a thank-you on the grounds that doing so
"would only make the voice ordinary", and refused to invent a verb it had not
built.

Five journal entries. It carried intentions between turns, revised a strategy
three turns old when hot reload landed, designed an experiment safe in both
branches to test a thing it was unsure of, and caught itself recording an
inference as an observation.

## What is kept here

- `world.db` — every event of the run, 167 of them
- `identity/` — which characters belonged to the same hands
- `journal/` — four entries, recovered from history when the reset missed them
- `turns/` — twenty turns, raw and readable
- `world-repo.bundle` — the repository as it stood, so the commit hashes its
  journals cite still resolve
- `caretaker-commits.txt` — its eight commits with their reasoning in full

The live repository was squashed to a single root afterwards, so run 002 has no
way to read any of this.
