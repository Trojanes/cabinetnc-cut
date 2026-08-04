/**
 * Node-only: spawn cabinetnc_offset CLI when built; else JS offsetRect.
 * Do not import this from browser entry (main.js) — use native_offset.js there.
 */
import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import { offsetRect } from "./poly.js";

const here = dirname(fileURLToPath(import.meta.url));

export function defaultOffsetCliCandidates() {
  const root = join(here, "..", "..", "native", "cabinetnc_core", "build");
  return [
    process.env.CABINETNC_OFFSET_CLI,
    join(root, "cabinetnc_offset.exe"),
    join(root, "cabinetnc_offset"),
    join(root, "Release", "cabinetnc_offset.exe"),
    join(root, "Debug", "cabinetnc_offset.exe"),
  ].filter(Boolean);
}

export function resolveOffsetCli(candidates = defaultOffsetCliCandidates()) {
  for (const p of candidates) {
    if (p && existsSync(p)) return p;
  }
  return null;
}

function cliEnv() {
  const env = { ...process.env };
  const mingw =
    process.env.CABINETNC_MINGW_BIN ||
    join("d:", "project", "tools", "llvm-mingw", "bin");
  if (existsSync(mingw)) {
    env.PATH = `${mingw};${env.PATH || ""}`;
  }
  return env;
}

/**
 * @param {number[][]} points
 * @param {number} delta
 * @param {{ cliPath?: string|null }} [opts]
 */
export function offsetPolygon(points, delta, opts = {}) {
  const cli =
    opts.cliPath === undefined ? resolveOffsetCli() : opts.cliPath;
  if (cli) {
    const req = JSON.stringify({
      op: "offset",
      delta: Number(delta) || 0,
      polygons: [points || []],
    });
    const r = spawnSync(cli, [], {
      input: req,
      encoding: "utf8",
      windowsHide: true,
      maxBuffer: 4 * 1024 * 1024,
      env: cliEnv(),
    });
    if (!r.error) {
      try {
        const j = JSON.parse(String(r.stdout || "").trim());
        if (j && j.ok && Array.isArray(j.polygons) && j.polygons[0]) {
          return {
            points: j.polygons[0],
            engine: j.engine || "cabinetnc_core",
            mode: j.mode || "clipper_offset",
          };
        }
      } catch {
        /* JS fallback */
      }
    }
  }
  return {
    points: offsetRect(points, delta),
    engine: "js",
    mode: "offset_rect",
  };
}

/**
 * @param {number[][]} subject
 * @param {number[][][]} clips
 * @param {{ cliPath?: string|null }} [opts]
 */
export function differencePolygon(subject, clips, opts = {}) {
  const cli = opts.cliPath === undefined ? resolveOffsetCli() : opts.cliPath;
  const clipsOk = (clips || []).filter((c) => Array.isArray(c) && c.length >= 3);
  if (cli) {
    const req = JSON.stringify({
      op: "difference",
      subject: subject || [],
      clips: clipsOk,
    });
    const r = spawnSync(cli, [], {
      input: req,
      encoding: "utf8",
      windowsHide: true,
      maxBuffer: 4 * 1024 * 1024,
      env: cliEnv(),
    });
    if (!r.error) {
      try {
        const j = JSON.parse(String(r.stdout || "").trim());
        if (j && j.ok && Array.isArray(j.polygons) && j.polygons[0]) {
          return {
            points: j.polygons[0],
            polygons: j.polygons,
            engine: j.engine || "cabinetnc_core",
            mode: j.mode || "clipper_difference",
          };
        }
      } catch {
        /* JS fallback */
      }
    }
  }
  return {
    points: subject || [],
    polygons: subject?.length >= 3 ? [subject] : [],
    engine: "js",
    mode: "difference_passthrough",
  };
}
