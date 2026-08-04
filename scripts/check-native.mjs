import { pathToFileURL } from "node:url";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(fileURLToPath(import.meta.url));
const geom = await import(pathToFileURL(join(root, "..", "src", "geom", "index.js")).href);
const native = await import(
  pathToFileURL(join(root, "..", "src", "geom", "native_offset_node.js")).href
);

const errors = [];
function assert(cond, msg) {
  if (!cond) errors.push(msg);
}

const rect = geom.rectOutline(100, 50);
const js = native.offsetPolygon(rect, 5, { cliPath: null });
assert(js.engine === "js", "force js engine");
assert(Math.abs(geom.bbox(js.points).width - 110) < 1e-6, "js offset w");
assert(Math.abs(geom.bbox(js.points).height - 60) < 1e-6, "js offset h");

const auto = native.offsetPolygon(rect, 5);
assert(auto.points.length >= 4, "auto offset pts");
assert(Math.abs(geom.bbox(auto.points).width - 110) < 1e-6, "auto offset w");
assert(auto.engine === "js" || auto.engine === "cabinetnc_core", "engine name");

const cli = native.resolveOffsetCli();
if (cli) {
  const n = native.offsetPolygon(rect, 5, { cliPath: cli });
  assert(n.engine === "cabinetnc_core", "native engine");
  assert(n.mode === "clipper_offset" || n.mode === "offset_rect", "native mode");
  assert(Math.abs(geom.bbox(n.points).width - 110) < 0.5, "native w ~110");
  assert(Math.abs(geom.bbox(n.points).height - 60) < 0.5, "native h ~60");
  // L-shape: Clipper must grow area (not just AABB rect hack)
  const ell = [
    [0, 0],
    [100, 0],
    [100, 40],
    [40, 40],
    [40, 100],
    [0, 100],
  ];
  const ellOff = native.offsetPolygon(ell, 8, { cliPath: cli });
  assert(ellOff.points.length >= 4, "L offset pts");
  assert(geom.area(ellOff.points) > geom.area(ell), "L offset area grows");

  // difference: 100x50 rect minus 20mm hole → area shrinks
  const hole = [];
  for (let i = 0; i < 16; i++) {
    const a = (i / 16) * Math.PI * 2;
    hole.push([50 + Math.cos(a) * 10, 25 + Math.sin(a) * 10]);
  }
  const diff = native.differencePolygon(rect, [hole], { cliPath: cli });
  assert(diff.engine === "cabinetnc_core", "diff engine");
  assert(diff.mode === "clipper_difference", "diff mode");
  assert(diff.points.length >= 4, "diff pts");
  assert((diff.polygons || []).length >= 2, "diff outer+hole paths");
  const holeArea = geom.area(diff.polygons[1]);
  assert(holeArea > 200 && holeArea < 400, `hole area ~314 got ${holeArea}`);
  console.log("native cli:", cli, "mode:", n.mode, "diff:", diff.mode);
} else {
  console.log("native cli: (not built — JS fallback only)");
}

if (errors.length) {
  console.error("check-native FAIL");
  for (const e of errors) console.error(" -", e);
  process.exit(1);
}
console.log("check-native OK", { engine: auto.engine, mode: auto.mode });
