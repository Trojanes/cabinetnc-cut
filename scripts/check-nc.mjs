import { pathToFileURL } from "node:url";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { readFileSync } from "node:fs";

const root = dirname(fileURLToPath(import.meta.url));
const { featuresToOps, attachOpsToNest, applyContourToolOffset, filterOpsEnabled } = await import(
  pathToFileURL(join(root, "..", "src", "ops.js")).href
);
const { opsToNc, drillsToSimpleNc, contourPassDepths } = await import(
  pathToFileURL(join(root, "..", "src", "nc.js")).href
);
const { getMachineProfile, toolRadiusMm } = await import(
  pathToFileURL(join(root, "..", "src", "machine.js")).href
);
const { offsetPolygon } = await import(
  pathToFileURL(join(root, "..", "src", "geom", "native_offset_node.js")).href
);
const demo = JSON.parse(
  readFileSync(join(root, "..", "public", "samples", "demo_cut_package.json"), "utf8")
);

const errors = [];
function assert(cond, msg) {
  if (!cond) errors.push(msg);
}

const profile = getMachineProfile("nesting_router_6");
let ops = attachOpsToNest(featuresToOps(demo.panels), demo.nestResult);
const contours = ops.filter((o) => o.op === "contour" && o.placed);
assert(contours.length >= 1, "placed contours");
assert(Array.isArray(contours[0].path) && contours[0].path.length >= 3, "contour sheet path");

const radius = toolRadiusMm(profile);
ops = applyContourToolOffset(ops, radius, (pts, r) => offsetPolygon(pts, r));

const nc = opsToNc(ops, profile);
assert(nc.includes("G21"), "G21");
assert(nc.includes("(wcs:"), "wcs comment");
assert(nc.includes("M2"), "M2");
assert(nc.includes("(sheet 1)"), "sheet 1");
assert(nc.includes("S18000") || nc.includes("M3"), "spindle");
assert(nc.includes("(contour"), "contour op");
assert(nc.includes("drill P1"), "drill P1");
assert(nc.includes("(groove P1"), "groove P1");
const goMoves = (nc.match(/^G0 X/gm) || []).length;
assert(goMoves >= 3, `xy moves >=3 got ${goMoves}`);

const fanuc = opsToNc(ops, getMachineProfile("fanuc_like_m30"));
assert(fanuc.includes("G17"), "fanuc G17");
assert(fanuc.includes("M30"), "fanuc M30");
assert(!fanuc.trimEnd().endsWith("M2"), "fanuc not M2");

const legacy = drillsToSimpleNc(
  ops.filter((o) => o.op === "drill" || o.op === "groove")
);
assert(legacy.includes("G21"), "legacy G21");

const drillOnlyOps = filterOpsEnabled(ops, getMachineProfile("drill_only_stub"));
const drillNc = opsToNc(drillOnlyOps, getMachineProfile("drill_only_stub"));
assert(drillNc.includes("drill"), "drill-only has drill");
assert(!drillNc.includes("(contour"), "drill-only no contour");
assert(!drillNc.includes("(groove"), "drill-only no groove");

assert(JSON.stringify(contourPassDepths(18, 0)) === "[18]", "no stepdown");
assert(JSON.stringify(contourPassDepths(18, 6)) === "[6,12,18]", "3 passes");
const stepped = opsToNc(ops, { ...profile, contourDepthMm: 18, contourStepdownMm: 6 });
assert(stepped.includes("passes=3"), "pass comment");
assert(stepped.includes("(pass 1/3"), "pass 1");
assert(stepped.includes("Z-18"), "final depth");

const peckNc = opsToNc(
  ops.filter((o) => o.op === "drill" && o.placed),
  { ...getMachineProfile("generic_cnc_mm"), drillPeckMm: 5 }
);
assert(peckNc.includes("peck=5"), "peck comment");
assert((peckNc.match(/^G0 Z/gm) || []).length >= 2, "peck retracts");

if (errors.length) {
  console.error("FAIL", errors);
  process.exit(1);
}
console.log(
  "OK nc",
  `bytes=${nc.length}`,
  `xyMoves=${goMoves}`,
  `profile=${profile.id}`,
  "contour+groove+fanuc=ok"
);
