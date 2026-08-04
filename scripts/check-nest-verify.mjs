/** Nest verify (poly + clipper gap). */
import { pathToFileURL } from "node:url";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(fileURLToPath(import.meta.url));
const { verifyNestPoly, verifyNestGapAsync } = await import(
  pathToFileURL(join(root, "..", "src", "nest_verify.js")).href
);
const { offsetPolygon } = await import(
  pathToFileURL(join(root, "..", "src", "geom", "native_offset_node.js")).href
);

const errors = [];
function assert(cond, msg) {
  if (!cond) errors.push(msg);
}

const L1 = {
  panelId: "L1",
  bbox: { widthMm: 100, heightMm: 100 },
  outline: {
    points: [
      [0, 0],
      [100, 0],
      [100, 40],
      [40, 40],
      [40, 100],
      [0, 100],
    ],
  },
};
const rect = {
  panelId: "R1",
  bbox: { widthMm: 40, heightMm: 40 },
  outline: {
    points: [
      [0, 0],
      [40, 0],
      [40, 40],
      [0, 40],
    ],
  },
};
const placeOk = [
  { panelId: "L1", sheetIndex: 0, offsetX: 0, offsetY: 0, rotationDeg: 0 },
  { panelId: "R1", sheetIndex: 0, offsetX: 50, offsetY: 50, rotationDeg: 0 },
];
const v = verifyNestPoly([L1, rect], placeOk, 0);
assert(v.ok, "notch nest poly ok");

const placeBad = [
  { panelId: "L1", sheetIndex: 0, offsetX: 0, offsetY: 0, rotationDeg: 0 },
  { panelId: "R1", sheetIndex: 0, offsetX: 10, offsetY: 10, rotationDeg: 0 },
];
const bad = verifyNestPoly([L1, rect], placeBad, 0);
assert(!bad.ok, "overlap detected");

const gap = await verifyNestGapAsync([L1, rect], placeOk, 12, (pts, d) => offsetPolygon(pts, d));
assert(gap.engine.includes("clipper") || gap.engine.includes("poly") || gap.engine.includes("gap"), "gap engine");
assert(typeof gap.ok === "boolean", "gap ok bool");

if (errors.length) {
  console.error("FAIL nest_verify", errors);
  process.exit(1);
}
console.log("OK nest_verify", `polyHits=${bad.hitCount}`, `gapEngine=${gap.engine}`);
