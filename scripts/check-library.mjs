/** Library helpers (M2). */
import { pathToFileURL } from "node:url";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(fileURLToPath(import.meta.url));
const {
  defaultLibrary,
  upsertMaterial,
  upsertTool,
  librarySummary,
  toolsFromMachines,
} = await import(pathToFileURL(join(root, "..", "src", "library.js")).href);
const { getMachineProfile, withProfileOverrides, toolRadiusMm } = await import(
  pathToFileURL(join(root, "..", "src", "machine.js")).href
);

const errors = [];
function assert(cond, msg) {
  if (!cond) errors.push(msg);
}

const lib = defaultLibrary();
assert(lib.materials.length >= 3, "default materials");
assert(lib.tools.length >= 1, "default tools from machines");
assert(toolsFromMachines().length === lib.tools.length, "tools sync");

const next = upsertMaterial(lib, { name: "bamboo", thicknessMm: 12 });
assert(next.materials.some((m) => m.name === "bamboo"), "upsert material");
const withTool = upsertTool(next, {
  name: "custom Ø8",
  diameterMm: 8,
  feedXyMmMin: 3500,
});
assert(withTool.tools.some((t) => t.name === "custom Ø8"), "upsert tool");

const sum = librarySummary(withTool);
assert(sum.materialCount >= 4 && sum.toolCount >= 2, "summary counts");

const base = getMachineProfile("generic_cnc_mm");
const merged = withProfileOverrides(base, { toolDiameterMm: 9, feedXyMmMin: 1111 });
assert(merged.toolDiameterMm === 9 && merged.feedXyMmMin === 1111, "profile override");
assert(toolRadiusMm(merged) === 4.5, "override radius");
assert(base.toolDiameterMm !== 9, "base unchanged");

const { opsToNc } = await import(pathToFileURL(join(root, "..", "src", "nc.js")).href);
const ncNote = opsToNc([], withProfileOverrides(base, { originNote: "G54 fixture", programEnd: "M30", dialect: "fanuc_like" }));
assert(ncNote.includes("(origin: G54 fixture)"), "origin note in nc");
assert(ncNote.includes("M30"), "M30 end");
assert(ncNote.includes("G17"), "fanuc preamble");

if (errors.length) {
  console.error("FAIL", errors);
  process.exit(1);
}
console.log("OK library", `mats=${sum.materialCount}`, `tools=${sum.toolCount}`, "override=ok", "post=ok");
