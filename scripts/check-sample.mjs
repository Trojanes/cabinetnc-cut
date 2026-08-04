import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";
import { pathToFileURL } from "node:url";

const SCHEMA = "cabinetnc.cut-package";
const root = dirname(fileURLToPath(import.meta.url));
const samplePath = join(root, "..", "public", "samples", "demo_cut_package.json");
const raw = JSON.parse(readFileSync(samplePath, "utf8"));

const packMod = await import(pathToFileURL(join(root, "..", "src", "pack.js")).href);
const { shelfPack } = packMod;

const errors = [];
if (raw.schema !== SCHEMA) errors.push("bad schema");
if (!Array.isArray(raw.panels) || raw.panels.length < 1) errors.push("no panels");
for (const p of raw.panels) {
  if (!p.outline?.points || p.outline.points.length < 3) errors.push(`bad outline ${p.panelId}`);
}
if (!raw.nestResult?.placements?.length) errors.push("demo missing nest placements");

const packed = shelfPack(raw.panels, raw.sheets[0], 12, 15, {
  allowRotation: Boolean(raw.nestSettings?.allowRotation),
});
if (packed.placements.length !== raw.panels.length) {
  errors.push("shelf pack lost panels on demo sizes");
}
if (packed.sheetCount < 1) errors.push("sheetCount expected >= 1");

// Force multi-sheet: short sheet so demo panels spill across sheets
const multi = shelfPack(raw.panels, { widthMm: 1220, lengthMm: 500 }, 12, 15);
if (multi.sheetCount < 2) errors.push(`expected multi-sheet, got ${multi.sheetCount}`);
if (multi.unplacedCount !== 0) errors.push(`unexpected unplaced ${multi.unplacedCount}`);
const maxIdx = Math.max(...multi.placements.map((p) => p.sheetIndex));
if (maxIdx + 1 !== multi.sheetCount) errors.push("sheetCount mismatch max sheetIndex");

if (errors.length) {
  console.error("FAIL", errors);
  process.exit(1);
}
console.log(
  "OK",
  `panels=${raw.panels.length}`,
  `placements=${raw.nestResult.placements.length}`,
  `shelfSheets=${packed.sheetCount}`,
  `multiSheets=${multi.sheetCount}`,
  `features=${raw.panels.reduce((n, p) => n + (p.features?.length || 0), 0)}`
);
