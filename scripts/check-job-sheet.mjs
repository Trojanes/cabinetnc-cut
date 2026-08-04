/** Job sheet HTML check. */
import { pathToFileURL } from "node:url";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { readFileSync } from "node:fs";

const root = dirname(fileURLToPath(import.meta.url));
const { buildJobSheetHtml } = await import(
  pathToFileURL(join(root, "..", "src", "job_sheet.js")).href
);
const demo = JSON.parse(
  readFileSync(join(root, "..", "public", "samples", "demo_cut_package.json"), "utf8")
);

const html = buildJobSheetHtml(demo, { name: "Nesting router", toolDiameterMm: 6, id: "r6" });
const errors = [];
if (!html.includes("<!DOCTYPE html>")) errors.push("doctype");
if (!html.includes("CabinetNC Cut")) errors.push("brand");
if (!html.includes("P1")) errors.push("panel P1");
if (!html.includes("打印")) errors.push("print btn");

if (errors.length) {
  console.error("FAIL job_sheet", errors);
  process.exit(1);
}
console.log("OK job_sheet", `bytes=${html.length}`);
