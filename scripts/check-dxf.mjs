/** DXF nest export checks (no geom / native). */
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const root = dirname(fileURLToPath(import.meta.url));
const { nestToDxf } = await import(pathToFileURL(join(root, "..", "src", "dxf.js")).href);

const demo = JSON.parse(
  readFileSync(join(root, "..", "public", "samples", "demo_cut_package.json"), "utf8")
);

const errors = [];
function assert(cond, msg) {
  if (!cond) errors.push(msg);
}

const dxf = nestToDxf(demo, 0);
assert(dxf.includes("LWPOLYLINE"), "panel polyline");
assert(dxf.includes("SHEET"), "sheet layer");
assert(dxf.includes("PANEL"), "panel layer");
assert(dxf.includes("CIRCLE"), "hole circles");
assert(dxf.includes("HOLE"), "hole layer");
assert(dxf.includes("LINE"), "groove lines");
assert(dxf.includes("GROOVE"), "groove layer");
assert(dxf.trimEnd().endsWith("EOF"), "eof");

const bare = nestToDxf(demo, 0, { includeFeatures: false });
assert(!bare.includes("CIRCLE"), "features off → no circle");
assert(bare.includes("PANEL"), "features off still has panels");

// empty sheet index still writes sheet frame
const emptySheet = nestToDxf(demo, 99);
assert(emptySheet.includes("SHEET"), "empty sheet still has frame");
assert(!emptySheet.includes("CIRCLE"), "no panels on sheet 99");

if (errors.length) {
  console.error("FAIL dxf", errors);
  process.exit(1);
}
console.log("OK dxf", `bytes=${dxf.length}`, "holes+grooves=ok");
