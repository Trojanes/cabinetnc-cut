import { pathToFileURL } from "node:url";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { readFileSync } from "node:fs";

const root = dirname(fileURLToPath(import.meta.url));
const { featuresToOps, attachOpsToNest, applyContourToolOffset, filterOpsEnabled } = await import(
  pathToFileURL(join(root, "..", "src", "ops.js")).href
);
const demo = JSON.parse(
  readFileSync(join(root, "..", "public", "samples", "demo_cut_package.json"), "utf8")
);

const ops = featuresToOps(demo.panels);
const drills = ops.filter((o) => o.op === "drill");
const grooves = ops.filter((o) => o.op === "groove");
const contours = ops.filter((o) => o.op === "contour");
const errors = [];
if (drills.length < 1) errors.push("expected drill ops from demo");
if (grooves.length < 1) errors.push("expected groove ops from demo");
if (contours.length !== demo.panels.length) {
  errors.push(`contour count ${contours.length} != panels ${demo.panels.length}`);
}
if (!drills.every((d) => d.diameterMm > 0 && d.panelId)) errors.push("bad drill fields");
if (!contours.every((c) => (c.path || []).length >= 3)) errors.push("bad contour path");

for (const panel of demo.panels) {
  const ranks = ops
    .filter((o) => o.panelId === panel.panelId)
    .map((o) => ({ contour: 0, drill: 1, groove: 2 }[o.op]));
  for (let i = 1; i < ranks.length; i++) {
    if (ranks[i] < ranks[i - 1]) {
      errors.push(`bad op order on ${panel.panelId}`);
      break;
    }
  }
}

const nested = attachOpsToNest(ops, demo.nestResult);
const nestedDrills = nested.filter((o) => o.op === "drill");
if (!nestedDrills.length) errors.push("no nested drills");
if (!nestedDrills.every((d) => d.placed === true && d.sheetX != null && d.sheetIndex != null)) {
  errors.push("drill missing nest placement fields");
}
const p1 = demo.nestResult.placements.find((p) => p.panelId === "P1");
const d1 = nestedDrills.find((d) => d.panelId === "P1");
if (p1 && d1) {
  const expectX = Math.round((d1.x + p1.offsetX) * 1000) / 1000;
  const expectY = Math.round((d1.y + p1.offsetY) * 1000) / 1000;
  if (d1.sheetX !== expectX || d1.sheetY !== expectY) {
    errors.push(`P1 sheet coords ${d1.sheetX},${d1.sheetY} != ${expectX},${expectY}`);
  }
}

// tool offset injects without importing geom (fn gets -radius for inward)
const mockOff = (pts, delta) => ({
  points: pts.map(([x, y]) => [x + delta, y + delta]),
  engine: "mock",
});
const offsetOps = applyContourToolOffset(nested, 3, mockOff);
const oc = offsetOps.find((o) => o.op === "contour" && o.placed);
if (!oc || oc.toolOffsetMm !== 3 || oc.offsetEngine !== "mock") {
  errors.push("applyContourToolOffset missing meta");
}
if (oc && oc.path[0][0] !== nested.find((o) => o.op === "contour").path[0][0] - 3) {
  errors.push("applyContourToolOffset path not inward");
}
const noOp = applyContourToolOffset(nested, 0, mockOff);
if (noOp.find((o) => o.op === "contour")?.toolOffsetMm) {
  errors.push("radius 0 should skip offset");
}

const drillOnly = filterOpsEnabled(ops, {
  enableContour: false,
  enableDrill: true,
  enableGroove: false,
});
if (drillOnly.some((o) => o.op !== "drill")) errors.push("filterOpsEnabled drill-only");
if (!drillOnly.length) errors.push("filterOpsEnabled empty");

if (errors.length) {
  console.error("FAIL", errors);
  process.exit(1);
}
console.log(
  "OK ops",
  `contour=${contours.length}`,
  `drill=${drills.length}`,
  `groove=${grooves.length}`,
  `nestedDrills=${nestedDrills.length}`,
  "toolOffset=ok"
);
