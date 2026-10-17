// A map of a world, drawn from its world.json: every room placed by the
// compass directions of its exits, starting from where people arrive.

import { esc } from "./markdown.js";

const COMPASS = {
  north: [0, -1], south: [0, 1], east: [1, 0], west: [-1, 0],
  northeast: [1, -1], northwest: [-1, -1], southeast: [1, 1], southwest: [-1, 1],
  ne: [1, -1], nw: [-1, -1], se: [1, 1], sw: [-1, 1], n: [0, -1], s: [0, 1], e: [1, 0], w: [-1, 0],
};
const CW = 176, CH = 84, BW = 150, BH = 40;

export function renderMap(el, world) {
  const rooms = new Map(world.locations.map(l => [String(l.index), l]));
  // Exits both ways, so a room reached only by a one-way exit is still placed.
  const links = new Map([...rooms.keys()].map(k => [k, []]));
  const edges = new Set();
  for (const [k, room] of rooms) {
    for (const [dir, to] of Object.entries(room.connections ?? {})) {
      const t = String(to);
      if (!rooms.has(t)) continue;
      const v = COMPASS[dir.toLowerCase()];
      links.get(k).push({ to: t, v, dir });
      links.get(t).push({ to: k, v: v && [-v[0], -v[1]], dir: null });
      edges.add([k, t].sort().join("|") + (v ? "" : "|odd"));
    }
  }

  const at = new Map(), taken = new Set();
  const key = (x, y) => `${x},${y}`;
  const place = (k, x, y) => {
    if (taken.has(key(x, y))) [x, y] = nearestFree(x, y);
    at.set(k, [x, y]); taken.add(key(x, y));
  };
  function nearestFree(x, y) {
    for (let r = 1; r < 12; r++)
      for (let dx = -r; dx <= r; dx++) for (let dy = -r; dy <= r; dy++)
        if (Math.max(Math.abs(dx), Math.abs(dy)) === r && !taken.has(key(x + dx, y + dy))) return [x + dx, y + dy];
    return [x, y];
  }
  const start = rooms.has(String(world.spawnLocation)) ? String(world.spawnLocation) : [...rooms.keys()][0];
  const order = [start, ...[...rooms.keys()].filter(k => k !== start)];
  let row = 0;
  for (const root of order) {
    if (at.has(root)) continue;
    place(root, 0, row);
    const queue = [root];
    while (queue.length) {
      const k = queue.shift();
      const [x, y] = at.get(k);
      // Compass exits first, so they get the cells they point at.
      for (const l of [...links.get(k)].sort((a, b) => !!b.v - !!a.v)) {
        if (at.has(l.to)) continue;
        const [dx, dy] = l.v ?? [1, 1];
        place(l.to, x + dx, y + dy);
        queue.push(l.to);
      }
    }
    row = Math.max(...[...at.values()].map(p => p[1])) + 2;
  }

  const xs = [...at.values()].map(p => p[0]), ys = [...at.values()].map(p => p[1]);
  const minX = Math.min(...xs), minY = Math.min(...ys);
  const W = (Math.max(...xs) - minX + 1) * CW, H = (Math.max(...ys) - minY + 1) * CH + 14;
  const centre = k => { const [x, y] = at.get(k); return [(x - minX) * CW + CW / 2, (y - minY) * CH + CH / 2]; };

  const lines = [...edges].map(e => {
    const [a, b, odd] = e.split("|");
    const [x1, y1] = centre(a), [x2, y2] = centre(b);
    return `<line class="edge" x1="${x1}" y1="${y1}" x2="${x2}" y2="${y2}"${odd ? ' stroke-dasharray="1 5"' : ""}></line>`;
  }).join("");
  const boxes = [...rooms.keys()].map(k => {
    const [cx, cy] = centre(k);
    const name = rooms.get(k).name || `room ${k}`;
    const words = wrap(name, 19);
    const text = words.length === 1
      ? `<text x="${cx}" y="${cy}">${esc(words[0])}</text>`
      : `<text x="${cx}" y="${cy - 8}">${esc(words[0])}</text><text x="${cx}" y="${cy + 8}">${esc(words[1])}</text>`;
    return `<g class="room${k === start ? " spawn" : ""}" data-room="${esc(k)}" tabindex="0" role="button" aria-label="${esc(name)}">
      <rect x="${cx - BW / 2}" y="${cy - BH / 2}" width="${BW}" height="${BH}"></rect>${text}
      ${k === start ? `<text class="mark" x="${cx}" y="${cy + BH / 2 + 11}">you arrive here</text>` : ""}</g>`;
  }).join("");

  el.innerHTML = `<div class="map-head"><div><span class="eyebrow">The world as it was left · ${rooms.size} rooms</span><span class="hint">Choose a room to read it</span></div></div>
    <svg viewBox="0 0 ${W} ${H}" style="max-width:${W}px;margin:0 auto" role="group" aria-label="Map">${lines}${boxes}</svg>
    <div class="room-desc" id="room-desc"></div>`;

  const desc = el.querySelector("#room-desc");
  const show = k => {
    const room = rooms.get(k);
    for (const g of el.querySelectorAll(".room")) g.classList.toggle("on", g.dataset.room === k);
    const exits = Object.entries(room.connections ?? {}).map(([d, t]) => `${d}: ${rooms.get(String(t))?.name ?? t}`);
    desc.innerHTML = `<h3>${esc(room.name)}</h3><p>${esc(room.description || "No description.")}</p>
      ${exits.length ? `<div class="exits">${esc(exits.join("  ·  "))}</div>` : `<div class="exits">no way out</div>`}`;
  };
  for (const g of el.querySelectorAll(".room")) {
    g.addEventListener("click", () => show(g.dataset.room));
    g.addEventListener("keydown", e => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); show(g.dataset.room); } });
  }
  show(start);
}

function wrap(name, width) {
  if (name.length <= width) return [name];
  const words = name.split(" ");
  let first = "";
  while (words.length && (first + " " + words[0]).trim().length <= width) first = (first + " " + words.shift()).trim();
  const rest = words.join(" ");
  return [first || name.slice(0, width), rest.length > width ? rest.slice(0, width - 1) + "…" : rest];
}
