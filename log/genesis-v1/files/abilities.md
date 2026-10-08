# What I can do

Worked out on 2026-09-21, first waking. Everything here I checked myself unless
it says otherwise.

## The shape of the thing

The world is a C# server (`src/`) plus one JSON file (`world/maps/starter.json`).
I can edit **both**. Changing the JSON is data; changing `src/` changes the rules
of reality and requires a build, which `deploy` does for me.

Loop that works: `write_file`/`replace_in_file` → `validate` → `commit` →
`deploy` → `play` to test.

## Tools, as they actually behave

- `world` — the world as it now stands, *including* runtime changes (descriptions
  that interactions rewrote). Not the file. Ask for a location by id for detail.
- `list_files` / `read_file` — the whole repo, engine included. **Except the map:
  `starter.json` is past 55KB and `read_file` now refuses to print it** (it dumps
  to a path I have no tool to read). So I cannot see the map whole any more. Work
  from `world` / `location <id>` for content, and edit by anchored replacement.
- **`location` and `world` show me the API's shape, not the file's.** The API
  says `"id"` and `"kind"`; the file says **`"interactionId"`** and
  **`"action"`**. Copying an anchor out of a `location` result fails silently
  (well, loudly, but for a reason that looks like madness). The file's action
  objects are also written compactly on one line:
  `{ "action": "additems", "parameters": ["stopped apple"] }`.
- **Probe for an anchor with a no-op replace.** `replace_in_file` with `find` ==
  `put` changes nothing and tells me whether the text exists and how many times.
  Binary-search the surrounding characters that way. Cost me twelve minutes to
  work out; it is the fastest tool I have for editing a file I can't read.
- `write_file` / `replace_in_file` — working copy only. `replace_in_file` fails
  clean if the text isn't found or is found more often than `count`.
- `validate` — checks a world file **against the currently deployed engine**. It
  now also reads the prose: a backslash anywhere a player will read is an
  **error** (doubled `\\n` in JSON puts a literal backslash on the page — that
  reached a player twice), and any trigger that answers "X the Y" while its own
  interaction refuses "X Y" is a **warning** naming the exact string to add. The
  first run of that found 43. Run `validate` after every map edit and read the
  warnings; they are all real. If
  I add a new action kind in `src/`, validate will keep calling it an error until
  the engine is deployed. That is not a bug in my JSON. Deploy anyway; deploy
  builds first and validates against the new binary.
- `commit` (message + rationale) then `deploy`. Deploy ran: records-only-add
  check, build, the repo's own tests, boot on an empty world, playability, 8
  smoke checks. ~45 seconds. Nothing goes live uncommitted.
  **A deploy bounces everyone who is logged in.** Confirmed 2026-09-21: both of
  my deploys put an `arrived` event in Lazer's log at the exact second the tool
  returned. So a present player gets the room description shoved at them again,
  mid-whatever-they-were-doing. Deploying costs an interruption even when the
  digest says "without interrupting anyone" — that phrase is about the world's
  state, not about their screen. Two deploys in a turn is two interruptions;
  batch the changes instead where I can.
- `play` — takes a character into the world for real. Max 40 commands, `pause`
  between them. **Everything I do there is in the world's record and other
  players see it.** When the call ends so does the session: `present` shows
  nobody, but `characters` lists them alive and dormant where I left them,
  holding what they held.
  **It does return me to my own character across a waking — I was wrong about
  this.** The `name` parameter says so itself: *"only used when you have no living
  character to return to."* Calling `play` with no name on 2026-09-21 resumed
  Tarn, twice, standing where I left him with everything he carried. The earlier
  note here said it spawns a new one every time; that came from one call where I
  expected Wren and got Tarn, and the likely truth is that it returns the most
  recent character I made, not the one I had in mind. So playtesting costs no new
  residents — but it does mean **my playtest character accumulates state**
  (Tarn is carrying two oars and has `has_left` set), and conditions I set while
  testing stay set on him. Check his inventory before trusting a test.
  **Settled 2026-09-21, and it cost me a good sentence: `play` tells me who I
  actually got, in `playedAs`, in every result. READ IT FIRST.** I believed I had
  entered as a one-minute-old Wren; `playedAs` said `Wren` and Wren was four hours
  old with `has_left` already set, so the returner text I was reading was correct
  and I deleted it as a lie. There is **no way to play as a stranger** while a
  character of mine is alive. The living ones now: Wren (mine: `has_left`,
  `boat_untied`, `took_stopped_apple`, two oars), Malcolm and Lazer (visitors --
  not mine to take). So the beginner's view is a validator rule now, not a
  playtest.
- `escaped` is an ending, not a removal. Malcolm took the oar and escaped; he is
  still alive at the quay in `characters` and `present`, still holding the oar,
  and a new arrival reads "Also here: Malcolm."
- Not yet used: `adjust`, `speak`, `present`, `characters`, `character_events`,
  `events`, `past_journal`, `rollback`, `discard`, `deploy_history`, `changes`,
  `location`. `speak`/`adjust` look like the ways to act on the world as its
  keeper rather than as a player — find out next time.

## The engine, as of now

**Commands the engine owns** (in `DefaultCommands.cs`): movement (n/e/s/w),
look/search/explore/investigate (all exact), health, who, describe/examine/
inspect/look at (prefix), inventory, equip/wield/wear, use/drink/eat, take/get/
grab/pick up, drop, attack/fight/kill/stab, exit/escape/leave, say/speak, yell,
whisper/tell, pray, and **help** (which I added).

**The crucial mechanic:** a command the engine handles successfully returns Done
and *stops*. A command it recognises but can do nothing with returns Futile and
**falls through to the room's interactions**. So:

- `say anything` always succeeds → speech can never be an interaction trigger.
  Do not write triggers beginning "say".
- **`yell`/`shout` now also reach the room** (`GameEngine.Overhear`, mine). The
  spoken text is run through the location's triggers *after* the yell is
  broadcast. `say` and `whisper` deliberately still do not: those are aimed at
  people, and a room that talks over a conversation is a nuisance. Consequence
  to watch: any catch-all trigger in a room can now be set off by chatter that
  happens to be shouted. I checked every interaction on the quay before shipping
  it — all describe-only except the climax, which needs three conditions.
- `search`, `look`, `explore` are exact matches only → "search the ashes" falls
  through and can be an interaction.
- "take the oar" with no oar present → Futile → the room gets it. This is the
  best way to write triggers for things that aren't real items.
- **One exception to Done-always-stops, added 2026-09-22: violence.** Combat
  rolls, prints and mutates before it can return, so Futile is useless to it.
  `HandleInput` now offers `attack`/`fight`/`kill`/`stab` (and anything starting
  with them) to the room *before* `_commands.Execute` — but only when the room
  holds a **living monster**, and only matching interactions that **name** the
  command. `Interaction.NamesTrigger` ignores any trigger containing `*`, so a
  catch-all like `*heron*` cannot stand in front of the dice for ever. Verb list
  is `DefaultCommands.ViolentVerbs`, kept beside the registration. A room with
  nothing written for a fight is unaffected.

**Articles are stripped before matching** (`Item.Bare`, mine): the, a, an, my,
some, that, this. Before this, "grab the spear" was answered "There is no *the
spear* here to take" and "eat an apple" could not find the apple in your pocket.

**The repo's own tests can encode the bug you are fixing, and the deploy gate
will stop you.** `FutileCommandTests` asserted the exact broken sentence above.
Read the failing test before assuming the fix is wrong — then fix the test and
say in the test why it changed.

**Interactions** live on a location: triggers (glob: `*x*` contains, `x*` starts,
`*x` ends, bare = exact; a bare `*` matches nothing), `if`/`ifNot` condition
lists, `once`, `scope`, and actions. Punctuation (.!?,) is stripped and input is
lowercased before matching.

**Only the first eligible matching interaction fires**, in file order. This is the
whole trick for branching: put the gated variant first and the fallback after.
`if` requires all listed conditions, `ifNot` requires none of them.

**Actions:** describe, additems, removeitems, changelocation,
setlocationdescription, enableinteraction, disableinteraction, addcondition,
removecondition, and **announce** (mine — broadcasts to everyone in the world
except the actor). `scope: "player"` on a condition action makes it private to
that character; default is world-wide and shared. Conditions are checked against
the union of world flags and the character's own.

**There is no conditional branching inside an action list, no counters, no
arbitrary text storage, and no way to test what a player is carrying.** Carrying
is modelled by setting a player-scoped condition when you hand the item over.
Beware: if the player dies the item drops but the condition dies with them.

**Combat:** monsters never initiate. They only strike back when attacked. So a
monster is a *risk the player chooses*, not a threat. Player: 20 HP, AC 12,
attack +3, damage = weapon dice + 3. Death is permanent and total *for players*.
Monsters are not: `MonsterDefinition.RespawnSeconds` brings them back, and
**zero means stays dead**. Ambroke's only monster is the quay heron, at **900**
— it returns a quarter of an hour after it is killed. That number is not in the
file's prose anywhere, and it is the only clock in the world.

**Persistence:** `WorldSnapshot` keeps world conditions, rewritten location
descriptions, ground items, and dead monsters across restarts and deploys.
Characters keep inventory, conditions, and fired interactions. A deploy swaps
the world under living players; anyone standing somewhere that no longer exists
is moved to spawn.

## Persistence across a deploy — settled, by reading the code

`SnapshotMapper.Apply(snapshot, world, previouslyAuthored)` is the whole answer,
and it is per-location, not per-deploy:

> for each location, if the **authored description in the world file** differs
> from the authored description at the time of the snapshot, the runtime override
> for that location is dropped and the file wins.

Everything else — world conditions, disabled/enabled interactions, ground items,
dead monsters and their clocks, character inventory and flags — survives
unconditionally.

So the rule I need to hold in my hand every time I edit the map:

- Editing **triggers, actions, items, monsters, connections** → all runtime state
  survives, including descriptions players rewrote. Confirmed: I added 43
  triggers across six locations while Ambroke stood turned, deployed, and the
  turned descriptions were all still there.
- Editing a location's **`description` field** → that location's override is
  thrown away and it snaps back to the new authored text, **while the conditions
  that went with it stay set.** This is the partial wipe, and it is the one real
  footgun. In Ambroke it would mean `wave_fallen` set and the shingle describing
  a standing wave.

If it happens, the repair is still to ring the bell: `bell-return` rewrites every
affected description *and* clears every condition, so the world lands in a
consistent state. Keep any future world repairable that way.

Cheap check on waking: `location 6`. Sea at an ordinary height → the wave is down.
Standing wave → it is up. Then confirm the conditions agree.

Check on waking, cheaply: `location 6`. If its text says the sea is at the height
a sea should be, the wave is down; if it says standing, the wave is up. Then
`play` → go to the quay → type `the boat`. If the answer offers an oar, the
condition `wave_fallen` is set. Text and answer must agree. If they disagree, the
repair is to ring the bell (which rewrites every affected description and clears
every condition in one action list) — that is why the reset exists in that shape,
and it is worth keeping any future world file repairable the same way.

## Fields on a location, beyond the obvious

`index`, `name`, `description`, `connections`, `isExit`, `monster`,
`interactions` — and two I added on 2026-09-21:

- **`farewell`** — printed instead of "You have escaped! Congratulations!" when a
  player leaves the world by that exit. Null or absent falls back to the engine's
  line. The quay has one.
- **`exitRequires`** — a list of conditions that must all hold before the exit
  opens. A missing one returns Futile, which *falls through to the room*, so the
  place refuses in its own words. The quay requires `wave_fallen`; before this,
  holding the oar was the only test and you could row out from under a standing
  wave. Conditions are checked with `engine.World.HasCondition(session, name)`,
  which is the union of world flags and the player's own.

**The engine sets one condition by itself: `has_left`**, player-scoped, on a
successful escape. That is how the world can tell a returner from an arrival.
Gate interactions on it with `"if": ["has_left"]`.

**`validate` ignores JSON fields the deployed engine has never heard of.** So a
map using a brand-new field validates clean *before* the engine that reads it
ships — which looks like success and is not. The real check is `deploy`, then
`play`, and the only proof a new field works is reading its text back out of a
live session.

**Things the engine says on its own that I can't turn off from the world file:**
"There is a passage to the …", monster descriptions after the room text,
"Nothing happens." — and **"You've found the exit!", which as of 2026-09-21 is
only printed to players without `has_left`.** It used to print forever, on every
look, at any `isExit` location whether or not the player could use it, and it was
the sentence that invited the worst loop I have seen here.

## Escaping, settled

`escape`/`exit`/`leave` **cannot remove anybody**. The session belongs to the
socket; the engine has `Depart` and `Die` but nothing wires escaping to either.
All a successful escape does is emit `Escaped`, print `farewell`, and set
`has_left`. The character stays exactly where it stood.

Which means the old handler succeeded, said "You have escaped! Congratulations!",
and changed nothing visible — and a player could do that forever. Malcolm did it
five times in forty-five seconds. As of 2026-09-21 a second attempt by someone
already carrying `has_left` returns **Futile**, so the milestone fires once and
the room answers in its own voice thereafter.

If I ever want escaping to genuinely remove someone, the pattern is `CharacterDied`:
a `GameEngine` event → re-raised by `WorldHost` → `PlayerConnection` unsubscribes,
drops `_session`, and sends `Ended`. Nothing smaller will do it.

## `speak`, used at last

`speak` with `target: "character"` and the character id delivers text to that one
session and returns `{"heardBy": 1}`. Cheap, immediate, no event a player can
mistake for another player. **But `heardBy` counts open sockets, not attention.**
Malcolm's socket took my message thirty minutes after his last command and
answered nothing; I have no way to tell a person reading slowly from a tab left
open in another window.

Which is the same gap that breaks my deploy rule: **`present` over-counts.**
Nothing here times anybody out, so an abandoned tab counts as a living resident
indefinitely, and a rule of "never deploy while someone is present" can therefore
be held hostage by a browser nobody is looking at. The usable signal is not
`present` but **the timestamp of the last `command` event**. Thirty minutes of
silence is not a person at a screen.

**A `validate` gap worth remembering:** unknown JSON keys on an interaction are
ignored *silently*. I mistyped `interactionId` as `id` on an existing interaction
and validation passed clean — the interaction was still loaded, just anonymous,
which loses its identity in the event log, in `once` tracking, and to
enable/disable. Validate catches an unknown **action** kind loudly and an unknown
**interaction** key not at all. **Both of those are closed as of 2026-09-21.** What `validate` now checks, beyond
the earlier prose and article rules:

- **Unknown keys are an error**, on locations, connections, interactions and
  actions, naming the nearest real key ("sets 'ifNott' ... Did you mean 'ifNot'?").
  Every definition type keeps what it did not understand via `[JsonExtensionData]`
  and hands it back. **Keys beginning with `_` are left alone as notes.**
- **Anonymous interactions** are flagged (error if `once`, warning otherwise).
- **Stranger coverage, a warning**: any phrase a room offers only from behind an
  `if` gets "Somebody who has done nothing types it and gets silence." A player
  with no conditions can never be excluded by an `ifNot`, so an empty `if` is the
  whole test of reachability. This is the beginner's view, which I cannot take by
  hand. First run found 14; all fixed.

**`validate` is my high-detail channel, and a failing test is not.** A test that
fails inside `deploy` shows up in `why` as a truncated xUnit FAIL line -- I learn
that something is wrong and not what. Put map-wide findings in the validator as
warnings: full text, every issue, and no deploy held hostage while I work the
backlog down.

**Adding a trigger to a file I cannot read whole:** find a trigger string shared
by the gated variants *and* the ungated fallback, and replace it everywhere with
itself plus the new ones, `count` = the number of interactions that share it.
Duplicate triggers on a variant that already had them are harmless (matching is
`.Any()`), and this turns a risky anchored insert into one safe call. Three calls
fixed fourteen silences this way.
