/** Recent project snapshots in localStorage (MakerHub-depth M6 local feel).
 * ponytail: full JSON blob capped — upgrade to filesystem handles / Tauri paths.
 */

const KEY = "cabinetnc.cut.recent";
const MAX = 5;
const MAX_BYTES = 1_500_000;

export function listRecent() {
  try {
    const raw = localStorage.getItem(KEY);
    const arr = raw ? JSON.parse(raw) : [];
    return Array.isArray(arr) ? arr : [];
  } catch {
    return [];
  }
}

function writeAll(list) {
  localStorage.setItem(KEY, JSON.stringify(list.slice(0, MAX)));
}

export function pushRecent(label, doc) {
  if (!doc || typeof doc !== "object") return listRecent();
  const json = JSON.stringify(doc);
  if (json.length > MAX_BYTES) return listRecent(); // skip oversized
  const id = `${Date.now()}_${Math.random().toString(36).slice(2, 8)}`;
  const entry = {
    id,
    label: String(label || "project").slice(0, 80),
    savedAt: new Date().toISOString(),
    doc,
  };
  const next = [entry, ...listRecent().filter((e) => e.label !== entry.label)].slice(0, MAX);
  writeAll(next);
  return next;
}

export function getRecent(id) {
  return listRecent().find((e) => e.id === String(id)) || null;
}

export function clearRecent() {
  localStorage.removeItem(KEY);
  return [];
}
