import { pathToFileURL } from "node:url";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

// render.js uses canvas DOM — only test pure helpers via geom + a tiny poly hit re-export check
const root = dirname(fileURLToPath(import.meta.url));
const geom = await import(pathToFileURL(join(root, "..", "src", "geom", "index.js")).href);

const errors = [];
function assert(cond, msg) {
  if (!cond) errors.push(msg);
}

const pts = [
  [10, 10],
  [110, 10],
  [110, 60],
  [10, 60],
];
assert(geom.pointInPolygon(50, 30, pts), "inside");
assert(!geom.pointInPolygon(0, 0, pts), "outside");
assert(Math.abs(geom.bbox(pts).width - 100) < 1e-9, "bbox w");

if (errors.length) {
  console.error("FAIL hit", errors);
  process.exit(1);
}
console.log("OK hit helpers");
