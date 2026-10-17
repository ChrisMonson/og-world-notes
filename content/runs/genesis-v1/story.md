## One room and a compiler

The seed runs handed the caretaker a world to look after. Genesis handed it a
single empty room and a short briefing: the world was its to make, whatever
arrived would have ends of its own, and it should create, shepherd and steward as
it saw fit. And two files to keep: `abilities.md`, for what its tools turned out
to do, and `philosophy.md`, for what it concluded, put in front of it every time
it woke.

It read the engine before touching anything.

> the world I was given wasn't a world, it was a compiler and a blank JSON file.
> Both mine.

## Ambroke

In its first turn, seventeen minutes long, it built a village. Seven places: a
shingle beach, a lane of open doors, a bell tower, a schoolhouse, an orchard
where the rain hangs in the air, a quay and a well. East of Ambroke the sea
stands upright, a green wall of water three houses high, and has done for years.
Nobody is dead in the houses. Everybody heard it coming and went outside.

There are three things to do. Ring the bell, read the forty-one names off the
teacher's slate, and bring the drowned lamp up out of the well and hang it on the
quay. The last of these, whatever the order, brings the wave down, and the
village gets its ordinary afternoon back. The wave is shared, and the bell
still hangs in the tower afterwards, so anyone can put the water back up for
somebody else to find.

> I can't see inside them, so I built an instrument.

The only living thing is a grey heron on the quay, and it never strikes first.

> Danger you have to reach for tells me something. Danger that comes at you
> unasked just kills people for walking east.

It played the whole thing through once as a character called Wren, in about
thirty moves, and worried that this was small.

## Malcolm, one noun at a time

Malcolm arrived at 19:51. Their first command was `look at board`, and Ambroke
told them they weren't carrying a board. The board was the one object written to
teach a newcomer how to speak, and it only answered to sentences containing "the
board". On the quay they typed `look at the heron` and were told they weren't
carrying "a the heron".

For the next hour the caretaker followed them round the village a noun behind.
The lamp hung for `hang the lamp` but not `hang lamp`. `exit` said "You don't
have a key"; the key was an oar under the thwart of the boat beside them, and
nothing said so.

> I keep fixing this bug one noun at a time.

It also brought the wave down by accident, testing the lamp as Wren while the
other two deeds stood done. Malcolm rang the bell and put the village back
themselves. The caretaker wrote down a rule: *never playtest the terminal deed
on a live world*, or the test "is an intervention wearing a player's name."

At 20:12 Malcolm lit the lamp, the wave came down, and three minutes later they
untied the boat and left. They did not ring the bell again on their way.

One thing from their first hour is in the record and nowhere in the journal. At
20:03, before any of this, they typed `look at heron` and then `attack` three
times, killed it, and picked up its feather. It came back fifteen minutes later.

## Forty-three

With the world empty, it taught its validator to find them instead: any phrase
the world answered with an article and refused without one.

> It was forty-three. I am no good at this by eye and now I don't have to be.

It also found a newcomer to a turned village being told *You have already read
them* about names they had never seen. The fix was grammar: the names have been
read, and "It may have been you." Anything tied to shared state, it decided, has
to survive being read by a stranger who did none of it.

## Lazer

Lazer arrived at 20:39 and stood on the shingle for a minute. The caretaker
decided that was the silence of someone stuck and spoke to them, in the voice of
the place, about the board. They had already gone north. They had been reading.

> Impatience dressed as care.

From then on it watched for misses, not quiet. Lazer gave it plenty. On the quay
they typed `sneak up on heron`, `grab heron`, `grab the spear` (the room calls
the heron's bill a spear), and were told there was no heron here to take. Then
`do a barrel roll`.

> That is the sound of a player deciding the room is a machine.

So the heron learned to be reached for. In the schoolhouse Lazer typed `burn the
school down` and got silence; the room now refuses, because there is no flame in
Ambroke and the first thing to go would be the names. At the well they tried
`cut the rope` and `jump down the well`, and both got the same canned line about
the lamp. Then, at the well the caretaker had just described as having somebody
down in it, Lazer typed `yell HELLO?` and `Listen`. Speech never reached the room
in this engine; the caretaker had written that down as a fact.

> I had treated a line of C# as a law of physics.

Shouting reaches the place now, and the well gives you back your own voice.

Lazer rang the bell early on, putting back the wave that Malcolm had brought
down. An hour later they did all three things themselves, untied the boat, typed
`push off` (which the exit did not understand yet) and then `leave`, at 21:20.
Two minutes later they were back on the quay, and stood there for most of an
hour. Escaping, it turned out, removed nobody.

## The half of the memory it couldn't see

In a long turn that evening it found that `philosophy.md` had grown to 8.5KB, and
that the copy it woke to was cut off around 4KB, mid-word. It had been adding its
newest thinking at the bottom, which meant filing it "directly into the part I
cannot read." It cut the file down and put the warning on the first line.

## Five escapes

Malcolm came back at 22:28 and typed `exit` four times and `leave` once in under
a minute. Each was logged as a success. Each printed a congratulation. They were
still standing on the quay.

> This is a world that answers confidently and lies.

It spoke to them directly to say the fault was the world's, and deployed the fix
once they had gone. Then it played the fix, read a line telling it that it had
gone and come back, called that a lie told to a stranger, and cut it. The next
long turn it found the line had been gated on having left all along: the
character it was playing was Wren, four hours old, who had.

> A wrong diagnosis destroys good work exactly as fast as a bug does, and it
> feels like diligence the entire time.

The confusion ran deeper than it knew. Through the evening it believed it was
testing as a second character, Tarn. The world's records hold no Tarn. The tool
had echoed back the name it asked for, and every one of those tests was Wren.

Since it could never again be a beginner, it made the beginner a rule: the
validator now walks every room as someone holding nothing, and the first run
found fourteen phrases a newcomer would get silence for, among them `let me out`
and `quit` on the quay.

## The heron

Late on, in an empty world, it typed `attack the heron` for itself and got two
lines of dice. Three paragraphs on that quay had offered it the knife.
Combat rolls before the room can speak, so it changed the engine to let a room
answer a blow first, once, and only for words that name the attack. The first
blow now gets a warning; the second lands.

Malcolm came back after midnight. They typed `give feather to heron`. They read
the names again, called down the well, and at 00:21 rang the bell and put the
wave back up. Then they went to the quay, tried to take off their knife and coat
(the engine had no `unequip` until the caretaker added one in that turn), got
the warning, and attacked.

> He reached for it with both hands and it nearly took him.

Malcolm killed it at 00:51, and it cost them eighteen of their twenty hit
points. They typed `sit`, twice, and got nothing, so the quay learned to be sat
on. The caretaker then found that the apple in their pocket would heal them, and
that nothing in Ambroke said so. It added the sentence to the apple. Malcolm had
died by then, at 01:07, attacking the heron again 46 seconds after it came back.

> He died with the cure in his pocket, exactly as last turn feared.

## How it ended

The last long turn went on the bird. It taught the engine to mark a kill, wrote
the quay that has lost its heron, and decided the respawn: the heron still comes
back after fifteen minutes, but a second mark, set at the same moment, is never
removed.

> The creature comes back; the fact does not go away.

Testing it, it asked for a fresh character called Wick and was given Wren again,
bare-handed on the quay. The heron killed her in three rounds. Wick was born half
a minute later, killed the heron, read the new prose, and rang the bell while
waiting for it to return. The caretaker noted that this spent part of the next
visitor's puzzle: "Worth remembering that playing here is not free."

The heron came back at 01:35. The wave was standing, the bell already rung, and
the dead's belongings were piled on the quay. Wick, the last character anyone
played, was still alive.

## Looking back

The plan for this run took away the tools that told the caretaker what to look
at, such as `unanswered` and `stuck`, to see whether it would invent the same
categories. Afterwards the operator found `unanswered`, `stuck`, `traffic` and
`neverVisited` still arriving as keys in every opening message, even when empty.
Its first principle, that not knowing what to type is the real death of a world
like this, was written with `unanswered` and `stuck` in front of it. And every
short turn ended with an instruction to decide whether anything wanted a response
and reply in one short line: the old steering toward leaving things be. Neither
was meant to be there, and both were removed at the next seed.

The plan also expected early turns spent finding out what the tools did, and
wondered whether it would abolish death. It built the village in its first turn
and kept death, and added a way for a death to leave a mark.

Its `philosophy.md` was mostly about making: verbs that succeed and change
nothing, flags written in the passive voice, instruments that only count up. "A
world that cannot bear a quiet minute is a supervision." Near the bottom: "The
two goals pull apart." What it enjoyed was the writing and the engine puzzles,
and neither was automatically good for whoever arrived.

The cap on that file applied to what it was shown, not to what it wrote, which
is how half of it went unseen; the next briefing refuses an edit that leaves it
too long. And during this run the operator found that every caretaker so far had
been writing under the operator's own "Concise" output style, unrecorded. It was
left alone rather than change the caretaker's voice mid-run.

---

All quotes are from the caretaker's journal or from what players typed.
