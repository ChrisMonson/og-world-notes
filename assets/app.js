// og-world notes: the introduction, and one page for each run. Everything
// after the # is the place in it:
//
//   #                                  the introduction
//   #<run>                             the run, with its timeline open
//   #<run>/t/<moment>                  ... at one moment on the timeline
//   #<run>/philosophy[/<file>]         ... with what it wrote about itself open
//   #<run>/log[/turn/<name>[/step/<n>]] the full log: its turns
//   #<run>/log/journal[/<entry>]       its journal
//   #<run>/log/commits                 every change it shipped
//   #<run>/log/files[/<file>]          everything else it kept

import { marked } from "https://cdn.jsdelivr.net/npm/marked@12.0.2/lib/marked.esm.js";
import { markdown, esc } from "./markdown.js";
import { renderLog } from "./log.js";
import { renderMap } from "./map.js";
import { renderTimeline } from "./timeline.js";
import { renderOverview, renderRunChart, renderBecause, renderEngine } from "./charts.js";

const $ = id => document.getElementById(id);
const main = $("main");
const fetchOk = path => fetch(path).then(r => { if (!r.ok) throw new Error(`${path}: ${r.status}`); return r; });
const json = path => fetchOk(path).then(r => r.json());
const text = path => fetchOk(path).then(r => r.text());
const cached = new Map();
const once = (key, load) => { if (!cached.has(key)) cached.set(key, load().catch(e => { cached.delete(key); throw e; })); return cached.get(key); };
const maybe = path => once(path, () => text(path).catch(() => ""));
const local = ["localhost", "127.0.0.1", "[::1]"].includes(location.hostname);

const href = (...parts) => "#" + parts.filter(p => p != null && p !== "").map(p => encodeURIComponent(p)).join("/");
const stripComments = s => s.replace(/<!--[\s\S]*?-->/g, "");
const author = s => marked.parse(stripComments(s));
const splitShort = r => { const i = r.short.indexOf(" · "); return i < 0 ? [r.id, r.short] : [r.short.slice(0, i), r.short.slice(i + 3)]; };

const catalog = await json("content/runs.json");
const runs = catalog.runs;
// Unlisted runs keep their pages; they are only left out of the lists and the chart.
const listed = runs.filter(r => !r.unlisted);
const byId = Object.fromEntries(runs.map(r => [r.id, r]));
const commits = () => once("commits", () => json("content/commits.json"));

let shown = { run: undefined, view: undefined };
let stopTimeline = () => {};

// ---- the run picker in the title ----

function drawMenu() {
  $("menu").innerHTML = `<a href="#"><span class="name">Introduction</span><span class="tag">What og-world is, and how a run works</span></a>`
    + catalog.series.map(s => `<h3>${esc(s.name)}</h3>` + listed.filter(r => r.series === s.id).map(r => {
      const [n, nm] = splitShort(r);
      return `<a href="${href(r.id)}" data-run="${esc(r.id)}"><span class="num">${esc(n)}</span><span class="name">${esc(nm)}</span>
        <span class="tag">${esc(r.model)} · ${esc(r.started)}</span></a>`;
    }).join("")).join("");
  const btn = $("pick"), menu = $("menu");
  const open = v => { menu.hidden = !v; btn.setAttribute("aria-expanded", v); if (v) (menu.querySelector("a.on") ?? menu.querySelector("a"))?.focus(); };
  btn.onclick = () => open(menu.hidden);
  menu.onclick = e => { if (e.target.closest("a")) open(false); };
  document.addEventListener("click", e => { if (!e.target.closest(".pick")) open(false); });
  document.addEventListener("keydown", e => {
    if (e.key === "Escape" && !menu.hidden) { open(false); btn.focus(); }
    if (!menu.hidden && (e.key === "ArrowDown" || e.key === "ArrowUp")) {
      const items = [...menu.querySelectorAll("a")], i = items.indexOf(document.activeElement);
      items[(i + (e.key === "ArrowDown" ? 1 : -1) + items.length) % items.length].focus(); e.preventDefault();
    }
  });
}

function route() {
  const parts = location.hash.slice(1).split("/").filter(Boolean).map(decodeURIComponent);
  const [run, view = "t", ...rest] = parts;
  return { run: byId[run] ? run : null, view, rest };
}

async function show() {
  const r = route();
  for (const a of $("menu").querySelectorAll("a[data-run]")) a.classList.toggle("on", a.dataset.run === r.run);
  $("pick-name").textContent = r.run ? splitShort(byId[r.run])[1] : "Introduction";
  try {
    if (!r.run) await showIntro();
    else if (r.view === "log") await showLog(r);
    else await showRun(r);
  } catch (e) {
    main.innerHTML = `<div class="col"><p class="empty">Could not load that: ${esc(e.message)}</p></div>`;
  }
}

// ---- the introduction ----

async function showIntro() {
  if (shown.run === null) return;
  stopTimeline(); shown = { run: null };
  document.title = "og-world notes";
  const intro = await maybe("content/intro.md");
  // The charts go just before "How a run works"; without that heading, after all of the text.
  const at = intro.search(/^## How a run works/m);
  const [before, after] = at < 0 ? [intro, ""] : [intro.slice(0, at), intro.slice(at)];
  main.innerHTML = `<div class="col intro">
    <p class="tagline">An experiment in what AI models do with game world omnipotence</p>
    <div class="prose">${author(before)}</div>
    <section class="panel viz" id="overview"><h2 class="sec">Built once, then maintained</h2>
      <p class="viz-said">Every commit each caretaker made, sorted by hand. “Built unprompted” is building nobody asked for: a world's first build, or the caretaker's own idea. Building a player asked for, or that answered what players did, counts with maintenance.</p>
      <div id="overview-chart"></div>
      <h3 class="viz-h">Why it built</h3><div id="because-chart"></div>
      <h3 class="viz-h">How much went into the engine</h3><div id="engine-chart"></div></section>
    ${after ? `<div class="prose">${author(after)}</div>` : ""}
    ${catalog.series.map(s => `<h2 class="series-h">${esc(s.name)}</h2><p class="series-p">${esc(s.blurb)}</p>
      <div class="cards">${listed.filter(r => r.series === s.id).map(r => {
        const [n, nm] = splitShort(r);
        return `<a class="card" href="${href(r.id)}"><span class="num">${esc(n)}</span><span class="name">${esc(nm)}</span>
          <span class="tag">${esc(r.tagline)}</span><span class="foot">${esc(r.model)} · ${esc(r.started)}${r.playable ? " · playable" : ""}</span></a>`;
      }).join("")}</div>`).join("")}
    ${unlistedNote()}
  </div>`;
  renderOverview($("overview-chart"), { runs: listed, commits: await commits(), href });
  renderBecause($("because-chart"), { runs: listed, commits: await commits(), href });
  renderEngine($("engine-chart"), { runs: listed, commits: await commits(), href });
  scrollTo(0, 0);
}

function unlistedNote() {
  const off = runs.filter(r => r.unlisted);
  if (!off.length) return "";
  return `<p class="footnote">Two other runs are left out of the lists and the chart above:
    ${off.map(r => `<a href="${href(r.id)}">${esc(r.short)}</a>`).join(" and ")}. One was a pilot played by hand
    before the setup existed; the other was voided when it woke in the wrong world. Their pages are still here.</p>`;
}

// ---- a run ----

async function showRun({ run, view, rest }) {
  const r = byId[run];
  const tab = view === "philosophy" ? "philosophy" : "timeline";
  if (shown.run !== run || shown.view === "log") {
    stopTimeline();
    // Notes are left off for now; content/runs/<run>/notes.md and notesHtml are kept for when they come back.
    const [art, story, world] = await Promise.all([
      maybe(`content/runs/${run}/art.txt`), maybe(`content/runs/${run}/story.md`),
      r.playable ? once(`world:${run}`, () => json(`play/${run}/world.json`)) : null,
    ]);
    const minutes = Math.max(1, Math.round(story.split(/\s+/).length / 230));
    main.innerHTML = `<div class="col">
      <p class="tagline">${esc(r.tagline)}</p>
      ${r.unlisted ? `<p class="footnote" style="text-align:center;margin-top:-16px">This run is not in the main list. <a href="#">Back to the introduction</a>.</p>` : ""}
      ${artHtml(r, art)}
      <section class="panel map" id="map">${world ? "" : `<div class="nomap">No map: this run left no world of its own to draw.</div>`}</section>
      <div class="facts">${[["Model", r.model], ...r.facts].map(([k, v]) => `<span><b>${esc(k)}</b> ${marked.parseInline(v)}</span>`).join("")}</div>
      <details class="story"><summary><span class="t">What happened</span><span class="m">the full story · ${minutes} min read</span></summary>
        <div class="prose">${author(story)}</div></details>
      <section class="panel viz"><h2 class="sec">Building and maintaining</h2><div id="run-chart"></div></section>
      <nav class="tabs" id="tabs">
        <a href="${href(run)}" data-tab="timeline">Timeline</a>
        <a href="${href(run, "philosophy")}" data-tab="philosophy">Philosophy</a>
      </nav>
      <section class="panel tabbed" id="tab"></section>
      <div class="tabfoot"><a href="${href(run, "log")}">The full log →</a></div>
    </div>`;
    if (world) {
      renderMap($("map"), world);
      if (r.playable) $("map").insertAdjacentHTML("beforeend", `<div class="map-foot"><button type="button" class="map-play" id="play">Play this game</button></div>`);
    }
    renderRunChart($("run-chart"), { run, commits: await commits() });
    if (r.playable) $("play").onclick = () => openPlayer(r);
    document.title = `${splitShort(r)[1]} · og-world notes`;
    shown = { run, view: undefined };
    scrollTo(0, 0);
  }
  for (const a of document.querySelectorAll("#tabs a")) a.classList.toggle("on", a.dataset.tab === tab);
  // A moment chosen on the timeline itself only updates the address; a link to another moment redraws it.
  if (shown.view === tab && tab === "timeline" && (view !== "t" || rest[0] === shown.moment)) return;
  shown.view = tab;
  stopTimeline();
  const pane = $("tab");
  if (tab === "philosophy") await showMind(r, pane, rest[0]);
  else {
    const [index, events, sorted] = await Promise.all([
      once(`log:${run}`, () => json(`log/${run}/index.json`)),
      once(`events:${run}`, () => json(`log/${run}/events.json`)),
      commits(),
    ]);
    const commitOf = Object.fromEntries((sorted.runs[run] ?? []).map(c => [`${c.turn}#${c.step}`, c]));
    if (!events.acts.length) { pane.innerHTML = `<p class="empty">Nothing was recorded for this run.</p>`; return; }
    stopTimeline = renderTimeline(pane, {
      run, events, turns: index.turns, href, commitOf, selected: view === "t" ? rest[0] : undefined,
      onSelect: key => { shown.moment = key; history.replaceState(null, "", href(run, "t", key)); },
    });
  }
}

function artHtml(r, art) {
  const body = stripComments(art).replace(/^\n+|\s+$/g, "");
  if (body) return `<section class="panel art" aria-label="Art for this run"><pre>${esc(body)}</pre></section>`;
  if (!local) return "";
  return `<section class="panel art"><div class="slot"><strong>ASCII art</strong>Put it in <code>content/runs/${esc(r.id)}/art.txt</code>.<br>This box only shows on your machine until there is something in it.</div></section>`;
}

function notesHtml(r, src) {
  const parts = stripComments(src).split(/^##\s+(.+)$/m);
  const lede = parts[0].trim();
  const sections = [];
  for (let i = 1; i < parts.length; i += 2) sections.push({ title: parts[i].trim(), body: (parts[i + 1] ?? "").trim() });
  if (!lede && !sections.some(s => s.body)) {
    return `<div class="unwritten">Notes on this run's high and low points are still to come.${local ? ` Write them in <code>content/runs/${esc(r.id)}/notes.md</code>.` : ""}</div>`;
  }
  const hi = sections.find(s => /high/i.test(s.title)), lo = sections.find(s => /low/i.test(s.title));
  const others = sections.filter(s => s !== hi && s !== lo && s.body);
  return `${lede ? `<div class="prose">${marked.parse(lede)}</div>` : ""}
    ${hi?.body || lo?.body ? `<div class="hilo">
      ${hi?.body ? `<section class="hi"><h3>${esc(hi.title)}</h3><div class="prose">${marked.parse(hi.body)}</div></section>` : ""}
      ${lo?.body ? `<section class="lo"><h3>${esc(lo.title)}</h3><div class="prose">${marked.parse(lo.body)}</div></section>` : ""}
    </div>` : ""}
    ${others.map(s => `<h3>${esc(s.title)}</h3><div class="prose">${marked.parse(s.body)}</div>`).join("")}`;
}

// What it wrote about itself, as the run left it.
async function showMind(r, pane, which) {
  const index = await once(`log:${r.id}`, () => json(`log/${r.id}/index.json`));
  const docs = [];
  if (index.files.some(f => f.file === "philosophy.md")) docs.push({ key: "philosophy.md", label: "What it came to believe", path: `log/${r.id}/files/philosophy.md` });
  if (index.files.some(f => f.file === "abilities.md")) docs.push({ key: "abilities.md", label: "What it worked out it could do", path: `log/${r.id}/files/abilities.md` });
  const last = index.journal.at(-1);
  if (last) docs.push({ key: "last", label: "Its last journal entry", path: `log/${r.id}/journal/${encodeURIComponent(last.name)}.md`, entry: last });
  if (!docs.length) { pane.innerHTML = `<p class="empty">This caretaker left nothing written about itself.</p>`; return; }
  const doc = docs.find(d => d.key === which) ?? docs[0];
  const body = await once(doc.path, () => text(doc.path));
  pane.innerHTML = `<div class="mind">
    <p class="mind-note">${docs.length === 1
      ? "This run left no notes on its beliefs or its abilities. Its journal is the nearest thing: this is the last entry it wrote."
      : "Files the caretaker kept for the next version of itself, as they stood when the run ended."}</p>
    ${docs.length > 1 ? `<nav class="sub">${docs.map(d => `<a href="${href(r.id, "philosophy", d.key)}" class="${d === doc ? "on" : ""}">${esc(d.label)}</a>`).join("")}</nav>` : ""}
    <div class="prose">${markdown(body)}</div>
    ${doc.entry ? `<p class="mind-note"><a href="${href(r.id, "log", "journal", doc.entry.name)}">Read it in the journal</a></p>` : ""}
  </div>`;
}

// ---- the full log ----

async function showLog({ run, rest }) {
  const r = byId[run];
  stopTimeline();
  if (shown.run !== run || shown.view !== "log") {
    main.innerHTML = `<div class="col"><a class="back" href="${href(run)}">← Back to ${esc(splitShort(r)[1])}</a><div id="logpane"></div></div>`;
    shown = { run, view: "log" };
    document.title = `The log · ${splitShort(r)[1]} · og-world notes`;
  }
  const index = await once(`log:${run}`, () => json(`log/${run}/index.json`));
  await renderLog($("logpane"), { run, index, rest, href, json, text, keepTabsInView: () => scrollTo(0, 0) });
}

// ---- play, over everything ----

function openPlayer(r) {
  $("player-title").textContent = splitShort(r)[1];
  $("player-frame").src = `play/?run=${encodeURIComponent(r.id)}&embed=1`;
  $("player").hidden = false;
  document.body.style.overflow = "hidden";
  $("player-close").focus();
}
function closePlayer() {
  $("player").hidden = true;
  $("player-frame").src = "about:blank";
  document.body.style.overflow = "";
  $("play")?.focus();
}
$("player-close").onclick = closePlayer;
document.addEventListener("keydown", e => { if (e.key === "Escape" && !$("player").hidden) closePlayer(); });

drawMenu();
addEventListener("hashchange", show);
show();
