/** Recent projects helper (node stub — no localStorage). */
import { pathToFileURL } from "node:url";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(fileURLToPath(import.meta.url));

// Minimal in-memory stand-in for the module's pure helpers shape via re-export test of push logic.
const store = { data: "[]" };
globalThis.localStorage = {
  getItem: (k) => (k ? store.data : null),
  setItem: (_k, v) => {
    store.data = String(v);
  },
  removeItem: () => {
    store.data = "[]";
  },
};

const { listRecent, pushRecent, getRecent, clearRecent } = await import(
  pathToFileURL(join(root, "..", "src", "recent.js")).href
);

const errors = [];
function assert(cond, msg) {
  if (!cond) errors.push(msg);
}

clearRecent();
assert(listRecent().length === 0, "empty");
const doc = { schema: "cabinetnc.cut-project", package: { panels: [] } };
const a = pushRecent("demo", doc);
assert(a.length === 1 && a[0].label === "demo", "push");
const again = pushRecent("demo", { ...doc, savedAt: "x" });
assert(again.length === 1, "dedupe by label");
pushRecent("other", doc);
assert(listRecent().length === 2, "two");
const got = getRecent(listRecent()[0].id);
assert(got && got.doc, "get");
clearRecent();
assert(listRecent().length === 0, "clear");

if (errors.length) {
  console.error("FAIL recent", errors);
  process.exit(1);
}
console.log("OK recent");
