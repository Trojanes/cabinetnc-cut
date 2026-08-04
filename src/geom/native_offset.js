/**
 * Browser-safe offset facade.
 * Prefers POST /api/offset (Vite → cabinetnc_offset Clipper2); else JS offsetRect.
 */
import { offsetRect } from "./poly.js";

export function resolveOffsetCli() {
  return null;
}

export function defaultOffsetCliCandidates() {
  return [];
}

/** Sync fallback (rect only) — prefer offsetPolygonAsync in UI. */
export function offsetPolygon(points, delta, _opts = {}) {
  return {
    points: offsetRect(points, delta),
    engine: "js",
    mode: "offset_rect",
  };
}

/**
 * @param {number[][]} points
 * @param {number} delta
 * @returns {Promise<{ points: number[][], engine: string, mode: string }>}
 */
export async function offsetPolygonAsync(points, delta) {
  const d = Number(delta) || 0;
  try {
    const res = await fetch("/api/offset", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ op: "offset", delta: d, polygons: [points || []] }),
    });
    const j = await res.json();
    if (j && j.ok && Array.isArray(j.polygons) && j.polygons[0]?.length >= 3) {
      return {
        points: j.polygons[0],
        engine: j.engine || "cabinetnc_core",
        mode: j.mode || "clipper_offset",
      };
    }
  } catch {
    /* JS fallback */
  }
  return {
    points: offsetRect(points, d),
    engine: "js",
    mode: "offset_rect",
  };
}

/**
 * Subject minus hole clips via Clipper difference (POST /api/offset).
 * @param {number[][]} subject
 * @param {number[][][]} clips
 * @returns {Promise<{ points: number[][], polygons: number[][][], engine: string, mode: string }>}
 */
export async function differencePolygonAsync(subject, clips) {
  const clipsOk = (clips || []).filter((c) => Array.isArray(c) && c.length >= 3);
  try {
    const res = await fetch("/api/offset", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        op: "difference",
        subject: subject || [],
        clips: clipsOk,
      }),
    });
    const j = await res.json();
    if (j && j.ok && Array.isArray(j.polygons) && j.polygons[0]?.length >= 3) {
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
  return {
    points: subject || [],
    polygons: subject?.length >= 3 ? [subject] : [],
    engine: "js",
    mode: "difference_passthrough",
  };
}
