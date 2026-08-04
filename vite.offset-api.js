/**
 * Vite plugin: POST /api/offset → cabinetnc_offset CLI (Clipper2).
 * Dev-only bridge so the browser Geom UI can preview native offsets.
 */
import { spawnSync } from "node:child_process";
import { existsSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(fileURLToPath(import.meta.url));

function resolveCli() {
  if (process.env.CABINETNC_OFFSET_CLI && existsSync(process.env.CABINETNC_OFFSET_CLI)) {
    return process.env.CABINETNC_OFFSET_CLI;
  }
  const build = join(root, "native", "cabinetnc_core", "build");
  for (const p of [
    join(build, "cabinetnc_offset.exe"),
    join(build, "cabinetnc_offset"),
    join(build, "Release", "cabinetnc_offset.exe"),
    join(build, "Debug", "cabinetnc_offset.exe"),
  ]) {
    if (existsSync(p)) return p;
  }
  return null;
}

function cliEnv() {
  const env = { ...process.env };
  const mingw =
    process.env.CABINETNC_MINGW_BIN ||
    join("d:", "project", "tools", "llvm-mingw", "bin");
  if (existsSync(mingw)) env.PATH = `${mingw};${env.PATH || ""}`;
  return env;
}

function readBody(req) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    req.on("data", (c) => chunks.push(c));
    req.on("end", () => resolve(Buffer.concat(chunks).toString("utf8")));
    req.on("error", reject);
  });
}

export function cabinetncOffsetApi() {
  return {
    name: "cabinetnc-offset-api",
    configureServer(server) {
      server.middlewares.use(async (req, res, next) => {
        const url = req.url?.split("?")[0];
        if (url !== "/api/offset" || req.method !== "POST") return next();
        try {
          const body = await readBody(req);
          const cli = resolveCli();
          if (!cli) {
            res.statusCode = 503;
            res.setHeader("Content-Type", "application/json");
            res.end(
              JSON.stringify({
                ok: false,
                error: "cabinetnc_offset not built — run native/cabinetnc_core/build.ps1",
              })
            );
            return;
          }
          const r = spawnSync(cli, [], {
            input: body,
            encoding: "utf8",
            windowsHide: true,
            maxBuffer: 4 * 1024 * 1024,
            env: cliEnv(),
          });
          res.statusCode = r.status === 0 ? 200 : 400;
          res.setHeader("Content-Type", "application/json");
          const out = String(r.stdout || "").trim();
          res.end(out || JSON.stringify({ ok: false, error: r.stderr || "empty cli stdout" }));
        } catch (err) {
          res.statusCode = 500;
          res.setHeader("Content-Type", "application/json");
          res.end(JSON.stringify({ ok: false, error: String(err?.message || err) }));
        }
      });
    },
  };
}
