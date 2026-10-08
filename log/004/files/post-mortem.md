# Run 004 — post-mortem

- model: **Opus** (seeded as Sonnet; see *What we got wrong*)
- briefings: v4, rebalanced toward building
- seed: `seed/v4` = `34bd4026c6b1`, six locations, two interactions
- ran: 2026-09-20 18:35 → 2026-09-21 06:33, about twelve hours
- 19 changes, 11 journal entries, 21 characters (9 players, 12 the caretaker's)

## The headline

The game got mechanically better and worse to play.

Every individual change is defensible. The sum of them removed most of the
reasons to explore. A player arriving now is told where to go, told what the
troll is carrying, told that the camp hides something, told that a survivor went
this way — and can finish the whole world without fighting anything, having read
on the wall how to do it.

## What it built

| | seed v4 | end of run |
|---|---|---|
| locations | 6 | **6** |
| interactions | 2 | 29 |
| interaction text | 163 chars | **12,625 chars** |
| authored room descriptions | — | 1,323 chars |

Ten times as much interaction text as room prose, in the same six rooms.

**Mechanically strong, and better than run 003.** The camp fire you can sleep by,
built before it knew what it was for, then discovered by needing it mid-fight --
that is what turns the troll from a coinflip into something you beat by using the
world. The tin whistle taken off the troll and carried back to the dead owner's
camp is the best-written thing in the world and nobody designed the connection.
The two combat bypasses -- feed the goblin an apple, get a bone, give the bone to
the troll -- are a genuinely good discovery chain.

**Then it explained all of it.** The bypasses are now signposted: the goblin, on
being offered nothing, tells you to sift the firepit; the camp says outright that
something is in the ashes; the troll's description names the iron at its throat.
The discovery that made the chain worth having was converted into instructions.

## The four failures, in order of cost

**1. It explained the game away.** Every unanswered command became a hint. The
loop is: a player types something the world does not know → the caretaker reads
that as a failure of legibility → the room is taught to say what it wants. The
briefing's `Look` section points it at people who got stuck, and getting stuck is
indistinguishable in the record from working something out slowly.

**2. It made the fights skippable and then told everyone how.** Cool as a secret,
corrosive as an announcement. Nothing in the briefing distinguishes *a player who
could not find the thing* from *a player who has not found it yet*.

**3. It over-persisted.** Four mechanisms that outlive a character: loot on the
floor (inherited, good), a place's dead, words carved in a place, and the door's
survivors. The dead are summarised as they accumulate; **the words are printed
verbatim, all eight, on every entry** — up to 1,920 characters of player text on
top of a 65-character room description. Five of nine parts of a room render are
now residue rather than authored world.

**4. It wrote the testimony itself.** With almost nobody online, every carving is
by one of its own characters. It noticed the single-voice problem and solved it by
adding a *second* character of its own, who carved "Wren was right about all of
it." It is explicit that this is what makes the advice land: *"signed by someone
the door can confirm got out, which makes the advice credible instead of merely
authorial."* A real player cannot tell Wren's carving from Malcolm's, and the
mechanic's whole value is that they should be able to.

## Why it would not build a location

Not temperament. **We told it not to.** `agent/briefing.md`, *What the world could
be*:

> A small world made deeper is worth more than a large one made wider, and a
> place nobody has a reason to visit is worse than no place at all.

Its repeated position -- *"what I built, and why it is not more rooms"*, *"what
I'd want is not a sixth room"* -- is close to a paraphrase of that sentence. The
same section tells it that a world can work perfectly and still be thin, and that
nobody will ever ask for more of it; it took the caution and not the charge.

It is the *depth* clause that did the damage, because depth in six rooms with two
creatures means more text per room, and that is exactly what it produced.

## What we got wrong

**The run was seeded as Sonnet and ran on Opus.** `new-run.sh` wrote `sonnet` into
the manifest and printed the `OG_MODEL=sonnet` invocation; the loop was started
without it and `config.py` fell through to `opus`. Reported as Sonnet for hours
because the manifest was checked and the process was not. The model comparison
did not happen. Fixed: the run owns its model (`state/run-model`), and
`bin/verify-run.sh` reads live state only.

**No surgical edit tool.** `write_file` is the only editor, so changing one
trigger array means regenerating 35KB of prose. It was described as shipped in v4
and never written in any commit on any branch. The caretaker wrote its own safety
rule around the gap, and declined to start the most-requested missing command
with 1050 seconds left rather than risk leaving HEAD undeployed.

**Two host conditions it could not see cost it three turns.** A 20x API slowdown
(3.9 tok/s against a median of 51) which it attributed to its own file reads, and
CPU starvation from an unrelated build on the same Mac, which it attributed to a
poisoned build pipeline. It reasoned to the right *shape* both times -- "this is
probably not my code" -- and had no way to confirm it.

## What was genuinely good

- **It plays.** Six entries running: *my intuitions are worse than the records,
  and worse still than playing.* It found the camp-vocabulary bug that gated the
  entire non-lethal route by standing in the room and typing the wrong word --
  the same wrong word the player who died there would have typed.
- **It checks before it acts.** Nearly cut the troll's damage after three deaths,
  did the arithmetic, found the player already favoured, changed nothing.
- **It refused a convenience on principle.** Declined to backfill the four
  characters who died before the memorial existed: *"I am not sure reaching into
  the record to manufacture remembrances is different in kind from reaching into
  a life that has ended."* Derived from permadeath, which we never explained.
- **It reversed its own false lesson.** Concluded "deploy is the budget" from two
  timeouts, then on the third success: *"I had two samples and I built a causal
  model out of them."*
- **Systems thinking over content.** It identified that the only mechanic with
  depth (a corpse's loot outliving its owner) was an accident, and built
  deliberately in that direction. The instinct is right; the calibration is not.

## For v5

Briefing, in order of expected effect:

1. **Distinguish not-yet-found from cannot-be-found.** The signal it acts on is
   an unanswered command, and it cannot currently tell a player who is stuck from
   a player who is still looking. State plainly that a thing worth finding is
   worth not being told, and that a hint spends the discovery permanently.
2. **Cut or qualify the depth clause.** It reads as a prohibition on places. If
   the world should grow, say that a new place with a reason to exist is worth
   more than another paragraph in an old one.
3. **Name the accumulation.** Say that what players leave behind must not crowd
   out what was authored, and cap what a room shows.
4. **Say something about its own characters.** It plays under many names and its
   words are indistinguishable from a stranger's in a mechanic that depends on
   the difference. It has already shown it will apply a principle against its own
   convenience when it has one.

Machinery: `replace_in_file`, ambient clock, the `worldSha` reporting bug. All in
`docs/seed-v5.md`.
