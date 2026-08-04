/** Bridge cut-package panel ↔ geom panel model. */

import { closeRing, num } from "./poly.js";
import { createRectPanel } from "./panel.js";
import {
  edgesToPoints,
  ensureOutlineEdges,
  fromMakerHubOutline,
  pointsToEdges,
  syncOutlineFromEdges,
} from "./edges.js";

export function fromCutPackagePanel(raw) {
  if (!raw || typeof raw !== "object") return null;
  const pts = raw.outline?.points;
  const rawEdges = raw.outline?.edges;
  if ((!Array.isArray(pts) || pts.length < 3) && !Array.isArray(rawEdges)) {
    const w = num(raw.bbox?.widthMm, 100);
    const h = num(raw.bbox?.heightMm, 50);
    return createRectPanel({
      panelId: raw.panelId || "P",
      widthMm: w,
      heightMm: h,
      thicknessMm: raw.thicknessMm,
      faceUp: raw.faceUp || "A",
    });
  }
  let outline;
  if (Array.isArray(rawEdges) && rawEdges.length) {
    outline = {
      edges: rawEdges.map((e) =>
        e.type === "arc"
          ? {
              type: "arc",
              x0: num(e.x0),
              y0: num(e.y0),
              x1: num(e.x1),
              y1: num(e.y1),
              angleDeg: num(e.angleDeg),
            }
          : {
              type: "line",
              x0: num(e.x0),
              y0: num(e.y0),
              x1: num(e.x1),
              y1: num(e.y1),
            }
      ),
      points: edgesToPoints(rawEdges, { close: true }),
    };
  } else {
    const points = pts.map((p) => [num(p[0]), num(p[1])]);
    outline = { points, edges: pointsToEdges(points, { closed: true }) };
  }
  const panel = {
    panelId: String(raw.panelId || "P"),
    thicknessMm: num(raw.thicknessMm, 18),
    faceUp: raw.faceUp || "A",
    outline,
    holes: [],
    features: [],
  };
  for (const f of raw.features || []) {
    if (f.kind === "holeVertical") {
      panel.features.push({
        id: f.id || null,
        kind: "holeVertical",
        x: num(f.x),
        y: num(f.y),
        diameterMm: num(f.diameterMm, 8),
        depthMm: num(f.depthMm, panel.thicknessMm),
        fromFace: f.fromFace || "A",
      });
      const r = num(f.diameterMm, 8) / 2;
      const ring = [];
      for (let i = 0; i < 16; i++) {
        const a = (i / 16) * Math.PI * 2;
        ring.push([num(f.x) + Math.cos(a) * r, num(f.y) + Math.sin(a) * r]);
      }
      panel.holes.push({ id: f.id, points: closeRing(ring), source: "holeVertical" });
    } else if (f.kind === "grooveVertical") {
      panel.features.push({
        id: f.id || null,
        kind: "grooveVertical",
        path: (f.path || []).map((p) => [num(p[0]), num(p[1])]),
        widthMm: num(f.widthMm, 6),
        depthMm: num(f.depthMm, 8),
        fromFace: f.fromFace || "A",
      });
    }
  }
  return panel;
}

/** Import MakerHub-style Outline[] into a geom panel. */
export function panelFromMakerHubOutline(segments, meta = {}) {
  const edges = fromMakerHubOutline(segments);
  const points = edgesToPoints(edges, { close: true });
  return {
    panelId: String(meta.panelId || meta.ID || "MH1"),
    thicknessMm: num(meta.thicknessMm, 18),
    faceUp: meta.faceUp || "A",
    outline: { edges, points },
    holes: [],
    features: [],
  };
}

export function toCutPackagePanel(geom, base = {}) {
  const synced = ensureOutlineEdges(geom);
  const pts = synced.outline?.points || [];
  const box = (() => {
    if (!pts.length) return { widthMm: 0, heightMm: 0 };
    const xs = pts.map((p) => p[0]);
    const ys = pts.map((p) => p[1]);
    return {
      widthMm: Math.max(...xs) - Math.min(...xs),
      heightMm: Math.max(...ys) - Math.min(...ys),
    };
  })();
  return {
    ...base,
    panelId: synced.panelId,
    thicknessMm: synced.thicknessMm,
    faceUp: synced.faceUp,
    bbox: {
      widthMm: Math.round(box.widthMm * 1000) / 1000,
      heightMm: Math.round(box.heightMm * 1000) / 1000,
    },
    outline: {
      points: pts.map((p) => [p[0], p[1]]),
      edges: (synced.outline.edges || []).map((e) => ({ ...e })),
    },
    features: (synced.features || []).map((f) => ({ ...f })),
  };
}

export function writeBackToPackage(pkg, geomPanel) {
  const next = structuredClone(pkg);
  const idx = (next.panels || []).findIndex((p) => p.panelId === geomPanel.panelId);
  const converted = toCutPackagePanel(
    geomPanel,
    idx >= 0 ? next.panels[idx] : { panelId: geomPanel.panelId }
  );
  if (idx >= 0) next.panels[idx] = converted;
  else next.panels = [...(next.panels || []), converted];
  if (next.nestResult) delete next.nestResult;
  return next;
}

export { syncOutlineFromEdges, ensureOutlineEdges, fromMakerHubOutline };
