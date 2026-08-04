/** Nest verification helpers — poly + optional Clipper gap inflate (not full NFP). */

import { findNestCollisions, placementOutline, placementAabb, aabbsConflict } from "./pack.js";
import { polygonsOverlap } from "./geom/poly.js";

export function verifyNestPoly(panels, placements, spacingMm = 12) {
  const hits = findNestCollisions(panels, placements, spacingMm, { poly: true });
  return {
    ok: hits.length === 0,
    engine: "poly_outline",
    hits,
    hitCount: hits.length,
  };
}

/**
 * Inflate outlines by gap/2 via offsetFn, then test polygon overlap.
 * offsetFn(points, delta) → { points } | points[]  (sync or Promise)
 */
export async function verifyNestGapAsync(panels, placements, spacingMm, offsetFn) {
  const half = Math.max(0, Number(spacingMm) || 0) / 2;
  const byId = new Map((panels || []).map((p) => [String(p.panelId), p]));
  const list = placements || [];
  const inflated = new Map();

  async function outlineOf(place) {
    const key = `${place.panelId}@${place.sheetIndex}@${place.offsetX}@${place.offsetY}@${place.rotationDeg}`;
    if (inflated.has(key)) return inflated.get(key);
    const panel = byId.get(String(place.panelId));
    const base = placementOutline(panel, place);
    if (!base) {
      inflated.set(key, null);
      return null;
    }
    if (!(half > 0) || typeof offsetFn !== "function") {
      inflated.set(key, base);
      return base;
    }
    const off = await offsetFn(base, half);
    const pts = Array.isArray(off) ? off : off?.points;
    const ring = Array.isArray(pts) && pts.length >= 3 ? pts : base;
    inflated.set(key, ring);
    return ring;
  }

  const hits = [];
  for (let i = 0; i < list.length; i++) {
    const a = list[i];
    const pa = byId.get(String(a.panelId));
    if (!pa) continue;
    const boxA = placementAabb(pa, a);
    const outA = await outlineOf(a);
    for (let j = i + 1; j < list.length; j++) {
      const b = list[j];
      if (Number(a.sheetIndex || 0) !== Number(b.sheetIndex || 0)) continue;
      const pb = byId.get(String(b.panelId));
      if (!pb) continue;
      const boxB = placementAabb(pb, b);
      if (!aabbsConflict(boxA, boxB, Number(spacingMm) || 0)) continue;
      const outB = await outlineOf(b);
      if (outA && outB) {
        if (polygonsOverlap(outA, outB)) {
          hits.push({
            panelIdA: String(a.panelId),
            panelIdB: String(b.panelId),
            sheetIndex: Number(a.sheetIndex || 0),
          });
        }
      } else if (aabbsConflict(boxA, boxB, Number(spacingMm) || 0)) {
        hits.push({
          panelIdA: String(a.panelId),
          panelIdB: String(b.panelId),
          sheetIndex: Number(a.sheetIndex || 0),
        });
      }
    }
  }
  return {
    ok: hits.length === 0,
    engine: half > 0 ? "clipper_gap_inflate" : "poly_outline",
    hits,
    hitCount: hits.length,
  };
}
