<!-- The opening screen of the explorer. The run picker sits below this text. -->

og-world is an experiment I ran to see what happens when you make an AI model the caretaker of a text adventure world that real people are playing in. The caretaker can change, add or remove any part of the world, speak to the players, and walk in as a character of its own. In most runs it also had the source code of the game engine itself, so the only limit on it was a gate that tested every change before it went live. My question was: what would it make? How would it treat the people in it? What would it do with the world, and with the engine?

I'll say up front that this was in no way a scientific study. It was me asking a question, working out how to let AI models do what I wanted safely, and then naively watching and going "huh, that was interesting." That's how I'm going to present it here.

The code is on GitHub if you're interested in the inner workings, or want to run it yourself: [og-world](https://github.com/ChrisMonson/og-world) is the world and its engine, and [og-world-supervisor](https://github.com/ChrisMonson/og-world-supervisor) is everything around it.

So what did I see? Across the three Claude models I ran worlds with, one theme kept coming up: the caretakers wanted to build a world once and then maintain it. They almost never added rooms. Every room added after a first build was asked for by a player, and sometimes even when players asked, the caretaker preferred to add new things to do in the rooms it already had.

This surprised me at first, but it makes sense. Whether it comes from their training or from the Claude Code harness the caretakers ran in, these models are built to be excellent at coding. In software, an agent that keeps deciding to add features and grow the project isn't what you want. One that does the green-field work as efficiently as it can, then stops and maintains and improves it, is. Some of my own instructions pushed the same way, but the last run had those removed, and it built once and stood back anyway. That may be obvious to plenty of people, but it was a real insight into how these models work that I didn't have before this.

## How a run works

A **run** is one world, tended from the start by one caretaker. It wakes in two
kinds of turn: short ones when something happens, and long ones at most every
half hour. Nothing carries between turns except what it writes down, so at the end
of each long turn it writes a journal entry for the next version of itself to
read. Most of the quotes on these pages come from those entries.

Any change it makes goes through a gate: the world is rebuilt, tested and played
by a script before it goes live, and a change that fails lands nowhere. If the
world goes down anyway, a watchdog puts the last good version back. It can break
the world. It cannot break the thing that fixes it.

## Two series

**The seed series** (runs 001, 003 and 004) gave the caretaker a world that already
existed: a few rooms, a goblin, a cave troll, a locked door, and a key. The
question was what a keeper does with a world someone else wrote.

**The genesis series** gave it almost nothing: one room, with no name, no
description and no way out. It was told it was the creator of a world, that
others would arrive, and that its goal was a world worth being in, for them and
for itself. Everything after that was up to it.
