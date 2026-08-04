/** Materials / nest-settings helpers. */
import { pathToFileURL } from "node:url";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { readFileSync } from "node:fs";

const root = dirname(fileURLToPath(import.meta.url));
const { sheetSummary, materialsFromPackage, nestSettingsOf, applyStockSheet } = await import(
  pathToFileURL(join(root, "..", "src", "materials.js")).href
);
const demo = JSON.parse(
  readFileSync(join(root, "..", "public", "samples", "demo_cut_package.json"), "utf8")
);

const errors = [];
function assert(cond, msg) {
  if (!cond) errors.push(msg);
}

const sum = sheetSummary(demo.sheets[0]);
assert(sum.material === "oak", "oak material");
assert(sum.widthMm === 1220, "sheet w");

const mats = materialsFromPackage(demo);
assert(mats.some((m) => m.material === "oak"), "materials list");

const nest = nestSettingsOf(demo);
assert(nest.spacingMm > 0 && nest.borderMm > 0, "nest settings");

const patched = JSON.parse(JSON.stringify(demo));
const stock = applyStockSheet(patched, {
  widthMm: 1000,
  lengthMm: 2000,
  thicknessMm: 15,
  material: "ply",
});
assert(stock.widthMm === 1000 && stock.lengthMm === 2000, "stock size");
assert(stock.material === "ply" && stock.thicknessMm === 15, "stock mat");
assert(patched.sheets[0].widthMm === 1000, "pkg sheet mutated");

if (errors.length) {
  console.error("FAIL", errors);
  process.exit(1);
}
console.log("OK materials", `mats=${mats.length}`, `gap=${nest.spacingMm}`, `stock=${stock.material}`);
