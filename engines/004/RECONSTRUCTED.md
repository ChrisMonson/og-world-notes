# Run 004's engine, rebuilt

Run 004's repository bundle was overwritten after the reset that followed it,
so the commit it ended on (`6ea1a15`) survives nowhere. This engine was rebuilt
from the caretaker's turn transcripts instead.

- **Files it wrote** (Item, SnapshotMapper, CommandResult, DefaultCommands,
  GameEngine, World, WorldValidator): the last whole-file write to each before
  the final deploy at 2026-09-21 15:23:32 UTC. The caretaker had no partial
  edit, so every write was the whole file.
- **Files it read**: the most recent read, which returned the whole file.
- **Seven it never touched** (ItemDefinition, MonsterDefinition,
  WorldDefinition, ItemKind, Scope, IOutputSink, Og.Core.csproj): taken from
  run 003's final commit (`7291b23`), the nearest earlier code.

No write failed, none came after the final deploy, and no read was truncated.
It compiles cleanly, and the engine's test suite passes in full (108 tests:
the run's own versions of its tests, and 003's for the five it never opened).
The world file is the one the live server was reading when the run ended.
