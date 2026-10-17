// The timeline: what the caretaker did and wrote, turn by turn, above the line;
// who was born into its world, died there or got out, below it. Long quiet
// stretches are folded so the busy hours have room.

import { esc } from "./markdown.js";

export const GROUPS = {
  change: { label: "Changes to the world", kinds: ["commit", "deploy", "refused", "discard"] },
  words: { label: "What it said", kinds: ["speak"] },
  writing: { label: "What it wrote down", kinds: ["journal", "philosophy", "abilities"] },
  hands: { label: "What it did in person", kinds: ["play", "adjust"] },
};
const GROUP_OF = Object.fromEntries(Object.entries(GROUPS).flatMap(([g, { kinds }]) => kinds.map(k => [k, g])));
const KIND_LABEL = {
  commit: "Commit", deploy: "Live", refused: "Refused", discard: "Discarded", speak: "Spoke",
  journal: "Journal", philosophy: "Philosophy", abilities: "Abilities", play: "Played", adjust: "Reached in",
};
const GLYPH = { born: "+", died: "✕", escaped: "↗" };
const FOLD = 40 * 60e3; // a quiet stretch longer than this is folded

const parse = s => new Date(s);
const clock = d => d.toLocaleString("en-GB", { timeZone: "UTC", hour: "2-digit", minute: "2-digit" });
const day = d => d.toLocaleString("en-GB", { timeZone: "UTC", weekday: "short", day: "numeric", month: "short" });
const span = ms => ms >= 36e5 * 1.5 ? `${Math.round(ms / 36e5)}h` : `${Math.round(ms / 6e4)}m`;

export function renderTimeline(el, { run, events, turns, href, commitOf = {}, selected, onSelect }) {
  const commitFor = a => a.kind === "commit" ? commitOf[`${a.turn}#${a.step}`] : null;
  const sortedAs = a => commitFor(a)?.kind;
  const BECAUSE = { asked: "asked for", watched: "from watching", own: "its own idea" };
  const BADGE = { build: "Built", maintain: "Maintained", notes: "Notes" };
  const kindOfTurn = Object.fromEntries(turns.map(t => [t.name, t.kind]));
  // One moment per turn: everything it did in that waking.
  const byKey = new Map();
  for (const a of events.acts) {
    const k = a.turn ?? `entry:${a.entry}`;
    if (!byKey.has(k)) byKey.set(k, { key: k, at: parse(a.at), turn: a.turn, acts: [] });
    const m = byKey.get(k);
    m.acts.push(a);
    if (parse(a.at) < m.at) m.at = parse(a.at);
  }
  const all = [...byKey.values()].sort((a, b) => a.at - b.at);
  const world = events.world.map(w => ({ ...w, at: parse(w.at) }));

  let on = new Set(Object.keys(GROUPS)), showWorld = true;
  try { const saved = JSON.parse(sessionStorage.getItem("og-tl-groups")); if (Array.isArray(saved)) on = new Set(saved); } catch {}

  el.innerHTML = `<div class="chips" role="group" aria-label="Show">
      ${Object.entries(GROUPS).map(([g, { label }]) => `<button type="button" class="chip k-${g}-c" data-g="${g}" aria-pressed="${on.has(g)}"><i></i>${label}</button>`).join("")}
      <button type="button" class="chip" data-g="world" style="--c:var(--muted)" aria-pressed="true"><i></i>Births, deaths, escapes</button>
    </div>
    <p class="tl-key"><svg width="12" height="12" viewBox="-6 -6 12 12" aria-hidden="true"><path class="bmark" d="M0 -5 l5 5 l-5 5 l-5 -5 z" style="fill:var(--build)"></path></svg> marks a turn where it built something new</p>
    <div class="tl" id="tl"></div>
    <div class="stepper"><button type="button" id="tl-prev">← Earlier</button><div class="at" id="tl-at"></div><button type="button" id="tl-next">Later →</button></div>
    <div id="tl-detail"></div>`;

  const box = el.querySelector("#tl"), detail = el.querySelector("#tl-detail");
  let moments = [], current = null;

  const visible = m => m.acts.some(a => on.has(GROUP_OF[a.kind]));
  function refilter() {
    moments = all.filter(visible);
    if (!moments.length) { current = null; draw(); detail.innerHTML = `<p class="empty">Nothing to show with these filters.</p>`; el.querySelector("#tl-at").textContent = ""; return; }
    if (!current || !moments.includes(current)) current = moments.find(m => m.key === selected) ?? nearest(current) ?? moments[0];
    draw(); describe();
  }
  const nearest = m => m && moments.reduce((best, x) => Math.abs(x.at - m.at) < Math.abs(best.at - m.at) ? x : best, moments[0]);

  for (const chip of el.querySelectorAll(".chip")) chip.onclick = () => {
    const g = chip.dataset.g;
    if (g === "world") showWorld = !showWorld;
    else { on.has(g) ? on.delete(g) : on.add(g); if (!on.size) on.add(g); try { sessionStorage.setItem("og-tl-groups", JSON.stringify([...on])); } catch {} }
    for (const c of el.querySelectorAll(".chip")) c.setAttribute("aria-pressed", c.dataset.g === "world" ? showWorld : on.has(c.dataset.g));
    refilter();
  };
  el.querySelector("#tl-prev").onclick = () => step(-1);
  el.querySelector("#tl-next").onclick = () => step(1);
  function step(d) { const i = moments.indexOf(current) + d; if (moments[i]) choose(moments[i], true); }
  function choose(m, focus) {
    current = m; selected = m.key; onSelect(m.key);
    for (const g of box.querySelectorAll(".moment")) g.classList.toggle("on", g.dataset.key === m.key);
    describe();
    if (focus) box.querySelector(`.moment[data-key="${CSS.escape(m.key)}"]`)?.focus({ preventScroll: true });
  }

  // Time, folded: every moment and world event keeps its order, but a gap
  // longer than FOLD takes no more room than FOLD.
  function draw() {
    const W = Math.max(300, box.clientWidth || 700), H = 150, AX = 92, PAD = 14;
    const points = [...moments.map(m => +m.at), ...(showWorld ? world.map(w => +w.at) : [])].sort((a, b) => a - b);
    if (!points.length) { box.innerHTML = ""; return; }
    const folded = new Map(), gaps = [];
    let acc = 0;
    points.forEach((t, i) => {
      if (i) { const d = t - points[i - 1]; if (d > FOLD) gaps.push({ at: acc + FOLD / 2, real: d }); acc += Math.min(d, FOLD); }
      folded.set(t, acc);
    });
    const total = acc || 1;
    const x = t => PAD + (folded.get(+t) / total) * (W - PAD * 2);
    const minGap = moments.length > 1 ? Math.min(...moments.slice(1).map((m, i) => x(m.at) - x(moments[i].at))) : 20;
    const hitW = Math.max(6, Math.min(18, minGap || 18));

    const marks = moments.map(m => {
      const counts = {};
      for (const a of m.acts) { const g = GROUP_OF[a.kind]; if (on.has(g)) counts[g] = (counts[g] || 0) + 1; }
      let y = AX - 4;
      const blocks = Object.keys(GROUPS).filter(g => counts[g]).map(g => {
        const h = Math.min(30, 3 + counts[g] * 3);
        y -= h + 1;
        return `<rect class="k-${g}" x="${x(m.at) - 2}" y="${y}" width="4" height="${h}" rx="1"></rect>`;
      }).join("");
      const top = Math.max(22, y - 8);
      const kind = kindOfTurn[m.turn];
      return `<g class="moment${m === current ? " on" : ""}" data-key="${esc(m.key)}" tabindex="-1" role="button"
          aria-label="${esc(`${day(m.at)} ${clock(m.at)}, ${m.acts.length} things`)}">
        <title>${esc(`${day(m.at)}, ${clock(m.at)} UTC${kind ? ` · ${kind === "deliberate" ? "long turn" : "short turn"}` : ""}\n${summary(m)}`)}</title>
        <rect class="hit" x="${x(m.at) - hitW / 2}" y="0" width="${hitW}" height="${AX + 4}"></rect>
        <line class="stem" x1="${x(m.at)}" x2="${x(m.at)}" y1="${top}" y2="${AX}"></line>
        <path class="flag" d="M${x(m.at) - 5} ${top - 8} h10 l-5 7 z"></path>${blocks}
        ${on.has("change") && m.acts.some(a => sortedAs(a) === "build") ? `<path class="bmark" d="M${x(m.at)} ${AX - 5} l5 5 l-5 5 l-5 -5 z"></path>` : ""}</g>`;
    }).join("");
    const below = showWorld ? world.map(w => `<text class="w w-${w.kind}${w.own ? " own" : ""}" x="${x(w.at)}" y="${AX + 18}">
        <title>${esc(`${clock(w.at)} UTC · ${w.text || `${w.who} ${w.kind === "born" ? "was born" : w.kind === "escaped" ? "got out" : "died"}`}${w.own ? " (one of its own characters)" : ""}`)}</title>${GLYPH[w.kind]}</text>`).join("") : "";
    const folds = gaps.filter(g => g.real > 2 * 36e5).map(g => {
      const gx = PAD + (g.at / total) * (W - PAD * 2);
      return `<line class="gapline" x1="${gx - 7}" x2="${gx + 7}" y1="${AX}" y2="${AX}"></line><text class="gap" x="${gx}" y="${AX + 34}">⋯ ${span(g.real)}</text>`;
    }).join("");
    const first = new Date(points[0]), last = new Date(points.at(-1));
    box.innerHTML = `<svg viewBox="0 0 ${W} ${H}" role="group" aria-label="Timeline of the run">
      <text class="side" x="0" y="10">the caretaker</text>
      ${showWorld ? `<text class="side" x="0" y="${AX + 44}">the world</text>` : ""}
      <line class="axis" x1="0" x2="${W}" y1="${AX}" y2="${AX}"></line>${folds}${marks}${below}
      <text class="when" x="${PAD}" y="${H - 2}">${esc(day(first))}, ${esc(clock(first))}</text>
      <text class="when" x="${W - PAD}" y="${H - 2}" text-anchor="end">${esc(day(last))}, ${esc(clock(last))} UTC</text></svg>`;
    for (const g of box.querySelectorAll(".moment")) g.onclick = () => choose(moments.find(m => m.key === g.dataset.key), true);
    const svg = box.querySelector("svg");
    svg.onkeydown = e => {
      if (e.key === "ArrowLeft" || e.key === "ArrowRight") { e.preventDefault(); step(e.key === "ArrowLeft" ? -1 : 1); }
    };
    box.querySelector(".moment.on")?.setAttribute("tabindex", "0");
  }

  function summary(m) {
    const n = {};
    for (const a of m.acts) n[a.kind] = (n[a.kind] || 0) + 1;
    return Object.entries(n).map(([k, c]) => `${c} × ${KIND_LABEL[k].toLowerCase()}`).join(", ");
  }

  function describe() {
    const m = current;
    const i = moments.indexOf(m);
    el.querySelector("#tl-prev").disabled = i <= 0;
    el.querySelector("#tl-next").disabled = i >= moments.length - 1;
    const kind = kindOfTurn[m.turn];
    el.querySelector("#tl-at").innerHTML = `${esc(day(m.at))}, ${esc(clock(m.at))} UTC
      <small>${kind ? (kind === "deliberate" ? "a long turn" : "a short turn") : "a journal entry"} · ${i + 1} of ${moments.length}</small>`;

    const acts = m.acts.filter(a => on.has(GROUP_OF[a.kind]));
    const live = acts.filter(a => a.kind === "deploy");
    const shown = acts.filter(a => a.kind !== "deploy");
    const items = shown.map(a => {
      const g = GROUP_OF[a.kind];
      const links = [];
      if (a.kind === "journal") links.push(`<a href="${href(run, "log", "journal", a.entry)}">Read the whole entry</a>`);
      if (a.turn && a.step) links.push(`<a href="${href(run, "log", "turn", a.turn, "step", a.step)}">See this in the log</a>`);
      const long = (a.text || "").length > 420 || (a.text || "").split("\n").length > 7;
      const c = commitFor(a), sorted = c?.kind;
      const badge = sorted ? `${BADGE[sorted]}${c.first ? " · first build" : c.because ? ` · ${BECAUSE[c.because]}` : ""}` : "";
      return `<li class="act k-${g}-c"><div class="head"><span class="k">${KIND_LABEL[a.kind]}</span>${sorted ? `<span class="badge b-${sorted}">${badge}</span>` : ""}<span class="title">${esc(a.title)}</span></div>
        ${c?.why ? `<div class="because"><span>Because</span> ${esc(c.why)}</div>` : ""}
        ${a.text ? `<div class="text${a.kind === "play" || a.kind === "refused" ? " mono" : ""}${long ? " clip" : ""}">${esc(a.text)}</div>` : ""}
        ${long || links.length ? `<div class="links">${long ? `<button type="button" class="more">Show all</button>` : ""}${links.join("")}</div>` : ""}</li>`;
    }).join("");
    const liveNote = live.length ? `<li class="act k-change-c"><div class="head"><span class="k">Live</span><span class="title">Put its changes live${live.length > 1 ? ` ${live.length} times` : ""}, through the gate</span></div></li>` : "";
    const happened = showWorld ? world.filter(w => w.at <= m.at && (i === 0 || w.at > moments[i - 1].at)) : [];
    detail.innerHTML = `<ul class="acts">${items}${liveNote}</ul>
      ${happened.length ? `<p class="worldnote">Since the moment before: ${esc(happened.map(w => w.kind === "died" ? `${w.who} died` : w.kind === "born" ? `${w.who} was born` : `${w.who} got out`).join(", "))}.</p>` : ""}
      ${m.turn ? `<p class="worldnote"><a href="${href(run, "log", "turn", m.turn)}">Open the whole turn</a></p>` : ""}`;
    for (const b of detail.querySelectorAll(".more")) b.onclick = () => {
      const t = b.closest(".act").querySelector(".text");
      t.classList.toggle("clip"); b.textContent = t.classList.contains("clip") ? "Show all" : "Show less";
    };
  }

  let width = box.clientWidth;
  const ro = new ResizeObserver(() => { if (Math.abs(box.clientWidth - width) > 4) { width = box.clientWidth; draw(); } });
  ro.observe(box);
  refilter();
  return () => ro.disconnect();
}
