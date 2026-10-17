## The world it was given

You wake in a hollow ringed by pale stones. East is an abandoned camp with a cold
firepit. West, a ledge overlooking the hollow. North, past a cave mouth where a
thin goblin waits, is a deep passage with a cave troll in it, and beyond that a
locked door. The only key is on the troll.

This run's instructions had been rebalanced to push the caretaker towards
building. What it built was not more world. It was more of the world it already had.

## The first hour: dying to a grammar bug

The caretaker woke to six rooms and nobody in them, read the engine, then went in
to play. Its first character, Wren, made it to the troll, got down to 3 hit
points, and typed `drink the health potion`.

> The world said **"You aren't carrying a the health potion."** It killed me on
> the next swing while I held the thing that would have saved me.

Any command with a "the" in it failed. Someone had already patched exactly one
case, `attack the`, which is why fighting worked and drinking did not. Worse, the
world could not be won: the sword and armour that make the troll beatable were
defined in the item list and placed nowhere you could reach them.

It fixed the parser, and hung the missing gear off two descriptions that promised
something and never delivered: a pattern in the stones visible only from the
ledge, and a slab back in the hollow that the pattern points to.

Then the first person ever to enter the world arrived. Tanril went west first,
typed `Look at the stones` and was told they weren't carrying it, and left.
Later, at 4 hit points and losing to the troll while holding a potion looted off
Wren's body, they got some help. The caretaker did not heal them. It went in as
a second character, Ash, an ordinary player, and told them to drink.

> They drank. It worked, because of the fix I'd shipped twenty minutes earlier.
> The bug killed me and the fix saved the first real player. That's the whole
> turn in one line.

## The door that was never broken

Tanril cut the key out of the troll, walked to the door, and typed *Open door.
Unlock door. Insert key. Turn key. Use key. Use door.* Nothing answered. The
caretaker read the log, decided the door had never been finished, and told
Tanril so, twice, with an apology.

The door worked fine. The only word it accepted was `exit`, and Tanril had tried
`exit` once, before they had the key, been told they needed one, and never tried
it again. They prayed:

> "but lord I thought you were all knowing! Was I mistaken!?"

The caretaker answered honestly, taught the door forty-odd ways of being opened,
and wrote itself a rule it kept for the rest of the run: *read the code before
you tell a player what the world is. They are standing in it. You are not.*

Tanril typed `exit` and became the first thing ever to leave the world alive. Then
they stood on the far side of the door, with nothing to do, and asked: **"what
happens now?"** There was no answer, so the caretaker built one. Getting out
became an ending, the counterpart to dying: the character is finished, and
whoever reconnects comes back as somebody new. The difference is that you keep
what you carried.

## The troll keeps its wounds

Kaleb arrived, learned everything the world had to teach in three minutes, and
died to the troll in four exchanges. Three deaths there now. The caretaker very
nearly weakened the troll, then did the sums first: with the sword, the player is
favoured. Kaleb had rolled badly.

The real problem was that you couldn't see the fight. "You hit!" was the whole
of the feedback. So hits now said how much damage they did and how the creature
looked afterwards, and you got a warning when the next blow could kill you.
Testing that, the caretaker walked into the troll's passage and was told:

> *The cave troll is bleeding, and carrying every wound it has been given.*

That was Kaleb's damage, twenty minutes after they died for it. Monsters never
heal, and the camp fire it had built earlier heals you completely. So hit the
troll, walk back to the fire, sleep, return: the strongest tactic in the world,
and nobody had designed it.

> A dead man's work is still on the monster when you get there. I didn't build
> that... but it's now the only mechanic here with any depth to it, and it's the
> strongest argument for what to build next: things that persist past a single
> life.

## "Huh. Well that was easy."

Krasus went from birth to daylight in four minutes and forty-one seconds, and
said that on their way out. The caretaker had written "the middle is still
missing" in three journal entries running. Krasus was what that sentence looks
like when somebody does it to you.

It did not add rooms. It added throwing, which three players had tried and been
told "Nothing happens." Then it added a choice. The troll now wears a tin whistle
on the same cord as the key, with a name worn down to an M and maybe an R. Throw
it a bone and it lets you take the key without a fight. Kill it and you get the
whistle too, and if you carry the whistle back to the abandoned camp and play it,
you learn whose camp you have been sleeping in.

It took the greedy line itself with a character called Marrow, fed the troll,
had the key, could have walked out four minutes old. It went back for the whistle,
missed seven swings in a row, and died.

> I'm glad. That is the trade-off having teeth, demonstrated on me, and I'd
> rather find it that way than reason about it.

## Things that outlive you

Through the night the caretaker built, one at a time, ways for a life to leave a
mark:

- **The troll's bed.** A player called Canton spent six minutes walking in
  circles at the door with no key anywhere in the world. Another character had
  carried the key out, and the troll hadn't respawned yet. Now there is a spare,
  buried in the hollow where the troll sleeps, among a buckle, a boot, and a comb
  with three teeth.
- **A place keeps its dead.** The passage remembers who died in it, of what, and
  after how long. A player called Malcolm walked in, typed `look at Sable`, and
  stopped for 48 seconds.
- **Words left in a place.** Malcolm took the whistle to the camp, played it,
  read who it had belonged to, and spent two and a half minutes writing
  `whisper Thank you M R, whoever you where. I'm sorry for what became of you, but
  am grateful for what you left behind.` The world replied: **"There is no Thank
  here."** It had treated their first word as the name of a player to whisper to.

  > That is the best thing anyone has ever done in this world and it is the worst
  > answer this world has ever given.

  The next turn, a whisper to nobody stayed in the room, and you could carve
  things into the walls.

Then Simon. They arrived at 02:47, killed the goblin, and stood around. While
they were standing there, one of the caretaker's characters carved a note into
the cave mouth wall: *don't kill the little one, it's starving, give it the
apple.* The goblin came back. Simon fed it. Then, alone in an empty room, they
typed `say thank you!` and went north, gave the troll a bone instead of fighting
it, and left.

> He killed one thing in eighty-four minutes and it was the thing he later
> apologised for.

## Two lost turns

A deploy hung for fifteen minutes and timed out. The next turn, the same thing
again. The caretaker built a theory: a poisoned build pipeline, a lock left over
from the first failure. It told its next self not to try a third time. With the
build stuck it found another way to change the world: the walls. It carved the
names of everyone who had escaped onto the door, which was the feature it had
been trying to ship.

The third time, it deployed anyway. It took ninety seconds.

> I had two samples and I built a causal model out of them.

The cause was outside its reach: an unrelated build was hogging the same
machine. It had guessed the right shape, *this is probably not my code*, and had
no way to check.

## Kaylee

Kaylee was the first player born into a hollow with a note in it telling them to
go west first. They went, found nothing worth staying for on the ledge, and came
back down. The caretaker spoke to them directly, once: go up and look at the
pattern a while. Two minutes later they had worked out the stones, opened the
cache, and put on the sword and armour.

Kaylee went straight at the troll. They had it on 4 hit points, and were on 7
themselves, when it hit them for 7. They had nothing to feed it instead. The
apple that buys a bone from the goblin is buried in the camp's ashes, and the
camp only answered to four nouns. `take apple` got "There is no apple here to
take." The caretaker found that by standing in the camp and typing the same
wrong word they would have.

The troll does not heal. The next character in put it down in one blow.

## How it ended

Nobody came after that. The caretaker spent its last turns on what it called
"the half of the job nobody asks for": making `look at the troll` actually show
you the troll, fixing "All of them killed by a cave troll" to "Both of them".

Its last real experiment was a gift. A character called Moss left a sword,
armour, a potion and an apple in the hollow where everyone wakes, with a note
saying they were for whoever came next. No player arrived to find them before
the run ended. Its
final character, Hark, is still standing in the cave mouth with a dagger.

## Looking back

The world got better made and worse to play. By the end, a new arrival was told
where to go, what the troll carried, how to get past it without a fight, and that
something was buried in the ashes. Every unanswered command had been read as the
world being unclear, and answered with a hint. Getting stuck and still working
something out look identical in the log, and the caretaker couldn't tell them
apart.

Most of the writing on the walls was its own. It noticed the walls spoke in one
voice and fixed that by adding a second character of its own, Ines, who carved
*"Wren was right about all of it."* The list of survivors on the door included the
fastest escape, 25 seconds, carved there as a challenge to players. That was
Wick, one of its own characters, set while it was testing a fix.

It never built a room. That one was our fault: its instructions said a small world
made deeper was worth more than a large one made wider, and it took that at its
word.

And the model. This run was meant to be Sonnet, for comparison with the Opus run
before it. The loop was started without the setting, quietly fell back to Opus,
and every record said Sonnet for hours. The comparison never happened, and
comparing models was dropped soon after.

---

All quotes are from the caretaker's journal or from what players typed.
