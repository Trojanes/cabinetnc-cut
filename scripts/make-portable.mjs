/**
 * Build a portable folder: vite dist + start.bat (needs Node on PATH).
 * DESKTOP / M6 next: see desktop/tauri-intent.json (Tauri spike, not wired).
 * ponytail: no Tauri yet — upgrade path = single-exe when required (VISION M6).
 */
import { spawnSync } from "node:child_process";
import { cpSync, existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = join(dirname(fileURLToPath(import.meta.url)), "..");
const out = join(root, "portable");
const dist = join(root, "dist");
const pkg = JSON.parse(readFileSync(join(root, "package.json"), "utf8"));
const version = pkg.version || "0.0.0";
const port = 5177;
const title = `CabinetNC Cut v${version}`;
const nativeCli = join(root, "native", "cabinetnc_core", "build", "cabinetnc_offset.exe");

const build = spawnSync("npm", ["run", "build"], {
  cwd: root,
  encoding: "utf8",
  shell: true,
  stdio: "inherit",
});
if (build.status !== 0) process.exit(build.status || 1);

if (existsSync(out)) rmSync(out, { recursive: true, force: true });
mkdirSync(out, { recursive: true });
cpSync(dist, join(out, "app"), { recursive: true });

let nativeNote = "Native Clipper CLI not found in this build (web-only pack).";
if (existsSync(nativeCli)) {
  mkdirSync(join(out, "native"), { recursive: true });
  cpSync(nativeCli, join(out, "native", "cabinetnc_offset.exe"));
  nativeNote =
    "Native CLI copied to native/cabinetnc_offset.exe (use with full npm run dev checkout for /api/offset).";
}

const intentSrc = join(root, "desktop", "tauri-intent.json");
if (existsSync(intentSrc)) {
  mkdirSync(join(out, "desktop"), { recursive: true });
  cpSync(intentSrc, join(out, "desktop", "tauri-intent.json"));
}

writeFileSync(
  join(out, "start.bat"),
  `@echo off
title ${title}
cd /d "%~dp0"
where node >nul 2>&1
if errorlevel 1 (
  echo [${title}] Need Node.js on PATH.
  echo Install from https://nodejs.org then re-run start.bat
  pause
  exit /b 1
)
echo.
echo  ${title}
echo  Portable cutting station · MakerHub-depth shell
echo  Opening http://localhost:${port}/ ...
echo  Close this window to stop the server.
echo.
start "" "http://localhost:${port}/"
npx --yes serve -l ${port} app
`,
  "utf8"
);

writeFileSync(
  join(out, "VERSION.txt"),
  `${title}
built: ${new Date().toISOString()}
repo: cabinetnc-cut
native: ${existsSync(nativeCli) ? "bundled" : "missing"}
`,
  "utf8"
);

writeFileSync(
  join(out, "README.txt"),
  `${title} — portable web shell

1. Double-click start.bat (requires Node.js on PATH).
2. Browser opens http://localhost:${port}/
3. Import cut-package / Demo · 几何→排版→刀路→输出

Notes
- ${nativeNote}
- Desktop shell (Tauri) is a spike only — see desktop/tauri-intent.json (VISION M6).
- Rebuild: from repo root run  npm run portable

See docs/VISION.md for MakerHub-depth roadmap.
`,
  "utf8"
);

writeFileSync(
  join(out, "INSTALL.txt"),
  `${title} — shop install (no Tauri / no single-exe yet)

1. Copy this whole folder to the PC (USB ok).
2. Install Node.js LTS if missing: https://nodejs.org
3. Double-click start.bat
4. Optional: pin the browser tab; keep this window open while cutting.

Tauri native window is blocked until Rust toolchain is installed
(see desktop/tauri-intent.json). This portable pack is the current
deliverable "installer" substitute.
`,
  "utf8"
);

// Optional zip beside portable/ for USB drop
const zipPath = join(root, `CabinetNC-Cut-v${version}-portable.zip`);
try {
  if (existsSync(zipPath)) rmSync(zipPath, { force: true });
  const zip = spawnSync(
    "powershell",
    [
      "-NoProfile",
      "-Command",
      `Compress-Archive -Path '${out.replace(/'/g, "''")}\\*' -DestinationPath '${zipPath.replace(/'/g, "''")}' -Force`,
    ],
    { encoding: "utf8", windowsHide: true }
  );
  if (zip.status === 0 && existsSync(zipPath)) {
    console.log("OK zip →", zipPath);
  } else {
    console.log("zip skipped", zip.stderr || zip.status);
  }
} catch (e) {
  console.log("zip skipped", e.message || e);
}

console.log("OK portable →", out, title, existsSync(nativeCli) ? "+native" : "");
