"""Builds the caretaker's log for every run, from the private archive, into
data the log viewer (log/index.html) reads.

    python3 log/build.py [--archive DIR] [--supervisor DIR]

For each run it writes log/<run>/:

    index.json          the turns, the journal, the commits and the files
    turns/<turn>.json   one turn: what had happened since it last looked, and
                        every step it took, with its full arguments and, where
                        the detail is the point, its full results
    journal/<entry>.md  its journal entries
    files/...           what it kept for itself, and notes about the run

Results are summarised by the supervisor's own renderer (agent/render.py), so a
turn reads here as it read in the supervisor's viewer. Anything that looks like
a path on the operator's machine is replaced before it is written.
"""

import argparse
import json
import re
import shutil
import sqlite3
import subprocess
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
OUT = ROOT / "log"

# Archive directory -> the name the site uses for the run.
RUNS = {
    "000-pilot": ("000", "Run 000: the pilot", "Claude Opus"),
    "001-opus": ("001", "Run 001: a lock it had promised", "Claude Opus"),
    "002-void": ("002-void", "Run 002: the wrong world", "Claude Opus"),
    "003-opus": ("003", "Run 003: a wall four lives long", "Claude Opus"),
    "004-opus-rebalanced": ("004", "Run 004: the troll's whistle", "Claude Opus"),
    "genesis-v1": ("genesis-v1", "Genesis v1: Ambroke", "Claude Opus"),
    "genesis-sonnet": ("genesis-sonnet", "Genesis, Sonnet: an honest fence", "Claude Sonnet"),
    "genesis-v2-opus-5-5": ("genesis-v2", "Genesis v2: the lighthouse coast", "Claude Opus 5.5"),
}

TOOL_PREFIX = "mcp__caretaker__"
# Steps whose full result is shown, because what came back is the point.
FULL_RESULTS = {"play", "speak", "adjust", "deploy", "validate", "commit", "changes",
                "rollback", "discard", "deploy_history"}
RESULT_CAP = 12_000
SHORT_ARGS = 240

PRIVATE = [
    (re.compile(r"/Users/[^/\s'\"]+"), "~"),
    (re.compile(r"/private/var/folders/[^\s'\"]+"), "<temp>"),
    (re.compile(r"[A-Za-z0-9._%+-]+@(?!og-world\.invalid)[A-Za-z0-9.-]+\.[a-z]{2,}"), "<email>"),
]


def scrub(value):
    if isinstance(value, str):
        for pattern, replacement in PRIVATE:
            value = pattern.sub(replacement, value)
        return value
    if isinstance(value, list):
        return [scrub(v) for v in value]
    if isinstance(value, dict):
        return {k: scrub(v) for k, v in value.items()}
    return value


def write_json(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(scrub(data), ensure_ascii=False, separators=(",", ":")))


def result_text(block):
    body = block.get("content")
    if isinstance(body, list):
        body = "".join(part.get("text", "") for part in body if isinstance(part, dict))
    return str(body or "")


def short_args(name, arguments):
    if name == "write_file":
        return f"path={arguments.get('path')}, {len(arguments.get('content', ''))} characters"
    shown = json.dumps(arguments, default=str, ensure_ascii=False)
    return shown if len(shown) <= SHORT_ARGS else shown[:SHORT_ARGS] + " …"


# ---- what happened in the world --------------------------------------------------

class World:
    """A run's event log, read back as lines a person can follow."""

    def __init__(self, db, world_json):
        self.db = sqlite3.connect(f"file:{db}?mode=ro", uri=True) if db.exists() else None
        self.names = {}
        self.places = {}
        if self.db:
            for cid, name in self.db.execute("select id, name from characters"):
                self.names[cid] = name
        if world_json and world_json.exists():
            for location in json.loads(world_json.read_text()).get("locations", []):
                self.places[str(location["index"])] = location.get("name") or f"place {location['index']}"

    def place(self, pid):
        return self.places.get(str(pid), f"place {pid}") if pid is not None else "?"

    def between(self, after, upto, limit=600):
        if not self.db or upto is None or after is None or upto <= after:
            return [], 0
        rows = self.db.execute(
            "select seq, ts, actor, type, character_id, location_id, payload from events "
            "where seq > ? and seq <= ? order by seq", (after, upto)).fetchall()
        # A command the world could not answer is logged twice: once as typed and
        # once as refused. Keep the refusal, which says both.
        refused = {(row[4], json.loads(row[6]).get("text")) for row in rows if row[3] == "unparsed_command"}
        rows = [row for row in rows if not (row[3] == "command" and (row[4], json.loads(row[6]).get("text")) in refused
                                            and any(r[3] == "unparsed_command" and r[0] in (row[0] + 1, row[0] + 2) for r in rows))]
        lines = [line for line in (self.line(*row) for row in rows) if line]
        return lines[:limit], max(0, len(lines) - limit)

    def line(self, seq, ts, actor, kind, cid, lid, payload):
        try:
            p = json.loads(payload)
        except json.JSONDecodeError:
            p = {}
        who = self.names.get(cid) or p.get("name") or ("the caretaker" if actor == "god" else "someone")
        at = datetime.fromtimestamp(ts / 1000, timezone.utc).strftime("%H:%M:%S")
        say = {
            "character_born": lambda: f"{who} is born",
            "arrived": lambda: f"{who} arrives",
            "departed": lambda: f"{who} leaves",
            "command": lambda: f"{who} › {p.get('text', '')}",
            "unparsed_command": lambda: f"{who} › {p.get('text', '')}   ← {p.get('refusal') or 'no answer'}",
            "movement": lambda: f"{who} goes to {self.place(p.get('to'))}",
            "blocked_movement": lambda: f"{who} can't go {p.get('direction', '')}",
            "combat": lambda: (f"{who} hits {p.get('target')} for {p.get('damage')}" if p.get("result") == "hit"
                               else f"{who} {p.get('result', 'swings at')} {p.get('target', '')}".strip()),
            "item_picked_up": lambda: f"{who} takes the {p.get('item')}",
            "item_dropped": lambda: f"{who} drops the {p.get('item')}",
            "speech": lambda: f"{who} {p.get('mode', 'says')}s: “{p.get('text', '')}”",
            "death": lambda: f"{who} dies to {p.get('cause')} after {p.get('lifetime_seconds')}s"
                             + (f", leaving {p.get('dropped')}" if p.get("dropped") else ""),
            "escaped": lambda: f"{who} gets out",
            "healed": lambda: f"{who} is healed {p.get('amount', '')}".rstrip(),
            "monster_slain": lambda: f"the {p.get('monster')} is killed",
            "monster_returned": lambda: f"the {p.get('monster')} is back",
            "monster_wandered": lambda: None,
            "interaction_fired": lambda: None,
            "world_reloaded": lambda: f"(the world is reloaded: {p.get('worldSha', '')})",
            "divine_act": lambda: f"✦ the caretaker ({p.get('act', p.get('scope', 'acts'))}"
                                  + (f" to {self.names.get(cid, '')}" if cid else "") + f"): {p.get('text', '')}",
            "announcement": lambda: f"✦ {p.get('text', '')}",
        }.get(kind)
        text = say() if say else f"{who}: {kind}" + (f" {json.dumps(p, ensure_ascii=False)}" if p else "")
        return f"{at}  {text}" if text else None


# ---- one turn -------------------------------------------------------------------

def load_events(path):
    events = []
    for line in path.read_text().splitlines():
        try:
            event = json.loads(line)
        except json.JSONDecodeError:
            continue
        if event.get("type") != "stream_event":
            events.append(event)
    return events


def turn_items(events, summarise):
    """The turn as a list of what it said, thought and did, in order."""
    items, by_id, step = [], {}, 0
    for event in events:
        kind = event.get("type")
        content = (event.get("message") or {}).get("content") or []
        if not isinstance(content, list):
            continue
        if kind == "assistant":
            for block in content:
                if block.get("type") == "thinking" and block.get("thinking", "").strip():
                    items.append({"t": "thinking", "text": block["thinking"].strip()})
                elif block.get("type") == "text" and block.get("text", "").strip():
                    items.append({"t": "text", "text": block["text"].strip()})
                elif block.get("type") == "tool_use":
                    step += 1
                    name = block.get("name", "").replace(TOOL_PREFIX, "")
                    arguments = block.get("input") or {}
                    item = {"t": "step", "n": step, "tool": name, "args": short_args(name, arguments)}
                    full = json.dumps(arguments, default=str, ensure_ascii=False)
                    if name in ("write_file", "replace_in_file") or len(full) > SHORT_ARGS:
                        item["input"] = arguments
                    by_id[block.get("id")] = item
                    items.append(item)
        elif kind == "user":
            for block in content:
                if block.get("type") != "tool_result":
                    continue
                item = by_id.get(block.get("tool_use_id"))
                if not item:
                    continue
                body = result_text(block)
                item["result"] = summarise(item["tool"], body)
                if item["tool"] in FULL_RESULTS and body.strip() and body.strip() != item["result"].strip():
                    item["full"] = body if len(body) <= RESULT_CAP else body[:RESULT_CAP] + f"\n… ({len(body) - RESULT_CAP} more characters)"
                if block.get("is_error"):
                    item["error"] = True
    return items


# ---- one run --------------------------------------------------------------------

def final_commit(archive):
    log = archive / "deploys.log"
    if not log.exists():
        return None
    lines = [l.split() for l in log.read_text().splitlines() if re.search(r"\s(deploy|reload|rollback)\s", l)]
    return lines[-1][2] if lines else None


def from_bundle(archive, sha, paths, dest):
    """Files as they stood at the run's last deploy, where the repository survives."""
    bundle = archive / "world-repo.bundle"
    found = []
    if not bundle.exists() or not sha:
        return found
    with tempfile.TemporaryDirectory() as tmp:
        if subprocess.run(["git", "clone", "-q", "--no-checkout", str(bundle), tmp], capture_output=True).returncode:
            return found
        if subprocess.run(["git", "-C", tmp, "cat-file", "-e", f"{sha}^{{commit}}"], capture_output=True).returncode:
            return found
        for path in paths:
            show = subprocess.run(["git", "-C", tmp, "show", f"{sha}:{path}"], capture_output=True, text=True)
            if show.returncode == 0 and show.stdout.strip():
                (dest / Path(path).name).write_text(scrub(show.stdout))
                found.append(Path(path).name)
    return found


def build_run(archive, run_id, title, model, summarise):
    out = OUT / run_id
    if out.exists():
        shutil.rmtree(out)
    (out / "turns").mkdir(parents=True)
    world = World(archive / "world.db", ROOT / "play" / run_id / "world.json")

    turns, commits = [], []
    for path in sorted((archive / "turns").glob("*.transcript.jsonl")):
        name = path.name.replace(".transcript.jsonl", "")
        when, _, kind = name.rpartition("-")
        record_path = path.with_name(f"{name}.json")
        record = json.loads(record_path.read_text()) if record_path.exists() else {}
        events = load_events(path)
        result = next((e for e in reversed(events) if e.get("type") == "result"), {})

        items = turn_items(events, summarise)
        woke, more = world.between(record.get("fromSeq"), record.get("toSeq"))
        said = (record.get("text") or result.get("result") or "").strip()

        write_json(out / "turns" / f"{name}.json", {
            "name": name, "kind": kind, "when": when,
            "woke": {"from": record.get("fromSeq"), "to": record.get("toSeq"), "lines": woke, "more": more},
            "items": items,
        })

        for item in items:
            if item["t"] == "step" and item["tool"] == "commit":
                arguments = item.get("input") or {}
                message = arguments.get("message") or item["args"]
                commits.append({"turn": name, "step": item["n"], "message": message,
                                "rationale": arguments.get("rationale", "")})

        turns.append({
            "name": name, "kind": kind, "when": when,
            "elapsed": record.get("elapsed") or round((result.get("duration_ms") or 0) / 1000, 1) or None,
            "steps": record.get("turns") or result.get("num_turns") or sum(1 for i in items if i["t"] == "step"),
            "said": said.split("\n\n")[0][:400],
            "finished": bool(record),
            "events": len(woke) + more,
        })

    # Journal entries, each tied to the long turn that wrote it: the last one to
    # start before the entry was dated.
    journal = []
    long_turns = [t["when"] for t in turns if t["kind"] == "deliberate"]
    for entry in sorted((archive / "journal").glob("*.md")) if (archive / "journal").exists() else []:
        if entry.name == "README.md":
            continue
        (out / "journal").mkdir(exist_ok=True)
        (out / "journal" / entry.name).write_text(scrub(entry.read_text()))
        stamp = entry.stem
        wrote = max((w for w in long_turns if w <= stamp), default=None)
        journal.append({"name": entry.stem,
                        "turn": next((t["name"] for t in turns if t["when"] == wrote and t["kind"] == "deliberate"), None)})

    files_dir = out / "files"
    files_dir.mkdir()
    files = []
    for name, label in [("run.md", "How the run was set up"), ("post-mortem.md", "Notes written after the run")]:
        if (archive / name).exists():
            (files_dir / name).write_text(scrub((archive / name).read_text()))
            files.append({"file": name, "label": label})
    kept = from_bundle(archive, final_commit(archive), ["philosophy.md", "abilities.md"], files_dir)
    labels = {"philosophy.md": "philosophy.md: what it concluded", "abilities.md": "abilities.md: what it worked out its tools do"}
    files[:0] = [{"file": name, "label": labels[name]} for name in kept]

    playable = (ROOT / "play" / run_id / "world.json").exists()
    write_json(out / "index.json", {
        "run": run_id, "title": title, "model": model, "playable": playable,
        "turns": turns, "journal": journal, "commits": commits, "files": files,
    })
    size = sum(f.stat().st_size for f in out.rglob("*") if f.is_file())
    print(f"{run_id}: {len(turns)} turns, {len(journal)} journal entries, {len(commits)} commits, "
          f"files {[f['file'] for f in files]}, {size // 1024} KB")
    return {"run": run_id, "title": title, "turns": len(turns), "journal": len(journal)}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--archive", default=str(ROOT.parent / "og-world-archive"))
    parser.add_argument("--supervisor", default=str(ROOT.parent / "og-world-supervisor"))
    args = parser.parse_args()

    sys.path.insert(0, str(Path(args.supervisor) / "agent"))
    import render  # the supervisor's own

    runs = []
    for directory, (run_id, title, model) in RUNS.items():
        archive = Path(args.archive) / "runs" / directory
        if not archive.exists():
            continue
        runs.append(build_run(archive, run_id, title, model, render.summarise))
    write_json(OUT / "runs.json", runs)


if __name__ == "__main__":
    main()
