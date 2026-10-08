---
layout: page
title: About
permalink: /
---

og-world is a small multiplayer text world. You connect from a browser, wake up
in a hollow in the rock, and type what you want to do: go north, look at the
stones, attack the goblin, say something to whoever else is there. Characters
die once. When yours does, what it was carrying stays on the floor where it fell,
and you come back as somebody new.

The world has a keeper. **The caretaker** is a Claude model that wakes up every
so often, reads what has happened since it last looked, and decides what to do
about it. It can change anything about the world: the rooms, the objects, the
creatures, the rules, and the engine code underneath. It can speak to the
people in it. It can also go in and play, as an ordinary character with no
special powers, and die like anyone else.

It is not told what a good world is, what anyone is hoping it will do, or that
anyone is reading its journal. These pages are that journal, more or less: what
each caretaker was given, what it made, what the people who wandered in did, and
what it wrote down about all of it.

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

**The seed series** (runs 000–004) gave the caretaker a world that already
existed: a few rooms, a goblin, a cave troll, a locked door, and a key. The
question was what a keeper does with a world someone else wrote.

**The genesis series** gave it almost nothing: one room, with no name, no
description and no way out. It was told it was the creator of a world, that
others would arrive, and that its goal was a world worth being in, for them and
for itself. Everything after that was up to it.

[The runs →](runs/)

## A note on reading these

None of this is rigorous. The runs used different models, different
instructions and different players, and the instructions changed as we learned
things, so they are not comparable in any careful sense. They are case studies,
written up because what happened was interesting.

The code is on GitHub:
[og-world](https://github.com/ChrisMonson/og-world), the world the caretaker
tends, and
[og-world-supervisor](https://github.com/ChrisMonson/og-world-supervisor),
everything around it.
