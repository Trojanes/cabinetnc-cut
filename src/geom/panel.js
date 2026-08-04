/** Panel geometric model — outline + MakerHub-like P0 features. */

import {
  area,
  bbox,
  closeRing,
  num,
  perimeter,
  rectOutline,
  rotate as rotPts,
  scale as scalePts,
  translate as movePts,
} from "./poly.js";

let _seq = 1;
export function nextFeatureId(prefix = "F") {
  return `${prefix}${_seq++}`;
}

export function createRectPanel({
  panelId = "P1",
  widthMm = 600,
  heightMm = 400,
  thicknessMm = 18,
  faceUp = "A",
} = {}) {
  const points = rectOutline(widthMm, heightMm);
  return {
    panelId: String(panelId),
    thicknessMm: num(thicknessMm, 18),
    faceUp: faceUp || "A",
    outline: {
      points,
      edges: [
        { type: "line", x0: 0, y0: 0, x1: widthMm, y1: 0 },
        { type: "line", x0: widthMm, y0: 0, x1: widthMm, y1: heightMm },
        { type: "line", x0: widthMm, y0: heightMm, x1: 0, y1: heightMm },
        { type: "line", x0: 0, y0: heightMm, x1: 0, y1: 0 },
      ],
    },
    holes: [],
    features: [],
  };
}

export function clonePanel(panel) {
  return structuredClone(panel);
}

export function panelBbox(panel) {
  return bbox(panel?.outline?.points || []);
}

export function panelMetrics(panel) {
  const pts = panel?.outline?.points || [];
  return {
    areaMm2: area(pts),
    perimeterMm: perimeter(pts),
    bbox: bbox(pts),
  };
}

export function transformPanel(panel, fnPoints) {
  const next = clonePanel(panel);
  next.outline.points = fnPoints(next.outline.points || []);
  if (Array.isArray(next.outline.edges)) {
    next.outline.edges = next.outline.edges.map((ed) => {
      const [[x0, y0]] = fnPoints([[ed.x0, ed.y0]]);
      const [[x1, y1]] = fnPoints([[ed.x1, ed.y1]]);
      if (ed.type === "arc") {
        return { type: "arc", x0, y0, x1, y1, angleDeg: ed.angleDeg };
      }
      return { type: "line", x0, y0, x1, y1 };
    });
  }
  next.holes = (next.holes || []).map((h) => ({
    ...h,
    points: fnPoints(h.points || []),
  }));
  next.features = (next.features || []).map((f) => transformFeature(f, fnPoints));
  return next;
}

function transformFeature(f, fnPoints) {
  if (!f) return f;
  if (f.kind === "holeVertical") {
    const [[x, y]] = fnPoints([[num(f.x), num(f.y)]]);
    return { ...f, x, y };
  }
  if (f.kind === "grooveVertical") {
    return { ...f, path: fnPoints(f.path || []) };
  }
  return f;
}

export function translatePanel(panel, dx, dy) {
  return transformPanel(panel, (pts) => movePts(pts, dx, dy));
}

/** Move holes/grooves only — outline stays put (visible in Geom auto-frame view). */
export function translateFeatures(panel, dx, dy) {
  const next = clonePanel(panel);
  const shift = (pts) => movePts(pts, dx, dy);
  next.holes = (next.holes || []).map((h) => ({
    ...h,
    points: shift(h.points || []),
  }));
  next.features = (next.features || []).map((f) => transformFeature(f, shift));
  return next;
}

export function rotatePanel(panel, deg, ox, oy) {
  const box = panelBbox(panel);
  const cx = ox == null ? box.minX + box.width / 2 : num(ox);
  const cy = oy == null ? box.minY + box.height / 2 : num(oy);
  return transformPanel(panel, (pts) => rotPts(pts, deg, cx, cy));
}

export function scalePanel(panel, sx, sy, ox, oy) {
  const box = panelBbox(panel);
  const cx = ox == null ? box.minX : num(ox);
  const cy = oy == null ? box.minY : num(oy);
  return transformPanel(panel, (pts) => scalePts(pts, sx, sy, cx, cy));
}

export function addVerticalHole(panel, { x, y, diameterMm, depthMm, fromFace = "A", id } = {}) {
  const next = clonePanel(panel);
  const d = num(diameterMm, 8);
  const feat = {
    id: id || nextFeatureId("H"),
    kind: "holeVertical",
    x: num(x),
    y: num(y),
    diameterMm: d,
    depthMm: num(depthMm, next.thicknessMm),
    fromFace,
  };
  next.features.push(feat);
  // geometric hole ring for boolean-ish preview
  const r = d / 2;
  const ring = [];
  const steps = 16;
  for (let i = 0; i < steps; i++) {
    const a = (i / steps) * Math.PI * 2;
    ring.push([feat.x + Math.cos(a) * r, feat.y + Math.sin(a) * r]);
  }
  next.holes.push({ id: feat.id, points: closeRing(ring), source: "holeVertical" });
  return next;
}

export function addVerticalGroove(
  panel,
  { path, widthMm = 6, depthMm = 8, fromFace = "A", id } = {}
) {
  const next = clonePanel(panel);
  const pts = (path || []).map((p) => [num(p[0]), num(p[1])]);
  if (pts.length < 2) return next;
  next.features.push({
    id: id || nextFeatureId("G"),
    kind: "grooveVertical",
    path: pts,
    widthMm: num(widthMm, 6),
    depthMm: num(depthMm, 8),
    fromFace,
  });
  return next;
}

/** Set rectangular outline; rebuilds line edges. */
export function setRectOutline(panel, widthMm, heightMm) {
  const next = clonePanel(panel);
  const w = Math.max(num(widthMm), 1);
  const h = Math.max(num(heightMm), 1);
  next.outline = {
    points: rectOutline(w, h),
    edges: [
      { type: "line", x0: 0, y0: 0, x1: w, y1: 0 },
      { type: "line", x0: w, y0: 0, x1: w, y1: h },
      { type: "line", x0: w, y0: h, x1: 0, y1: h },
      { type: "line", x0: 0, y0: h, x1: 0, y1: 0 },
    ],
  };
  return next;
}

/**
 * Resize axis-aligned rect panel about min corner (0,0) after normalize.
 * Features/holes scale with sx/sy so relative layout is preserved.
 */
export function resizeRectKeepingFeatures(panel, widthMm, heightMm) {
  const box = panelBbox(panel);
  const w0 = Math.max(box.width, 1e-6);
  const h0 = Math.max(box.height, 1e-6);
  const w1 = Math.max(num(widthMm), 1);
  const h1 = Math.max(num(heightMm), 1);
  // move min to origin, scale, set clean rect outline
  let next = translatePanel(panel, -box.minX, -box.minY);
  next = scalePanel(next, w1 / w0, h1 / h0, 0, 0);
  next.outline = { points: rectOutline(w1, h1) };
  next.outline.edges = [
    { type: "line", x0: 0, y0: 0, x1: w1, y1: 0 },
    { type: "line", x0: w1, y0: 0, x1: w1, y1: h1 },
    { type: "line", x0: w1, y0: h1, x1: 0, y1: h1 },
    { type: "line", x0: 0, y0: h1, x1: 0, y1: 0 },
  ];
  // rebuild hole rings from hole features
  next.holes = [];
  for (const f of next.features || []) {
    if (f.kind !== "holeVertical") continue;
    const r = num(f.diameterMm, 8) / 2;
    const ring = [];
    for (let i = 0; i < 16; i++) {
      const a = (i / 16) * Math.PI * 2;
      ring.push([num(f.x) + Math.cos(a) * r, num(f.y) + Math.sin(a) * r]);
    }
    next.holes.push({ id: f.id, points: closeRing(ring), source: "holeVertical" });
  }
  return next;
}

export function moveHole(panel, featureId, x, y) {
  const next = clonePanel(panel);
  const feat = (next.features || []).find(
    (f) => f.kind === "holeVertical" && String(f.id) === String(featureId)
  );
  if (!feat) return next;
  feat.x = num(x);
  feat.y = num(y);
  const hole = (next.holes || []).find((h) => String(h.id) === String(featureId));
  const r = num(feat.diameterMm, 8) / 2;
  const ring = [];
  for (let i = 0; i < 16; i++) {
    const a = (i / 16) * Math.PI * 2;
    ring.push([feat.x + Math.cos(a) * r, feat.y + Math.sin(a) * r]);
  }
  const closed = closeRing(ring);
  if (hole) hole.points = closed;
  else next.holes.push({ id: feat.id, points: closed, source: "holeVertical" });
  return next;
}

export function moveGroovePoint(panel, featureId, pointIndex, x, y) {
  const next = clonePanel(panel);
  const feat = (next.features || []).find(
    (f) => f.kind === "grooveVertical" && String(f.id) === String(featureId)
  );
  if (!feat || !Array.isArray(feat.path)) return next;
  const i = Number(pointIndex);
  if (i < 0 || i >= feat.path.length) return next;
  feat.path[i] = [num(x), num(y)];
  return next;
}

/** True if outline is an axis-aligned rectangle (4 unique corners). */
export function isAxisAlignedRect(panel) {
  const pts = panel?.outline?.points || [];
  if (pts.length < 4) return false;
  const box = bbox(pts);
  const uniq = [];
  for (const p of pts) {
    const x = Math.round(num(p[0]) * 1000) / 1000;
    const y = Math.round(num(p[1]) * 1000) / 1000;
    if (!uniq.some((u) => u[0] === x && u[1] === y)) uniq.push([x, y]);
  }
  if (uniq.length !== 4) return false;
  return uniq.every(
    (p) =>
      (Math.abs(p[0] - box.minX) < 1e-6 || Math.abs(p[0] - box.maxX) < 1e-6) &&
      (Math.abs(p[1] - box.minY) < 1e-6 || Math.abs(p[1] - box.maxY) < 1e-6)
  );
}
