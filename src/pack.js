/** Naive multi-sheet shelf pack when nestResult is missing.
 * ponytail: AABB shelf + optional 90° — upgrade to sheet_pack poly when ported.
 */

import { polygonsOverlap } from "./geom/poly.js";

function sizeFromPanel(p) {
  let w = Number(p?.bbox?.widthMm) || 0;
  let h = Number(p?.bbox?.heightMm) || 0;
  if (w > 0 && h > 0) return { w, h };
  const pts = p?.outline?.points;
  if (Array.isArray(pts) && pts.length >= 2) {
    const xs = pts.map((pt) => Number(pt[0]) || 0);
    const ys = pts.map((pt) => Number(pt[1]) || 0);
    w = Math.max(...xs) - Math.min(...xs);
    h = Math.max(...ys) - Math.min(...ys);
  }
  return { w, h };
}

function rotatePoint(x, y, deg) {
  const r = ((Number(deg) || 0) * Math.PI) / 180;
  const c = Math.cos(r);
  const s = Math.sin(r);
  return [x * c - y * s, x * s + y * c];
}

/** World-space AABB of a placed panel (outline if present, else bbox). */
export function placementAabb(panel, place) {
  const ox = Number(place?.offsetX) || 0;
  const oy = Number(place?.offsetY) || 0;
  const rot = Number(place?.rotationDeg) || 0;
  const pts = panel?.outline?.points;
  if (Array.isArray(pts) && pts.length >= 2) {
    const world = pts.map(([x, y]) => {
      const [rx, ry] = rotatePoint(Number(x) || 0, Number(y) || 0, rot);
      return [rx + ox, ry + oy];
    });
    const xs = world.map((p) => p[0]);
    const ys = world.map((p) => p[1]);
    return {
      minX: Math.min(...xs),
      minY: Math.min(...ys),
      maxX: Math.max(...xs),
      maxY: Math.max(...ys),
    };
  }
  const { w, h } = sizeFromPanel(panel);
  const ww = rot % 180 === 90 || rot % 180 === -90 ? h : w;
  const hh = rot % 180 === 90 || rot % 180 === -90 ? w : h;
  return { minX: ox, minY: oy, maxX: ox + ww, maxY: oy + hh };
}

/** World-space outline ring of a placed panel (null if no outline). */
export function placementOutline(panel, place) {
  const pts = panel?.outline?.points;
  if (!Array.isArray(pts) || pts.length < 3) return null;
  const ox = Number(place?.offsetX) || 0;
  const oy = Number(place?.offsetY) || 0;
  const rot = Number(place?.rotationDeg) || 0;
  return pts.map(([x, y]) => {
    const [rx, ry] = rotatePoint(Number(x) || 0, Number(y) || 0, rot);
    return [rx + ox, ry + oy];
  });
}

/** True if AABBs violate minimum gap (edge-to-edge clearance). */
export function aabbsConflict(a, b, gapMm = 0) {
  if (!a || !b) return false;
  const g = Math.max(0, Number(gapMm) || 0);
  return !(
    a.maxX + g <= b.minX ||
    b.maxX + g <= a.minX ||
    a.maxY + g <= b.minY ||
    b.maxY + g <= a.minY
  );
}

/**
 * List pairwise spacing/collision violations on a nest result.
 * options.poly: when both have outlines, AABB-overlap alone is not enough —
 * require polygon overlap (lets L-shapes nest into false AABB space).
 * Spacing gap still uses AABB when polys do not overlap.
 * @returns {{ panelIdA: string, panelIdB: string, sheetIndex: number }[]}
 */
export function findNestCollisions(panels, placements, spacingMm = 12, options = {}) {
  const byId = new Map();
  for (const p of panels || []) {
    if (p?.panelId) byId.set(String(p.panelId), p);
  }
  const gap = Number(spacingMm) || 0;
  const poly = Boolean(options?.poly);
  const list = placements || [];
  const hits = [];
  for (let i = 0; i < list.length; i++) {
    const a = list[i];
    const pa = byId.get(String(a.panelId));
    if (!pa) continue;
    const boxA = placementAabb(pa, a);
    const outA = poly ? placementOutline(pa, a) : null;
    for (let j = i + 1; j < list.length; j++) {
      const b = list[j];
      if (Number(a.sheetIndex || 0) !== Number(b.sheetIndex || 0)) continue;
      const pb = byId.get(String(b.panelId));
      if (!pb) continue;
      const boxB = placementAabb(pb, b);
      if (poly && outA) {
        const outB = placementOutline(pb, b);
        if (outB) {
          const aabb0 = aabbsConflict(boxA, boxB, 0);
          if (aabb0 && polygonsOverlap(outA, outB)) {
            hits.push({
              panelIdA: String(a.panelId),
              panelIdB: String(b.panelId),
              sheetIndex: Number(a.sheetIndex || 0),
            });
            continue;
          }
          if (!aabb0 && gap > 0 && aabbsConflict(boxA, boxB, gap)) {
            hits.push({
              panelIdA: String(a.panelId),
              panelIdB: String(b.panelId),
              sheetIndex: Number(a.sheetIndex || 0),
            });
          }
          continue;
        }
      }
      if (aabbsConflict(boxA, boxB, gap)) {
        hits.push({
          panelIdA: String(a.panelId),
          panelIdB: String(b.panelId),
          sheetIndex: Number(a.sheetIndex || 0),
        });
      }
    }
  }
  return hits;
}

/** Global nestSettings.allowRotation, then per-panel rotatable / grainLocked. */
function panelMayRotate(panel, allowRotation) {
  if (!allowRotation) return false;
  if (panel?.grainLocked === true) return false;
  if (panel?.rotatable === false) return false;
  return true;
}

function orientationsFor(item, allowRotation) {
  const base = [{ w: item.w, h: item.h, rotationDeg: 0 }];
  if (!allowRotation || Math.abs(item.w - item.h) < 1e-9) return base;
  return [...base, { w: item.h, h: item.w, rotationDeg: 90 }];
}

/**
 * @param {object[]} panels
 * @param {object} sheet
 * @param {number} [spacingMm=12]
 * @param {number} [borderMm=15]
 * @param {{ allowRotation?: boolean }} [options]
 */
export function shelfPack(panels, sheet, spacingMm = 12, borderMm = 15, options = {}) {
  const width = Number(sheet?.widthMm) || 1220;
  const height = Number(sheet?.lengthMm) || 2440;
  const gap = Number(spacingMm) || 12;
  const border = Number(borderMm) || 15;
  const allowRotation = Boolean(options?.allowRotation);

  const items = (panels || [])
    .map((p) => {
      const { w, h } = sizeFromPanel(p);
      return {
        panelId: String(p.panelId),
        w,
        h,
        mayRotate: panelMayRotate(p, allowRotation),
      };
    })
    .filter((p) => p.w > 0 && p.h > 0)
    .sort((a, b) => Math.max(b.h, b.w) - Math.max(a.h, a.w) || b.h - a.h || b.w - a.w);

  const placements = [];
  const unplaced = [];
  let sheetIndex = 0;
  let x = border;
  let y = border;
  let rowH = 0;

  function newSheet() {
    sheetIndex += 1;
    x = border;
    y = border;
    rowH = 0;
  }

  function fitsSheet(w, h) {
    return w + border * 2 <= width && h + border * 2 <= height;
  }

  function fitsRow(w, h) {
    return x + w <= width - border && y + h <= height - border;
  }

  for (const item of items) {
    const orients = orientationsFor(item, item.mayRotate).filter((o) => fitsSheet(o.w, o.h));
    if (!orients.length) {
      unplaced.push(item.panelId);
      continue;
    }

    let chosen = orients.find((o) => fitsRow(o.w, o.h));
    if (!chosen) {
      // wrap row
      x = border;
      y += rowH + gap;
      rowH = 0;
      chosen = orients.find((o) => fitsRow(o.w, o.h));
    }
    if (!chosen) {
      newSheet();
      chosen = orients.find((o) => fitsRow(o.w, o.h));
    }
    if (!chosen) {
      unplaced.push(item.panelId);
      continue;
    }

    placements.push({
      panelId: item.panelId,
      sheetIndex,
      offsetX: x,
      offsetY: y,
      rotationDeg: chosen.rotationDeg,
    });
    x += chosen.w + gap;
    rowH = Math.max(rowH, chosen.h);
  }

  return {
    engine: allowRotation ? "browser_shelf_v1" : "browser_shelf_v0",
    placements,
    sheetCount: placements.length ? sheetIndex + 1 : 0,
    unplacedCount: unplaced.length,
    unplaced,
    sheetSize: { widthMm: width, lengthMm: height },
  };
}

/**
 * Bottom-left fill with free-rect list (better utilization than single-row shelf).
 * ponytail: AABB free-rects — options.polyCollision rejects candidates by outline overlap
 * (not full NFP; upgrade path: true NFP / Clipper).
 */
export function blfPack(panels, sheet, spacingMm = 12, borderMm = 15, options = {}) {
  const width = Number(sheet?.widthMm) || 1220;
  const height = Number(sheet?.lengthMm) || 2440;
  const gap = Number(spacingMm) || 12;
  const border = Number(borderMm) || 15;
  const allowRotation = Boolean(options?.allowRotation);
  const polyCollision = Boolean(options?.polyCollision);
  const innerW = width - border * 2;
  const innerH = height - border * 2;

  const lockedSeeds = (options.lockedPlacements || []).filter((p) => p && p.panelId != null);
  const lockedIds = new Set(lockedSeeds.map((p) => String(p.panelId)));
  const byId = new Map((panels || []).map((p) => [String(p.panelId), p]));

  const items = (panels || [])
    .map((p) => {
      const { w, h } = sizeFromPanel(p);
      return {
        panelId: String(p.panelId),
        w,
        h,
        mayRotate: panelMayRotate(p, allowRotation),
      };
    })
    .filter((p) => p.w > 0 && p.h > 0 && !lockedIds.has(p.panelId))
    .sort((a, b) => b.w * b.h - a.w * a.h || Math.max(b.h, b.w) - Math.max(a.h, a.w));

  const placements = [];
  const unplaced = [];
  let sheetIndex = 0;
  /** @type {{x:number,y:number,w:number,h:number}[]} */
  let free = [{ x: border, y: border, w: innerW, h: innerH }];

  function splitFree(_fr, x, y, w, h) {
    const next = [];
    for (const r of free) {
      if (x + w <= r.x || x >= r.x + r.w || y + h <= r.y || y >= r.y + r.h) {
        next.push(r);
        continue;
      }
      if (x > r.x) next.push({ x: r.x, y: r.y, w: x - r.x, h: r.h });
      if (x + w < r.x + r.w) next.push({ x: x + w, y: r.y, w: r.x + r.w - (x + w), h: r.h });
      if (y > r.y) next.push({ x: r.x, y: r.y, w: r.w, h: y - r.y });
      if (y + h < r.y + r.h) next.push({ x: r.x, y: y + h, w: r.w, h: r.y + r.h - (y + h) });
    }
    free = next.filter((a) => a.w >= 1 && a.h >= 1).filter((a, i, arr) => {
      return !arr.some(
        (b, j) =>
          i !== j &&
          a.x >= b.x &&
          a.y >= b.y &&
          a.x + a.w <= b.x + b.w &&
          a.y + a.h <= b.y + b.h
      );
    });
  }

  function occupyPlacement(place) {
    const panel = byId.get(String(place.panelId));
    const box = placementAabb(panel || { bbox: { widthMm: 1, heightMm: 1 } }, place);
    const w = Math.max(1, box.maxX - box.minX);
    const h = Math.max(1, box.maxY - box.minY);
    splitFree(null, box.minX, box.minY, w + gap, h + gap);
  }

  function conflictsPoly(panel, place) {
    const out = placementOutline(panel, place);
    const box = placementAabb(panel, place);
    for (const p of placements) {
      if (Number(p.sheetIndex || 0) !== Number(place.sheetIndex || 0)) continue;
      const other = byId.get(String(p.panelId));
      if (!other) continue;
      const boxB = placementAabb(other, p);
      if (!out) {
        if (aabbsConflict(box, boxB, gap)) return true;
        continue;
      }
      const outB = placementOutline(other, p);
      if (outB) {
        if (aabbsConflict(box, boxB, 0) && polygonsOverlap(out, outB)) return true;
        if (!aabbsConflict(box, boxB, 0) && gap > 0 && aabbsConflict(box, boxB, gap)) return true;
        continue;
      }
      if (aabbsConflict(box, boxB, gap)) return true;
    }
    return false;
  }

  function seedSheet(idx) {
    sheetIndex = idx;
    free = [{ x: border, y: border, w: innerW, h: innerH }];
    for (const seed of lockedSeeds) {
      if (Number(seed.sheetIndex || 0) !== Number(idx)) continue;
      const kept = {
        panelId: String(seed.panelId),
        sheetIndex: idx,
        offsetX: Number(seed.offsetX) || 0,
        offsetY: Number(seed.offsetY) || 0,
        rotationDeg: Number(seed.rotationDeg) || 0,
        locked: true,
      };
      placements.push(kept);
      occupyPlacement(kept);
    }
  }

  function resetSheet() {
    seedSheet(sheetIndex + 1);
  }

  seedSheet(0);

  for (const item of items) {
    const orients = orientationsFor(item, item.mayRotate).filter(
      (o) => o.w <= innerW && o.h <= innerH
    );
    if (!orients.length) {
      unplaced.push(item.panelId);
      continue;
    }

    let placed = false;
    for (let attempt = 0; attempt < 8 && !placed; attempt++) {
      const candidates = [];
      for (const o of orients) {
        for (const fr of free) {
          if (fr.w + 1e-9 < o.w || fr.h + 1e-9 < o.h) continue;
          candidates.push({ x: fr.x, y: fr.y, w: o.w, h: o.h, rotationDeg: o.rotationDeg });
        }
      }
      candidates.sort((a, b) => a.y - b.y || a.x - b.x);
      const panel = byId.get(item.panelId);
      for (const best of candidates) {
        const place = {
          panelId: item.panelId,
          sheetIndex,
          offsetX: best.x,
          offsetY: best.y,
          rotationDeg: best.rotationDeg,
        };
        if (polyCollision && panel && conflictsPoly(panel, place)) continue;
        placements.push(place);
        splitFree(null, best.x, best.y, best.w + gap, best.h + gap);
        placed = true;
        break;
      }
      if (!placed) {
        if (
          placements.some((p) => p.sheetIndex === sheetIndex && !p.locked) ||
          lockedSeeds.some((p) => Number(p.sheetIndex || 0) === sheetIndex)
        ) {
          resetSheet();
        } else {
          break;
        }
      }
    }
    if (!placed) unplaced.push(item.panelId);
  }

  // locked seeds on sheets never opened still kept
  for (const seed of lockedSeeds) {
    const idx = Number(seed.sheetIndex || 0);
    if (placements.some((p) => String(p.panelId) === String(seed.panelId))) continue;
    placements.push({
      panelId: String(seed.panelId),
      sheetIndex: idx,
      offsetX: Number(seed.offsetX) || 0,
      offsetY: Number(seed.offsetY) || 0,
      rotationDeg: Number(seed.rotationDeg) || 0,
      locked: true,
    });
  }

  const maxSheet = placements.reduce((m, p) => Math.max(m, Number(p.sheetIndex) || 0), -1);

  return {
    engine: polyCollision ? "browser_blf_poly_v0" : "browser_blf_v0",
    placements,
    sheetCount: placements.length ? maxSheet + 1 : 0,
    unplacedCount: unplaced.length,
    unplaced,
    sheetSize: { widthMm: width, lengthMm: height },
  };
}

/** Panel area from outline or bbox. */
export function panelAreaMm2(panel) {
  const pts = panel?.outline?.points;
  if (Array.isArray(pts) && pts.length >= 3) {
    let a = 0;
    for (let i = 0, n = pts.length; i < n; i++) {
      const p0 = pts[i];
      const p1 = pts[(i + 1) % n];
      a += (Number(p0[0]) || 0) * (Number(p1[1]) || 0) - (Number(p1[0]) || 0) * (Number(p0[1]) || 0);
    }
    return Math.abs(a) / 2;
  }
  const { w, h } = sizeFromPanel(panel);
  return w * h;
}

/**
 * Utilization + collision summary for a nest result.
 */
export function nestStats(panels, nestResult, spacingMm = 12, options = {}) {
  const sheetW = Number(nestResult?.sheetSize?.widthMm) || 1220;
  const sheetH = Number(nestResult?.sheetSize?.lengthMm) || 2440;
  const sheetCount = Math.max(1, Number(nestResult?.sheetCount) || 1);
  const sheetArea = sheetW * sheetH * sheetCount;
  const byId = new Map((panels || []).map((p) => [String(p.panelId), p]));
  let used = 0;
  for (const pl of nestResult?.placements || []) {
    const p = byId.get(String(pl.panelId));
    if (p) used += panelAreaMm2(p);
  }
  const poly =
    Boolean(options?.poly) || String(nestResult?.engine || "").includes("poly");
  const collisions = findNestCollisions(panels, nestResult?.placements, spacingMm, {
    poly,
  });
  const utilizationPct = sheetArea > 0 ? (used / sheetArea) * 100 : 0;
  return {
    usedAreaMm2: used,
    sheetAreaMm2: sheetArea,
    sheetCount,
    utilizationPct,
    collisions,
    collisionCount: collisions.length,
    unplacedCount: Number(nestResult?.unplacedCount) || 0,
  };
}

/**
 * Pack with BLF, fall back to shelf if BLF leaves more unplaced.
 * options.engine: "auto" | "blf" | "shelf" | "poly"
 * When lockedPlacements present, BLF only (seeds free space around locks).
 */
export function packPanels(panels, sheet, spacingMm = 12, borderMm = 15, options = {}) {
  const engine = options.engine || "auto";
  const poly = engine === "poly";
  const packOpts = poly ? { ...options, polyCollision: true } : options;
  const statsOpts = poly ? { poly: true } : {};
  if (poly || (options.lockedPlacements || []).length || engine === "blf") {
    const a = blfPack(panels, sheet, spacingMm, borderMm, packOpts);
    return { ...a, stats: nestStats(panels, a, spacingMm, statsOpts) };
  }
  if (engine === "shelf") {
    const b = shelfPack(panels, sheet, spacingMm, borderMm, options);
    return { ...b, stats: nestStats(panels, b, spacingMm) };
  }
  const a = blfPack(panels, sheet, spacingMm, borderMm, options);
  const b = shelfPack(panels, sheet, spacingMm, borderMm, options);
  const pick =
    a.unplacedCount < b.unplacedCount
      ? a
      : a.unplacedCount > b.unplacedCount
        ? b
        : nestStats(panels, a, spacingMm).utilizationPct >=
            nestStats(panels, b, spacingMm).utilizationPct
          ? a
          : b;
  const stats = nestStats(panels, pick, spacingMm);
  return { ...pick, stats };
}
