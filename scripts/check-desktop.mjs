/** Desktop / Tauri spike gate (VISION M6) — no Tauri binary required yet. */
import { existsSync, readFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const errors = [];
function assert(cond, msg) {
  if (!cond) errors.push(msg);
}

const intentPath = join(root, "desktop", "tauri-intent.json");
assert(existsSync(intentPath), "desktop/tauri-intent.json missing");
const intent = JSON.parse(readFileSync(intentPath, "utf8"));
assert(intent.productName === "CabinetNC Cut", "productName");
assert(intent.status === "spike-not-wired", "status spike");
assert(intent.devUrl?.includes("5177"), "devUrl");
assert(intent.window?.width >= 960, "window size");
assert(Array.isArray(intent.blockedOn) && intent.blockedOn.length > 0, "blockedOn");

const portableScript = join(root, "scripts", "make-portable.mjs");
assert(existsSync(portableScript), "make-portable.mjs");
const src = readFileSync(portableScript, "utf8");
assert(src.includes("tauri-intent") || src.includes("DESKTOP") || src.includes("M6"), "portable mentions desktop next");
assert(src.includes("INSTALL.txt"), "INSTALL.txt for shop drop");
assert(src.includes("Compress-Archive") || src.includes("zip"), "zip pack");

if (errors.length) {
  console.error("FAIL desktop", errors);
  process.exit(1);
}
console.log("OK desktop", `intent=${intent.status}`, `window=${intent.window.width}x${intent.window.height}`);
