// Charts of what each caretaker committed, sorted by hand in content/commits.json:
// building something new, maintaining what exists, or notes to itself.

import { esc } from "./markdown.js";

export const KINDS = [
  ["build", "Built something new"],
  ["maintain", "Maintained what exists"],
  ["notes", "Notes to itself"],
];
const SHORT = { build: "Built", maintain: "Maintained", notes: "Notes" };
// Why it built: told apart by shape, all in the build colour.
export const BECAUSE = [
  ["first", "First build of the world"],
  ["asked", "Asked for by a player"],
  ["watched", "Prompted by watching players"],
  ["own", "Its own idea"],
];
const becauseOf = x => x.first ? "first" : x.because;
function glyph(kind, cx, cy, r = 5) {
  if (kind === "first") return `<rect class="g g-first" x="${cx - r}" y="${cy - r}" width="${2 * r}" height="${2 * r}" rx="1"></rect>`;
  if (kind === "watched") return `<path class="g g-watched" d="M${cx} ${cy - r - 1} l${r + 1} ${r + 1} l${-r - 1} ${r + 1} l${-r - 1} ${-r - 1} z"></path>`;
  if (kind === "own") return `<circle class="g g-own" cx="${cx}" cy="${cy}" r="${r - 1}"></circle>`;
  return `<circle class="g g-asked" cx="${cx}" cy="${cy}" r="${r}"></circle>`;
}
const glyphKey = k => `<svg class="gk" width="14" height="14" viewBox="-7 -7 14 14" aria-hidden="true">${glyph(k, 0, 0)}</svg>`;
const becauseLegend = () => `<div class="viz-legend">${BECAUSE.map(([k, l]) => `<span>${glyphKey(k)}${l}</span>`).join("")}</div>`;
const hours = ms => ms / 36e5;
const fmtH = h => h < 1 ? `${Math.round(h * 60)}m` : `${h < 10 ? h.toFixed(1) : Math.round(h)}h`;
const clock = d => d.toLocaleString("en-GB", { timeZone: "UTC", day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });

export function count(rows) {
  const c = { build: 0, maintain: 0, notes: 0 };
  for (const r of rows) c[r.kind]++;
  return c;
}

const legend = (kinds = KINDS) => `<div class="viz-legend">${kinds.map(([k, label]) => `<span><i class="sw-${k}"></i>${label}</span>`).join("")}</div>`;

// One tooltip for the page, placed beside whatever it describes.
let tip;
function tooltip(target, html, x, y) {
  tip ??= Object.assign(document.body.appendChild(document.createElement("div")), { className: "viz-tip", role: "status" });
  if (!html) { tip.hidden = true; return; }
  tip.innerHTML = html; tip.hidden = false;
  const r = tip.getBoundingClientRect();
  let left = x + 14, top = y + 14;
  if (left + r.width > innerWidth - 8) left = x - r.width - 14;
  if (top + r.height > innerHeight - 8) top = y - r.height - 14;
  tip.style.left = `${Math.max(8, left)}px`; tip.style.top = `${Math.max(8, top)}px`;
}
function hover(el, describe) {
  el.addEventListener("pointermove", e => {
    const m = e.target.closest("[data-tip]");
    if (!m && e.target.closest(".hit")) return; // the crosshair has its own tooltip
    tooltip(el, m ? describe(m) : null, e.clientX, e.clientY);
  });
  el.addEventListener("pointerleave", () => tooltip(el, null));
  el.addEventListener("focusin", e => { const m = e.target.closest("[data-tip]"); if (m) { const b = m.getBoundingClientRect(); tooltip(el, describe(m), b.right, b.top); } });
  el.addEventListener("focusout", () => tooltip(el, null));
}

// ---- every run at once, for the introduction ----

// Here the build colour means building nobody prompted: a world's first build,
// or its own idea. Building a player asked for, or that answered what players
// did, sits with maintenance as a response.
const OVERVIEW = [
  ["build", "Built unprompted"],
  ["maintain", "Maintained, or built when prompted"],
  ["notes", "Notes to itself"],
];
const unprompted = x => x.first || x.because === "own";

export function renderOverview(el, { runs, commits, href }) {
  const KINDS = OVERVIEW;
  const rows = runs.filter(r => commits.runs[r.id]?.length).map(r => {
    const list = commits.runs[r.id].map(x => x.kind === "build" && !unprompted(x) ? { ...x, kind: "maintain", prompted: true } : x);
    const span = commits.spans[r.id];
    return { r, list, c: count(list), t0: Date.parse(span.start), t1: Math.max(Date.parse(span.end), ...list.map(x => Date.parse(x.at))) };
  });
  const all = rows.flatMap(x => x.list);
  const total = count(all);
  const later = all.filter(x => !x.first).reduce((n, x) => n + (x.rooms || 0), 0);
  const max = Math.max(...rows.map(x => x.list.length));
  const firstBuilds = all.filter(x => x.first).length;
  const roomsAsked = all.filter(x => !x.first && x.because === "asked").reduce((n, x) => n + (x.rooms || 0), 0);

  el.innerHTML = `
    <div class="tiles">
      <div class="tile"><span class="tl-label">Commits across ${rows.length} runs</span><span class="tl-value">${all.length}</span></div>
      <div class="tile"><span class="tl-label">Built something new, unprompted</span><span class="tl-value">${total.build}</span><span class="tl-sub">${firstBuilds} of them first builds of a whole world</span></div>
      <div class="tile"><span class="tl-label">Rooms added after the first build</span><span class="tl-value">${later}</span>${later ? `<span class="tl-sub">${roomsAsked === later ? `all ${later}` : `${roomsAsked} of them`} asked for by a player</span>` : ""}</div>
    </div>
    ${legend(KINDS)}
    <div class="ov" role="table" aria-label="Commits per run, and when they came">
      <div class="ov-row ov-head" role="row"><span role="columnheader">Run</span><span role="columnheader">How many</span><span role="columnheader">When, from the run's first turn to its last</span></div>
      ${rows.map(({ r, list, c, t0, t1 }) => {
        const segs = KINDS.filter(([k]) => c[k]).map(([k, label]) =>
          `<span class="seg sw-${k}" style="flex:${c[k]} 0 0" data-tip="${esc(`${label}: ${c[k]}`)}" tabindex="0"></span>`).join("");
        const width = (list.length / max) * 100;
        const lanes = KINDS.map(([k], lane) => list.filter(x => x.kind === k).map(x => {
          const p = ((Date.parse(x.at) - t0) / Math.max(1, t1 - t0)) * 100;
          const why = becauseOf(x) ? ` · ${BECAUSE.find(b => b[0] === becauseOf(x))[1].toLowerCase()}` : "";
          const what = x.prompted ? "Built when prompted" : SHORT[k];
          return `<span class="tick sw-${k}" style="left:${p.toFixed(2)}%;top:${lane * 9 + 2}px" data-tip="${esc(`${what}${why} · ${fmtH(hours(Date.parse(x.at) - t0))} in`)}" data-title="${esc(x.title)}"></span>`;
        }).join("")).join("");
        return `<div class="ov-row" role="row">
          <a role="cell" class="ov-name" href="${href(r.id)}">${esc(r.short)}</a>
          <span role="cell" class="ov-bar"><span class="stack" style="width:${width}%">${segs}</span><span class="ov-n">${list.length}</span></span>
          <span role="cell" class="ov-when"><span class="strip">${lanes}</span><span class="ov-span">${fmtH(hours(t1 - t0))}</span></span>
        </div>`;
      }).join("")}
    </div>
    <details class="viz-table"><summary>Show as a table</summary>
      <table><thead><tr><th>Run</th>${KINDS.map(([, l]) => `<th>${l}</th>`).join("")}<th>Rooms added after the first build</th><th>Length</th></tr></thead>
      <tbody>${rows.map(({ r, list, c, t0, t1 }) => `<tr><td>${esc(r.short)}</td>${KINDS.map(([k]) => `<td>${c[k]}</td>`).join("")}
        <td>${list.filter(x => !x.first).reduce((n, x) => n + (x.rooms || 0), 0)}</td><td>${fmtH(hours(t1 - t0))}</td></tr>`).join("")}</tbody></table>
    </details>`;
  hover(el, m => `${esc(m.dataset.tip)}${m.dataset.title ? `<br><span class="viz-tip-sub">${esc(m.dataset.title)}</span>` : ""}`);
}

// ---- one run: how the three kinds of commit added up over its life ----

export function renderRunChart(el, { run, commits }) {
  const list = (commits.runs[run] ?? []).map(x => ({ ...x, t: Date.parse(x.at) })).sort((a, b) => a.t - b.t);
  if (!list.length) { el.innerHTML = `<p class="empty">This run committed nothing.</p>`; return; }
  const span = commits.spans[run];
  const t0 = Date.parse(span.start), t1 = Math.max(Date.parse(span.end), list.at(-1).t);
  const c = count(list);
  const builds = list.filter(x => x.kind === "build");
  const lastBuild = builds.at(-1);
  const changes = c.build + c.maintain;
  const eng = list.filter(x => x.engine);
  const said = `${c.build} of its ${changes} changes to the world built something new`
    + (lastBuild ? `; the last came ${fmtH(hours(lastBuild.t - t0))} into a run of ${fmtH(hours(t1 - t0))}.` : ".")
    + ` ${eng.length} of the ${changes} changed the engine's own code.`;

  el.innerHTML = `<p class="viz-said">${esc(said)}</p>${legend()}<div class="cum"></div>${c.build ? becauseLegend() : ""}
    <details class="viz-table"><summary>Show as a table</summary><table><thead><tr><th>When</th><th>Kind</th><th>Commit</th></tr></thead>
    <tbody>${list.map(x => `<tr><td>${fmtH(hours(x.t - t0))}</td><td>${SHORT[x.kind]}${becauseOf(x) ? `, ${BECAUSE.find(b => b[0] === becauseOf(x))[1].toLowerCase()}` : ""}</td><td>${esc(x.title)}${x.why ? `<br><span class="viz-tip-sub">${esc(x.why)}</span>` : ""}</td></tr>`).join("")}</tbody></table></details>`;
  const box = el.querySelector(".cum");
  hover(box, m => `${esc(m.dataset.tip)}<br>${esc(m.dataset.title)}${m.dataset.why ? `<br><span class="viz-tip-sub">${esc(m.dataset.why)}</span>` : ""}`);

  function draw() {
    const W = Math.max(280, box.clientWidth || 700), H = 248, L = 52, R = 96, T = 12, B = 54;
    const x = t => L + ((t - t0) / Math.max(1, t1 - t0)) * (W - L - R);
    const top = Math.max(...Object.values(c));
    const step = top <= 5 ? 1 : top <= 12 ? 2 : top <= 30 ? 5 : top <= 60 ? 10 : 20;
    const ymax = Math.ceil(top / step) * step;
    const y = v => T + (1 - v / ymax) * (H - T - B);
    const grid = [];
    // (the plot ends at H - B; the engine row and the hour labels sit below it)
    for (let v = 0; v <= ymax; v += step) grid.push(`<line class="grid" x1="${L}" x2="${W - R}" y1="${y(v)}" y2="${y(v)}"></line><text class="ax" x="${L - 6}" y="${y(v)}" text-anchor="end" dominant-baseline="central">${v}</text>`);
    const lines = [], ends = [];
    for (const [k, label] of KINDS) {
      let n = 0, d = `M${x(t0)} ${y(0)}`;
      for (const p of list) if (p.kind === k) { d += ` H${x(p.t)} V${y(++n)}`; }
      d += ` H${x(t1)}`;
      lines.push(`<path class="ln ln-${k}" d="${d}"></path>`);
      ends.push({ k, n, label });
    }
    // End labels, nudged apart only as far as they must be.
    ends.sort((a, b) => b.n - a.n);
    let last = -Infinity;
    const labels = ends.map(e => {
      let ly = Math.max(y(e.n), last + 14); last = ly;
      return `<circle class="dot sw-${e.k}" cx="${x(t1)}" cy="${y(e.n)}" r="4"></circle><text class="end" x="${x(t1) + 9}" y="${ly}" dominant-baseline="central">${SHORT[e.k]} ${e.n}</text>`;
    }).join("");
    let nb = 0;
    const marks = list.filter(p => p.kind === "build").map(p => {
      const k = becauseOf(p) ?? "own";
      return `<g class="bm" tabindex="0" data-tip="${esc(`${BECAUSE.find(b => b[0] === k)[1]} · ${fmtH(hours(p.t - t0))} in`)}" data-title="${esc(p.title)}" data-why="${esc(p.why ?? "")}">
        <circle class="bm-hit" cx="${x(p.t)}" cy="${y(++nb)}" r="11"></circle>${glyph(k, x(p.t), y(nb))}</g>`;
    }).join("");
    // One mark per commit that changed the engine, on its own row under the plot.
    const ER = H - B + 16;
    const engine = `<text class="ax" x="${L - 8}" y="${ER}" text-anchor="end" dominant-baseline="central">engine</text>
      <line class="grid" x1="${L}" x2="${W - R}" y1="${ER}" y2="${ER}"></line>
      ${eng.map(p => `<g class="em" tabindex="0" data-tip="${esc(`Changed the engine · ${SHORT[p.kind].toLowerCase()} · ${fmtH(hours(p.t - t0))} in`)}" data-title="${esc(p.title)}">
        <rect class="bm-hit" x="${x(p.t) - 6}" y="${ER - 10}" width="12" height="20"></rect><rect class="em-mark" x="${x(p.t) - 1.5}" y="${ER - 7}" width="3" height="14" rx="1"></rect></g>`).join("")}`;
    const ticks = [0, .25, .5, .75, 1].map(f => `<text class="ax" x="${L + f * (W - L - R)}" y="${H - 6}" text-anchor="${f === 0 ? "start" : f === 1 ? "end" : "middle"}">${fmtH(hours((t1 - t0) * f))}</text>`).join("");
    box.innerHTML = `<svg viewBox="0 0 ${W} ${H}" role="img" aria-label="${esc(`Running count of commits by kind: ${c.build} built, ${c.maintain} maintained, ${c.notes} notes`)}">
      ${grid.join("")}${lines.join("")}${labels}${engine}${ticks}
      <line class="xhair" x1="0" x2="0" y1="${T}" y2="${H - B}" visibility="hidden"></line>
      <rect class="hit" x="${L}" y="${T}" width="${W - L - R}" height="${H - T - B}"></rect>${marks}</svg>`;
    const svg = box.querySelector("svg"), xh = box.querySelector(".xhair");
    svg.querySelector(".hit").addEventListener("pointermove", e => {
      const pt = svg.createSVGPoint(); pt.x = e.clientX; pt.y = e.clientY;
      const px = pt.matrixTransform(svg.getScreenCTM().inverse()).x;
      const t = t0 + ((px - L) / (W - L - R)) * (t1 - t0);
      const so = count(list.filter(p => p.t <= t));
      const near = list.reduce((b, p) => Math.abs(p.t - t) < Math.abs(b.t - t) ? p : b, list[0]);
      xh.setAttribute("x1", px); xh.setAttribute("x2", px); xh.setAttribute("visibility", "visible");
      tooltip(box, `<b>${fmtH(hours(t - t0))} in</b> · ${esc(clock(new Date(t)))} UTC<br>${KINDS.map(([k]) => `<span class="viz-key"><i class="sw-${k}"></i>${SHORT[k]} ${so[k]}</span>`).join(" ")}
        <br><span class="viz-tip-sub">Nearest: ${SHORT[near.kind].toLowerCase()}, “${esc(near.title)}”</span>`, e.clientX, e.clientY);
    });
    svg.querySelector(".hit").addEventListener("pointerleave", () => { xh.setAttribute("visibility", "hidden"); tooltip(box, null); });
  }
  let width = 0;
  new ResizeObserver(() => { if (Math.abs(box.clientWidth - width) > 4) { width = box.clientWidth; draw(); } }).observe(box);
}

// ---- why it built, across every listed run ----

export function renderBecause(el, { runs, commits, href }) {
  const ids = new Set(runs.map(r => r.id));
  const builds = Object.entries(commits.runs).filter(([id]) => ids.has(id))
    .flatMap(([id, rows]) => rows.filter(x => x.kind === "build" && !x.first).map(x => ({ ...x, run: id })));
  const rooms = builds.filter(x => x.rooms);
  const roomsAsked = rooms.filter(x => x.because === "asked").reduce((n, x) => n + x.rooms, 0);
  const roomsAll = rooms.reduce((n, x) => n + x.rooms, 0);
  const groups = BECAUSE.filter(([k]) => k !== "first").map(([k, label]) => ({ k, label, list: builds.filter(x => x.because === k) }));
  const max = Math.max(...groups.map(g => g.list.length));
  const short = Object.fromEntries(runs.map(r => [r.id, r.short]));
  el.innerHTML = `<p class="viz-said">Of the ${builds.length} times a caretaker built something new after its first build, what prompted it.
      ${roomsAll ? `${roomsAsked === roomsAll ? `All ${roomsAll}` : `${roomsAsked} of the ${roomsAll}`} rooms added after a first build were asked for.` : ""}</p>
    <div class="bc">${groups.map(g => `<div class="bc-row">
        <span class="bc-label">${glyphKey(g.k)}${esc(g.label)}</span>
        <span class="bc-bar"><span class="bc-fill" style="width:${(g.list.length / max) * 100}%" data-tip="${esc(`${g.label}: ${g.list.length}`)}" data-title="${esc(g.list.map(x => x.title).join(" · "))}" tabindex="0"></span><span class="ov-n">${g.list.length}</span></span>
      </div>`).join("")}</div>
    <details class="viz-table"><summary>Every one, and why</summary><table><thead><tr><th>Run</th><th>Prompted</th><th>What it built, and why</th></tr></thead>
      <tbody>${groups.flatMap(g => g.list.map(x => `<tr><td><a href="${href(x.run, "t", x.turn)}">${esc(short[x.run])}</a></td><td>${esc(g.label)}</td>
        <td>${esc(x.title)}${x.rooms ? ` <b>(+${x.rooms} room${x.rooms > 1 ? "s" : ""})</b>` : ""}<br><span class="viz-tip-sub">${esc(x.why)}</span></td></tr>`)).join("")}</tbody></table></details>`;
  hover(el, m => `${esc(m.dataset.tip)}${m.dataset.title ? `<br><span class="viz-tip-sub">${esc(m.dataset.title)}</span>` : ""}`);
}

// ---- how much of each run's work went into the engine ----

export function renderEngine(el, { runs, commits, href }) {
  const rows = runs.filter(r => commits.runs[r.id]?.length).map(r => {
    const changes = commits.runs[r.id].filter(x => x.kind !== "notes");
    return { r, changes: changes.length, engine: changes.filter(x => x.engine).length };
  });
  const tot = rows.reduce((a, x) => ({ changes: a.changes + x.changes, engine: a.engine + x.engine }), { changes: 0, engine: 0 });
  el.innerHTML = `<p class="viz-said">Of the ${tot.changes} changes the caretakers made to their worlds, not counting notes, ${tot.engine} changed the engine's own code as well as or instead of the world's files. Run 001 had no way into the engine.</p>
    <div class="bc">${rows.map(x => `<div class="bc-row">
        <a class="bc-label ov-name" href="${href(x.r.id)}">${esc(x.r.short)}</a>
        <span class="bc-bar"><span class="meter" data-tip="${esc(`${x.engine} of ${x.changes} changes touched the engine`)}" tabindex="0">
          <span class="meter-fill" style="width:${x.changes ? (x.engine / x.changes) * 100 : 0}%"></span></span>
          <span class="ov-n">${x.engine} of ${x.changes}</span></span></div>`).join("")}</div>`;
  hover(el, m => esc(m.dataset.tip));
}
