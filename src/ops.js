/** Map cut-package P0 features → cutting-machine intent ops (no NC yet). */

const OP_RANK = { contour: 0, drill: 1, groove: 2 };

/** Drop op kinds disabled on the machine profile (defaults all on). */
export function filterOpsEnabled(ops, profile) {
  const enableContour = profile?.enableContour !== false;
  const enableDrill = profile?.enableDrill !== false;
  const enableGroove = profile?.enableGroove !== false;
  return (ops || []).filter((o) => {
    if (o.op === "contour") return enableContour;
    if (o.op === "drill") return enableDrill;
    if (o.op === "groove") return enableGroove;
    return true;
  });
}

export function featuresToOps(panels) {
  const ops = [];
  for (const panel of panels || []) {
    const panelId = String(panel.panelId || "");
    const pts = panel?.outline?.points;
    if (Array.isArray(pts) && pts.length >= 3) {
      ops.push({
        op: "contour",
        panelId,
        featureId: null,
        path: pts,
        face: panel.faceUp || "A",
      });
    }
    for (const f of panel.features || []) {
      if (f.kind === "holeVertical") {
        ops.push({
          op: "drill",
          panelId,
          featureId: f.id || null,
          x: Number(f.x) || 0,
          y: Number(f.y) || 0,
          diameterMm: Number(f.diameterMm) || 0,
          depthMm: Number(f.depthMm) || 0,
          face: f.fromFace || "A",
        });
      } else if (f.kind === "grooveVertical") {
        ops.push({
          op: "groove",
          panelId,
          featureId: f.id || null,
          path: f.path || [],
          widthMm: Number(f.widthMm) || 0,
          depthMm: Number(f.depthMm) || 0,
          face: f.fromFace || "A",
        });
      }
    }
  }
  // Stable machine intent order: contour → drill → groove (within each panel group).
  ops.sort((a, b) => {
    if (a.panelId !== b.panelId) return String(a.panelId).localeCompare(String(b.panelId));
    return (OP_RANK[a.op] ?? 9) - (OP_RANK[b.op] ?? 9);
  });
  return ops;
}

function rotatePoint(x, y, deg) {
  const r = ((Number(deg) || 0) * Math.PI) / 180;
  const c = Math.cos(r);
  const s = Math.sin(r);
  return [x * c - y * s, x * s + y * c];
}

/** Attach nest placement fields (and sheet-frame coords for drills). */
export function attachOpsToNest(ops, nestResult) {
  const byId = new Map();
  for (const p of nestResult?.placements || []) {
    byId.set(String(p.panelId), p);
  }
  return (ops || []).map((op) => {
    const place = byId.get(String(op.panelId));
    if (!place) {
      return { ...op, placed: false };
    }
    const offsetX = Number(place.offsetX) || 0;
    const offsetY = Number(place.offsetY) || 0;
    const rotationDeg = Number(place.rotationDeg) || 0;
    const sheetIndex = Number(place.sheetIndex) || 0;
    const next = {
      ...op,
      placed: true,
      sheetIndex,
      offsetX,
      offsetY,
      rotationDeg,
    };
    if (op.op === "drill") {
      const [rx, ry] = rotatePoint(Number(op.x) || 0, Number(op.y) || 0, rotationDeg);
      next.sheetX = Math.round((rx + offsetX) * 1000) / 1000;
      next.sheetY = Math.round((ry + offsetY) * 1000) / 1000;
    } else if ((op.op === "contour" || op.op === "groove") && Array.isArray(op.path)) {
      next.path = op.path.map((p) => {
        const [rx, ry] = rotatePoint(Number(p[0]) || 0, Number(p[1]) || 0, rotationDeg);
        return [
          Math.round((rx + offsetX) * 1000) / 1000,
          Math.round((ry + offsetY) * 1000) / 1000,
        ];
      });
    }
    return next;
  });
}

/**
 * Inward tool-radius compensation on placed exterior contours.
 * Passes **-radiusMm** into offsetFn (Clipper: negative = shrink).
 * offsetFn(points, deltaMm) → { points, engine? } | points[]
 */
export function applyContourToolOffset(ops, radiusMm, offsetFn) {
  const r = Number(radiusMm) || 0;
  if (r <= 0 || typeof offsetFn !== "function") return ops || [];
  return (ops || []).map((op) => {
    if (op.op !== "contour" || !op.placed || !Array.isArray(op.path) || op.path.length < 3) {
      return op;
    }
    const off = offsetFn(op.path, -r);
    const points = Array.isArray(off) ? off : off?.points;
    if (!Array.isArray(points) || points.length < 3) return op;
    return {
      ...op,
      path: points,
      toolOffsetMm: r,
      offsetEngine: Array.isArray(off) ? undefined : off?.engine,
    };
  });
}

/** Async twin for browser `/api/offset` (offsetPolygonAsync). */
export async function applyContourToolOffsetAsync(ops, radiusMm, offsetFnAsync) {
  const r = Number(radiusMm) || 0;
  if (r <= 0 || typeof offsetFnAsync !== "function") return ops || [];
  const out = [];
  for (const op of ops || []) {
    if (op.op !== "contour" || !op.placed || !Array.isArray(op.path) || op.path.length < 3) {
      out.push(op);
      continue;
    }
    const off = await offsetFnAsync(op.path, -r);
    const points = Array.isArray(off) ? off : off?.points;
    if (!Array.isArray(points) || points.length < 3) {
      out.push(op);
      continue;
    }
    out.push({
      ...op,
      path: points,
      toolOffsetMm: r,
      offsetEngine: Array.isArray(off) ? undefined : off?.engine,
    });
  }
  return out;
}
