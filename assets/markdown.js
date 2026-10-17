// Just enough Markdown for what the caretaker wrote: its journal, its own files,
// and what it said in its turns. Everything is escaped first, so nothing it
// wrote can become markup on this page.

export const esc = s => String(s ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

function inline(s) {
  return esc(s)
    .replace(/`([^`]+)`/g, "<code>$1</code>")
    .replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>")
    .replace(/(^|[^*\w])\*([^*\n]+)\*(?!\w)/g, "$1<em>$2</em>")
    .replace(/\[([^\]]+)\]\((https?:[^)\s]+)\)/g, '<a href="$2">$1</a>');
}

export function markdown(src) {
  const out = []; let para = [], list = null, quote = [];
  const flush = () => {
    if (para.length) { out.push(`<p>${inline(para.join(" "))}</p>`); para = []; }
    if (list) { out.push(`<ul>${list.map(i => `<li>${inline(i)}</li>`).join("")}</ul>`); list = null; }
    if (quote.length) { out.push(`<blockquote>${markdown(quote.join("\n"))}</blockquote>`); quote = []; }
  };
  let fence = null;
  for (const line of src.split("\n")) {
    if (fence !== null) { if (/^```/.test(line)) { out.push(`<pre>${esc(fence.join("\n"))}</pre>`); fence = null; } else fence.push(line); continue; }
    if (/^```/.test(line)) { flush(); fence = []; continue; }
    const h = /^(#{1,3})\s+(.*)$/.exec(line);
    if (h) { flush(); out.push(`<h${h[1].length}>${inline(h[2])}</h${h[1].length}>`); continue; }
    if (/^\s*(---|\*\*\*)\s*$/.test(line)) { flush(); out.push("<hr>"); continue; }
    if (/^>\s?/.test(line)) { if (para.length || list) { const q = quote; quote = []; flush(); quote = q; } quote.push(line.replace(/^>\s?/, "")); continue; }
    const li = /^\s*[-*]\s+(.*)$/.exec(line) || /^\s*\d+\.\s+(.*)$/.exec(line);
    if (li) { if (para.length || quote.length) flush(); (list ??= []).push(li[1]); continue; }
    if (!line.trim()) { flush(); continue; }
    if (list && /^\s+/.test(line)) { list[list.length - 1] += " " + line.trim(); continue; }
    if (quote.length) flush();
    para.push(line.trim());
  }
  if (fence) out.push(`<pre>${esc(fence.join("\n"))}</pre>`);
  flush();
  return out.join("\n");
}
