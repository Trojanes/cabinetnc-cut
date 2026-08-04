/** CAM sim + NC preflight checks. */
import { pathToFileURL } from "node:url";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(fileURLToPath(import.meta.url));
const { simOpsList, clampSimIndex, simStep, describeSimOp, expandSimFrames, describeSimFrame } = await import(
  pathToFileURL(join(root, "..", "src", "cam_sim.js")).href
);
const { preflightNc, formatPreflight } = await import(
  pathToFileURL(join(root, "..", "src", "nc_preflight.js")).href
);

const errors = [];
function assert(cond, msg) {
  if (!cond) errors.push(msg);
}

const ops = [
  { op: "contour", panelId: "P1", placed: true, path: [[0, 0], [10, 0], [10, 10]] },
  { op: "drill", panelId: "P1", placed: false, sheetX: 1, sheetY: 1 },
  { op: "drill", panelId: "P2", placed: true, sheetX: 5, sheetY: 5, diameterMm: 8 },
];
const list = simOpsList(ops);
assert(list.length === 2, "simOpsList placed only");
assert(clampSimIndex(-1, 2) === 1, "clamp wrap");
assert(simStep(0, 2, 1) === 1, "step +1");
assert(describeSimOp(list[0], 0, 2).startsWith("1/2"), "describe");

const frames = expandSimFrames(ops);
assert(frames.length >= 4, "point frames");
assert(frames.some((f) => f.kind === "drill"), "drill frame");
assert(describeSimFrame(frames[0], 0, frames.length).includes("pt"), "frame desc");

const empty = preflightNc([], { feedXyMmMin: 1000, spindleRpm: 18000 });
assert(!empty.ok && empty.issues.some((i) => i.code === "no_ops"), "empty ops");

const good = preflightNc(list, { feedXyMmMin: 3000, spindleRpm: 18000, toolDiameterMm: 6 }, {
  widthMm: 100,
  lengthMm: 100,
});
assert(good.ok, "in-bounds ok");

const oob = preflightNc(
  [{ op: "drill", placed: true, sheetX: 500, sheetY: 5, diameterMm: 5 }],
  { feedXyMmMin: 1000, spindleRpm: 1 },
  { widthMm: 100, lengthMm: 100 }
);
assert(!oob.ok && oob.issues.some((i) => i.code === "out_of_sheet"), "oob");
assert(formatPreflight(oob).includes("✗"), "format");

if (errors.length) {
  console.error("FAIL cam/preflight", errors);
  process.exit(1);
}
console.log("OK cam_sim+preflight", `ops=${list.length}`);
