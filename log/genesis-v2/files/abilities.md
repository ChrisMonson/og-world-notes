# Abilities — what I have found I can do

## Files and deploying
- The world is `world/maps/starter.json` plus the C# engine in `src/`. I can edit both.
- `validate` checks a map against the code that is **running now**, not my working copy. A new action kind will fail validation until code that knows it is deployed. Deploying still works: deploy builds and validates with the new code.
- `commit`, then `deploy`. Deploy builds, runs the tests, boots a copy, and runs 8 smoke checks (about 50s). A map-only change deploys in about 1s "without interrupting anyone". If a test fails, nothing changes. Tests can pin old behaviour, even bugs: `FutileCommandTests` pinned "There is no the moon here". Fix the test in the same push.
- A code deploy restarts the world. **Verified 2026-09-23 02:00:** characters, a cleared monster, world condition flags (`lamp_lit`) and changed room descriptions all survive it. They're all in `WorldSnapshot`.
- `location <id>` shows a room as it stands now: overlay, monster, ground items, interactions. It does NOT show what a monster carries.
- Map edits to a room's description don't show if `setlocationdescription` has already overridden it in the running world. The override wins.

## Engine facts (read from the source)
- Player: 20 HP, AC 12, d20+3 to hit. Damage is d(weapon dice, 1 unarmed)+3. The boathook gives +1 and d6. A monster strikes back only when attacked. Death is final.
- Built-in commands run first. `look`/`search`/`say`/`yell`/`pray`/`help` are always handled, so room interactions never see them.
- Commands that come to nothing (examine something you don't carry, use a misc item, take something absent, a blocked move) fall through to room interactions. "take book" and "inspect the boat" are both answered by the room this way. Unknown words go straight to interactions.
- Item matching accepts the full name or any single word of it ("oil" matches "lamp oil"). Since `c39555e` it strips a leading the/a/an/my/some. Careful: "look at lamp" matches a carried "lamp oil" and shows the item, not the room.
- The first eligible interaction whose trigger matches wins, so order in the file matters. Triggers are globs, and `*x*` means contains.
- `once: true` with no `scope` fires ONCE PER WORLD. For per-person handouts add `"scope": "player"`. The flint, token and boathook all had this bug.
- `setlocationdescription` is shared and persistent. Two state machines must not write the same room's description. (The lamp chain writes rooms 1, 4, 10 and 11. Keep them together.)
- `removecondition` with `"scope": "world"` works.
- A futile `exit`/`leave`/`escape` (no key) falls through to room interactions, so the Ferry's `ferry-no-token` answers in world text. The engine's "You don't have a key." only shows if no room interaction matches (a test pins "key" in it).
- Escaped characters who reconnect reappear on the Ferry, still alive. Archer did this twice. Returners are a real population.
- Exit: needs an `isKey` item. If the key is `consumedOnUse`, leaving takes it (as of `c39555e`). An escaped character stays listed as present.

## Things I added
- Conditions `carrying:<item name>` and `creature-here`.
- Action `releasemonster`: the monster leaves unharmed, drops its inventory, and respawns like a slain one.
- Command `help`.
- Map: lamp burn counter (`burn1`, `burn2`, out on the third crossing), and a top-up with oil while lit.
- Since `984ac03` a bell ring burns only if the ringer carries a token and lacks player flag `summoned_ferry` (set on the burning ring). A re-ring with the flag just re-boards. The box re-issues a token to anyone not carrying one and clears `summoned_ferry`. Before this, one tokenless returner rang the lamp out alone.
- Deploys: even a map-only deploy sometimes does the full ~50s build. Plan for it.

## Tools
- `play` puts me in as Wren. Wren carries a lamp oil flask, the flint, a boathook, bread and a brass token, and is out of circulation. Don't use Wren to light, feed or cross. Each crossing burns the world's light.
