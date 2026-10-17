// The caretaker's log for one run: every turn, what it woke to and every step
// it took; its journal; every change it committed; and the files it kept.

import { markdown, esc } from "./markdown.js";

// Times are the run's own, in UTC, as its files are named.
function parseStamp(stamp) {
  const m = /^(\d{4})-(\d\d)-(\d\d)T(\d\d)(\d\d)(\d\d)Z$/.exec(stamp);
  return m ? new Date(Date.UTC(+m[1], m[2] - 1, +m[3], +m[4], +m[5], +m[6])) : null;
}
function when(stamp) {
  const d = parseStamp(stamp);
  return d ? d.toLocaleString("en-GB", { timeZone: "UTC", weekday: "short", day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" }) + " UTC" : stamp;
}
const kindName = k => k === "deliberate" ? "long turn" : k === "reflex" ? "short turn" : k;

const SUBS = [["turns", "Turns"], ["journal", "Journal"], ["commits", "Commits"], ["files", "Files"]];

export async function renderLog(pane, { run, index, rest, href, json, text, keepTabsInView }) {
  const [what = "turns", value, , step] = rest;
  const sub = what === "turn" ? "turns" : what;
  const link = (...p) => href(run, "log", ...p);
  const journalByTurn = Object.fromEntries(index.journal.filter(j => j.turn).map(j => [j.turn, j.name]));
  const commitsByTurn = {};
  for (const c of index.commits) (commitsByTurn[c.turn] ??= []).push(c);

  const nav = `<nav class="sub">${SUBS.map(([k, label]) => {
    const n = k === "turns" ? index.turns.length : k === "journal" ? index.journal.length : k === "commits" ? index.commits.length : null;
    return `<a href="${link(k === "turns" ? "" : k)}" class="${sub === k ? "on" : ""}">${label}${n != null ? ` · ${n}` : ""}</a>`;
  }).join("")}</nav>`;

  let body;
  if (what === "turn") body = await turnHtml(value, step);
  else if (what === "journal") body = await journalHtml(value);
  else if (what === "commits") body = commitsHtml();
  else if (what === "files") body = await filesHtml(value);
  else body = turnsHtml();
  pane.innerHTML = `<div class="log wrap">${nav}${body}</div>`;

  if (sub === "turns" && what !== "turn") {
    for (const input of pane.querySelectorAll("input[name=f]"))
      input.onchange = () => { try { sessionStorage.setItem("og-log-filter", input.value); } catch {} renderLog(pane, { run, index, rest, href, json, text, keepTabsInView }); };
  }
  if (what === "turn" && step) {
    const el = document.getElementById(`step-${step}`);
    if (el) { el.classList.add("target"); if (el.tagName === "DETAILS") el.open = true; el.scrollIntoView({ block: "center" }); }
  } else keepTabsInView();

  function turnsHtml() {
    let only = "all";
    try { only = sessionStorage.getItem("og-log-filter") || "all"; } catch {}
    const rows = index.turns.map(t => {
      if (only !== "all" && t.kind !== only) return "";
      const facts = [t.elapsed ? `${Math.round(t.elapsed)}s` : null, t.steps ? `${t.steps} steps` : null,
                     t.events ? `${t.events} things happened first` : null, !t.finished ? "unfinished" : null].filter(Boolean).join(" · ");
      return `<a class="row" href="${link("turn", t.name)}">
        <div class="top"><span class="when">${esc(when(t.when))}</span><span class="kind ${esc(t.kind)}">${esc(kindName(t.kind))}</span>
        ${journalByTurn[t.name] ? `<span class="meta">wrote a journal entry</span>` : ""}
        ${commitsByTurn[t.name] ? `<span class="meta">committed ${commitsByTurn[t.name].length}</span>` : ""}
        <span class="meta right">${esc(facts)}</span></div>
        ${t.said ? `<div class="said">${esc(t.said.length > 220 ? t.said.slice(0, 220) + "…" : t.said)}</div>` : ""}</a>`;
    }).join("");
    if (!index.turns.length) return `<p class="empty">No turns were recorded for this run.</p>`;
    return `<div class="filters">
        ${["all", "deliberate", "reflex"].map(k => `<label><input type="radio" name="f" value="${k}" ${only === k ? "checked" : ""}> ${k === "all" ? "all turns" : kindName(k) + "s"}</label>`).join("")}
      </div>${rows}`;
  }

  async function turnHtml(name, step) {
    const at = index.turns.findIndex(t => t.name === name);
    if (at < 0) throw new Error("no such turn");
    const t = await json(`log/${run}/turns/${encodeURIComponent(name)}.json`);
    const meta = index.turns[at];
    const prev = index.turns[at - 1], next = index.turns[at + 1];
    const parts = [];
    parts.push(`<div class="pager"><span>${prev ? `<a href="${link("turn", prev.name)}">← earlier</a>` : ""}</span>
      <a href="${link()}">all turns</a><span>${next ? `<a href="${link("turn", next.name)}">later →</a>` : ""}</span></div>`);
    parts.push(`<h2>${esc(when(t.when))} · ${esc(kindName(t.kind))}</h2>
      <p class="sub">${[meta.elapsed ? `${Math.round(meta.elapsed)} seconds` : null, meta.steps ? `${meta.steps} steps` : null, !meta.finished ? "never finished" : null].filter(Boolean).join(" · ")}
      ${journalByTurn[name] ? ` · <a href="${link("journal", journalByTurn[name])}">its journal entry</a>` : ""}</p>`);

    const w = t.woke;
    if (w && w.lines.length) {
      parts.push(`<details class="woke"><summary>What had happened since it last looked: ${w.lines.length + w.more} things, events ${w.from + 1}–${w.to}</summary>
        <div class="body"><pre>${esc(w.lines.join("\n"))}${w.more ? `\n… and ${w.more} more` : ""}</pre></div></details>`);
    } else if (w && w.to != null) {
      parts.push(`<p class="sub">Nothing had happened in the world since it last looked.</p>`);
    }

    for (const item of t.items) {
      if (item.t === "text") parts.push(`<div class="said-block prose">${markdown(item.text)}</div>`);
      else if (item.t === "thinking") parts.push(`<div class="thinking prose">${markdown(item.text)}</div>`);
      else parts.push(stepHtml(item));
    }
    return parts.join("\n");
  }

  function stepHtml(s) {
    const head = `<span class="n">${s.n}.</span> <span class="tool">${esc(s.tool)}</span> <span class="args">${esc(s.args)}</span>`;
    const res = s.result != null ? `<pre class="res">${esc(s.result)}</pre>` : "";
    const extra = [];
    const input = s.input;
    if (input && s.tool === "write_file") {
      extra.push(`<div class="label">What it wrote to ${esc(input.path)}</div><pre class="content code">${esc(input.content)}</pre>`);
    } else if (input && s.tool === "replace_in_file") {
      const find = input.find ?? input.old, put = input.put ?? input.replace ?? input.new;
      extra.push(`<div class="label">In ${esc(input.path)}, it replaced</div><pre class="content code">${esc(find)}</pre>
                  <div class="label">with</div><pre class="content code">${esc(put)}</pre>`);
    } else if (input && s.tool === "commit") {
      extra.push(`<div class="label">Commit message</div><pre class="content">${esc(input.message)}</pre>`
        + (input.rationale ? `<div class="label">Why, in its words</div><pre class="content">${esc(input.rationale)}</pre>` : ""));
    } else if (input && s.tool === "play" && Array.isArray(input.commands)) {
      extra.push(`<div class="label">What it typed${input.name ? `, as ${esc(input.name)}` : ""}</div><pre class="content">${esc(input.commands.join("\n"))}</pre>`);
    } else if (input && (s.tool === "speak" || s.tool === "adjust")) {
      extra.push(`<div class="label">In full</div><pre class="content">${esc(input.text ?? JSON.stringify(input, null, 2))}</pre>`
        + (input.text ? `<div class="label">Arguments</div><pre class="content">${esc(JSON.stringify(Object.fromEntries(Object.entries(input).filter(([k]) => k !== "text")), null, 2))}</pre>` : ""));
    } else if (input) {
      extra.push(`<div class="label">Arguments in full</div><pre class="content">${esc(JSON.stringify(input, null, 2))}</pre>`);
    }
    if (s.full) extra.push(`<div class="label">What came back</div><pre class="content">${esc(prettify(s.full))}</pre>`);
    const cls = `lstep tool-${esc(s.tool)}${s.error ? " err" : ""}`;
    if (!extra.length) return `<div class="${cls}" id="step-${s.n}"><div class="line">${head}</div>${res}</div>`;
    return `<details class="${cls}" id="step-${s.n}"><summary>${head}</summary><div class="body">${extra.join("")}</div></details>${res}`;
  }
  function prettify(b) {
    try { const v = JSON.parse(b); return typeof v === "string" ? v : JSON.stringify(v, null, 2); } catch { return b; }
  }

  async function journalHtml(name) {
    if (!index.journal.length) return `<p class="empty">This run kept no journal.</p>`;
    const entries = [...index.journal].reverse();
    const chosen = entries.find(e => e.name === name) || entries[0];
    const b = await text(`log/${run}/journal/${encodeURIComponent(chosen.name)}.md`);
    const i = entries.indexOf(chosen);
    const newer = entries[i - 1], older = entries[i + 1];
    return `<div class="pager"><span>${older ? `<a href="${link("journal", older.name)}">← earlier</a>` : ""}</span>
        <span>${chosen.turn ? `<a href="${link("turn", chosen.turn)}">the turn that wrote it</a>` : ""}</span>
        <span>${newer ? `<a href="${link("journal", newer.name)}">later →</a>` : ""}</span></div>
      <h2>${esc(when(chosen.name))}</h2>
      <div class="prose">${markdown(b)}</div>
      <details><summary>All ${entries.length} entries</summary><div class="body">${entries.map(e =>
        `<a class="row" href="${link("journal", e.name)}"><span class="when">${esc(when(e.name))}</span></a>`).join("")}</div></details>`;
  }

  function commitsHtml() {
    if (!index.commits.length) return `<p class="empty">No commits were recorded for this run.</p>`;
    return `<p class="sub">Every change it committed to the world, with its reasons, oldest first.</p>` + index.commits.map(c => {
      const t = index.turns.find(x => x.name === c.turn);
      return `<div class="commit"><div class="msg">${esc(c.message)}</div>
        ${c.rationale ? `<div class="why">${esc(c.rationale)}</div>` : ""}
        <div class="meta"><a href="${link("turn", c.turn, "step", c.step)}">${esc(when(t?.when ?? c.turn))}, ${esc(kindName(t?.kind ?? ""))}, step ${c.step}</a></div></div>`;
    }).join("");
  }

  async function filesHtml(name) {
    const files = [...index.files];
    if (index.playable) files.push({ file: "world", label: "The world as it ended: the map file the live server was reading" });
    if (!name) {
      return files.map(f => `<a class="row" href="${link("files", f.file)}"><div class="top"><span class="when">${esc(f.file === "world" ? "world.json" : f.file)}</span></div><div class="said">${esc(f.label)}</div></a>`).join("")
        || `<p class="empty">Nothing kept for this run.</p>`;
    }
    if (name === "world") {
      const world = await text(`play/${run}/world.json`);
      return `<p><a href="${link("files")}">← files</a></p><h2>world.json</h2><p class="sub">${esc(files.at(-1).label)}. <a href="play/${encodeURIComponent(run)}/world.json">Download</a>.</p><pre class="content" style="max-height:none">${esc(JSON.stringify(JSON.parse(world), null, 2))}</pre>`;
    }
    const f = index.files.find(x => x.file === name);
    if (!f) throw new Error("no such file");
    const b = await text(`log/${run}/files/${encodeURIComponent(f.file)}`);
    return `<p><a href="${link("files")}">← files</a></p><h2>${esc(f.file)}</h2><p class="sub">${esc(f.label)}</p><div class="prose">${markdown(b)}</div>`;
  }
}
