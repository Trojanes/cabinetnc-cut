/** End-to-end smoke: demo → pack(if needed) → ops → nest attach → nc */
import { readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const root = dirname(fileURLToPath(import.meta.url));
const src = (name) => pathToFileURL(join(root, "..", "src", name)).href;

const { packPanels } = await import(src("pack.js"));
const { featuresToOps, attachOpsToNest } = await import(src("ops.js"));
const { opsToNc } = await import(src("nc.js"));
const { getMachineProfile } = await import(src("machine.js"));
const { nestToDxf } = await import(src("dxf.js"));
const { buildProjectDoc, parseProjectDoc } = await import(src("project.js"));

const demo = JSON.parse(
  readFileSync(join(root, "..", "public", "samples", "demo_cut_package.json"), "utf8")
);

const pkg = structuredClone(demo);
if (!pkg.nestResult?.placements?.length) {
  pkg.nestResult = packPanels(
    pkg.panels,
    pkg.sheets?.[0],
    pkg.nestSettings?.spacingMm,
    pkg.nestSettings?.borderMm,
    { allowRotation: Boolean(pkg.nestSettings?.allowRotation) }
  );
}

const ops = attachOpsToNest(featuresToOps(pkg.panels), pkg.nestResult);
const nc = opsToNc(ops, getMachineProfile("generic_cnc_mm"));
const drills = ops.filter((o) => o.op === "drill");
const grooves = ops.filter((o) => o.op === "groove");
const contours = ops.filter((o) => o.op === "contour");
const dxf = nestToDxf(pkg, 0);
const proj = parseProjectDoc(buildProjectDoc(pkg, { machineId: "generic_cnc_mm" }));

// lock first placed panel and repack — coords must stick
const first = pkg.nestResult.placements[0];
first.locked = true;
first.offsetX = 80;
first.offsetY = 90;
const lockedId = first.panelId;
const again = packPanels(
  pkg.panels,
  pkg.sheets?.[0],
  pkg.nestSettings?.spacingMm,
  pkg.nestSettings?.borderMm,
  {
    allowRotation: Boolean(pkg.nestSettings?.allowRotation),
    lockedPlacements: [first],
  }
);
const kept = again.placements.find((p) => String(p.panelId) === String(lockedId));

const errors = [];
if (!pkg.panels.length) errors.push("no panels");
if (!ops.length) errors.push("no ops");
if (!drills.every((d) => d.placed)) errors.push("drill not placed");
if (!nc.includes("M2")) errors.push("nc incomplete");
if (!nc.includes("(wcs:")) errors.push("nc missing wcs");
if (!nc.includes("(contour")) errors.push("nc missing contour");
if (!dxf.includes("LWPOLYLINE")) errors.push("dxf missing");
if (!dxf.includes("CIRCLE")) errors.push("dxf holes");
if (!proj.ok) errors.push("project roundtrip");
if (!kept?.locked || Math.abs(kept.offsetX - 80) > 1e-6) errors.push("lock not preserved");

if (errors.length) {
  console.error("FAIL smoke", errors);
  process.exit(1);
}

console.log(
  "OK smoke",
  `panels=${pkg.panels.length}`,
  `placed=${pkg.nestResult.placements.length}`,
  `engine=${pkg.nestResult.engine}`,
  `util=${pkg.nestResult.stats?.utilizationPct?.toFixed?.(1) ?? "?"}%`,
  `contour=${contours.length}`,
  `drill=${drills.length}`,
  `groove=${grooves.length}`,
  `ncBytes=${nc.length}`,
  `dxfBytes=${dxf.length}`,
  `lock=${lockedId}@80,90`
);
