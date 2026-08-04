/** Project open/save round-trip. */
import { pathToFileURL } from "node:url";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { readFileSync } from "node:fs";

const root = dirname(fileURLToPath(import.meta.url));
const { buildProjectDoc, parseProjectDoc } = await import(
  pathToFileURL(join(root, "..", "src", "project.js")).href
);
const demo = JSON.parse(
  readFileSync(join(root, "..", "public", "samples", "demo_cut_package.json"), "utf8")
);

const errors = [];
function assert(cond, msg) {
  if (!cond) errors.push(msg);
}

const doc = buildProjectDoc(demo, {
  machineId: "fanuc_like_m30",
  allowOverlap: true,
  showToolpath: true,
  toolpathDeltaMm: 8,
  viewMode: "nest",
  selectedPanelId: "P1",
  sheetIndex: 0,
  machineOverrides: { fanuc_like_m30: { safeZMm: 12, originNote: "G54" } },
});
assert(doc.schema === "cabinetnc.cut-project", "schema");
const round = parseProjectDoc(JSON.parse(JSON.stringify(doc)));
assert(round.ok, "parse ok");
assert(round.session.machineId === "fanuc_like_m30", "machine");
assert(round.session.machineOverrides?.fanuc_like_m30?.safeZMm === 12, "overrides");
assert(round.package?.panels?.length === demo.panels.length, "panels");

const plain = parseProjectDoc(demo);
assert(plain.ok && plain.package?.panels?.length, "plain cut-package");

if (errors.length) {
  console.error("FAIL", errors);
  process.exit(1);
}
console.log("OK project");
