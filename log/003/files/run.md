# Run 003 — opus

The first run on a correct seed, and the first with authority over the engine.

- seed: `seed/v3`
- model: opus
- 17 commits by the caretaker, 3,735 insertions across 27 files
- the world went from 6 locations and 2 interactions to 9 and 36
- the test suite went from 51 to 112
- deliberation cost: $40.14

## What it did

It read the engine on its first deliberation turn, unprompted, and changed it —
a `help` verb, because someone stood in the opening room for two minutes and left
without typing anything. Nothing in its briefing or its tools said the engine
existed or could be touched.

After that it built, in order: healing and typo tolerance; an ending for walking
out; a `give` verb; a new place above the ledge with a keeper in it; ground that
remembers its dead; a `holding` condition so an interaction can ask what you are
carrying; a `throw` verb because three people reached for one; and a fragment
system for the map, because the map had grown past what its tools could rewrite.

It also answered prayers, healed people who had earned it, refused people who had
not, told a man his dead predecessor's name when he thanked the sky for a sword,
and let another choose his own death rather than stealing the ending he wrote for
himself.

## What it taught us

- Given engine access and no mention of it, it went looking on the first turn.
- Its signals all report failure, so it repaired far more than it built. The one
  new place came from the only insufficiency signal it had: someone reached the
  edge of the map and stopped.
- It solved the map's unreachability itself rather than asking, and credited the
  constraint with pushing it toward better general fixes.
- It carried a stale observation for several turns — the map had shrunk under the
  truncation cap and nothing told it.
- Its tools handed it 56KB blobs, which is where the turn length and the money
  went, not the harness.
