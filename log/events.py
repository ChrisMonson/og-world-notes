"""Builds each run's timeline, log/<run>/events.json, from the log already in
log/<run>/: what the caretaker did and wrote, turn by turn, and, behind it,
who was born into the world, who died there, and who got out.

    python3 log/events.py            every run
    python3 log/events.py 003 004    just these

log/build.py runs this for every run it builds. It needs nothing but the log.

Its own acts carry the turn and step they came from, so the page can open the
turn at that step. Steps have no clock of their own, so they are dated by the
turn they were taken in.
"""

import json
import re
import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
LOG = ROOT / "log"
OWN_FILES = {"philosophy.md": "philosophy", "abilities.md": "abilities"}


def stamp(when):
    return datetime.strptime(when, "%Y-%m-%dT%H%M%SZ").replace(tzinfo=timezone.utc)


def iso(d):
    return d.strftime("%Y-%m-%dT%H:%M:%SZ")


def arguments(step):
    if step.get("input"):
        return step["input"]
    try:
        value = json.loads(step.get("args") or "{}")
        return value if isinstance(value, dict) else {}
    except json.JSONDecodeError:
        return {}


def clip(text, n=600):
    text = (text or "").strip()
    return text if len(text) <= n else text[:n].rsplit(" ", 1)[0] + " …"


def build(run):
    out = LOG / run
    index = json.loads((out / "index.json").read_text())
    world = ROOT / "play" / run / "world.json"
    places = {str(l["index"]): l.get("name") for l in json.loads(world.read_text())["locations"]} if world.exists() else {}

    acts, happened, seen = [], [], set()
    spoken_to = {}  # the first words of something it said -> who heard it, from the world's own record
    played_as = set()
    names = {}  # who a character id turned out to be
    turns = [(t, json.loads((out / "turns" / f"{t['name']}.json").read_text())) for t in index["turns"]]

    # What happened in the world, from what each turn woke to. Lines carry only
    # the time of day; the day is the turn's, or the one before if that time
    # is later than the turn began.
    for meta, turn in turns:
        woke_at = stamp(meta["when"])
        for line in (turn.get("woke") or {}).get("lines", []):
            m = re.match(r"(\d\d):(\d\d):(\d\d)\s+(.*)$", line)
            if not m:
                continue
            at = woke_at.replace(hour=int(m[1]), minute=int(m[2]), second=int(m[3]))
            if at > woke_at:
                at -= timedelta(days=1)
            text = m[4]
            if (iso(at), text) in seen:
                continue
            seen.add((iso(at), text))
            said = re.match(r"✦ the caretaker \(speak to ([^)]+)\): (.*)", text)
            if said:
                spoken_to[said[2][:60]] = said[1]
            if b := re.match(r"(.+?) is born$", text):
                happened.append({"at": iso(at), "kind": "born", "who": b[1]})
            elif d := re.match(r"(.+?) dies to (.+?) after (\d+)s(?:, leaving (.*))?$", text):
                happened.append({"at": iso(at), "kind": "died", "who": d[1], "text": f"{d[1]} died to {d[2]}, {lifetime(int(d[3]))} after being born."
                                 + (f" They left {d[4]}." if d[4] else "")})
            elif e := re.match(r"(.+?) gets out$", text):
                happened.append({"at": iso(at), "kind": "escaped", "who": e[1]})

    # What it did.
    for meta, turn in turns:
        at = iso(stamp(meta["when"]))
        base = {"at": at, "turn": meta["name"], "turnKind": meta["kind"]}
        for item in turn["items"]:
            if item.get("t") != "step":
                continue
            tool, a, n = item["tool"], arguments(item), item["n"]
            act = None
            if tool == "commit":
                act = {"kind": "commit", "title": a.get("message") or item["args"], "text": a.get("rationale", "")}
            elif tool == "deploy":
                ok = '"ok": true' in (item.get("full") or "") or (item.get("result") or "").startswith("deployed")
                act = {"kind": "deploy" if ok else "refused",
                       "title": "Put its changes live" if ok else "Tried to put its changes live; the gate refused",
                       "text": "" if ok else clip(gate_failure(item), 900)}
            elif tool in ("discard", "rollback"):
                act = {"kind": "discard", "title": "Threw away its changes" if tool == "discard" else "Rolled the world back", "text": ""}
            elif tool == "speak":
                words = a.get("text", "")
                if a.get("target") == "location":
                    whom = f"everyone in {places.get(str(a.get('id')), 'a room')}"
                else:
                    whom = spoken_to.get(words[:60])
                    if whom:
                        names[a.get("id")] = whom
                act = {"kind": "speak", "title": f"Spoke to {whom}" if whom else None, "id": a.get("id"), "text": words}
            elif tool == "adjust":
                whom = a.get("characterId") or "someone"
                act = {"kind": "adjust", "title": f"Reached in and changed {whom}", "text": a.get("rationale") or json.dumps(a, ensure_ascii=False)}
            elif tool == "play":
                name = a.get("name") or played(item) or "someone"
                played_as.add(name)
                commands = a.get("commands") or []
                act = {"kind": "play", "title": f"Went in as {name}", "text": "\n".join(f"> {c}" for c in commands)}
            elif tool in ("write_file", "replace_in_file") and a.get("path") in OWN_FILES:
                what = OWN_FILES[a["path"]]
                act = {"kind": what, "title": "Rewrote what it believes" if what == "philosophy" else "Rewrote what it knows it can do",
                       "text": clip(a.get("content") or a.get("put") or a.get("replace") or a.get("new") or "", 900)}
            if act:
                acts.append({**base, "step": n, **act})

    # Its journal: the entry itself is the act; the page loads it on demand.
    for entry in index["journal"]:
        body = (out / "journal" / f"{entry['name']}.md").read_text()
        first = next((l.strip("# ").strip() for l in body.splitlines() if l.strip()), "")
        acts.append({"at": iso(stamp(entry["name"])), "turn": entry.get("turn"), "kind": "journal",
                     "title": "Wrote in its journal", "entry": entry["name"], "text": clip(first_paragraphs(body), 700),
                     "heading": first})

    # Some of what it said never reached the world's record; the same id often did.
    for act in acts:
        if act["kind"] == "speak":
            if not act["title"]:
                act["title"] = f"Spoke to {names.get(act['id'], 'someone')}"
            act.pop("id", None)

    for h in happened:
        h["own"] = h["who"] in played_as
    acts.sort(key=lambda e: (e["at"], e.get("step") or 10_000))
    happened.sort(key=lambda e: e["at"])
    (out / "events.json").write_text(json.dumps({"run": run, "acts": acts, "world": happened}, ensure_ascii=False, indent=1) + "\n")
    print(f"{run}: {len(acts)} acts, {len(happened)} things in the world")


def played(item):
    """Who it went in as, from what the world answered: a name it did not
    choose is one the world gave it."""
    try:
        return json.loads((item.get("full") or "").split("\n\n[")[0]).get("playedAs")
    except (json.JSONDecodeError, AttributeError):
        m = re.search(r'"playedAs":\s*"([^"]+)"', item.get("full") or "")
        return m and m[1]


def gate_failure(item):
    body = item.get("full") or item.get("result") or ""
    try:
        value = json.loads(body.split("\n\n[")[0])
        body = value.get("output") or value.get("error") or body
    except (json.JSONDecodeError, AttributeError):
        pass
    lines = [l for l in body.splitlines() if "FAIL" in l or "error" in l.lower()]
    return "\n".join(lines[:8]) or body


def first_paragraphs(body):
    paras = [p.strip() for p in body.split("\n\n") if p.strip() and not p.lstrip().startswith("#")]
    return "\n\n".join(paras[:2])


def lifetime(seconds):
    if seconds < 90:
        return f"{seconds} seconds"
    if seconds < 5400:
        return f"{round(seconds / 60)} minutes"
    return f"{round(seconds / 3600)} hours"


if __name__ == "__main__":
    runs = sys.argv[1:] or sorted(p.parent.name for p in LOG.glob("*/index.json"))
    for run in runs:
        build(run)
