/** Outline edge model: line | arc (MakerHub-compatible Angle).
 * Tessellate to polyline points for display / nest / area.
 */

import { closeRing, num } from "./poly.js";

/** @typedef {{ type: 'line', x0: number, y0: number, x1: number, y1: number }} LineEdge */
/** @typedef {{ type: 'arc', x0: number, y0: number, x1: number, y1: number, angleDeg: number }} ArcEdge */
/** @typedef {LineEdge | ArcEdge} Edge */

export function lineEdge(x0, y0, x1, y1) {
  return {
    type: "line",
    x0: num(x0),
    y0: num(y0),
    x1: num(x1),
    y1: num(y1),
  };
}

export function arcEdge(x0, y0, x1, y1, angleDeg) {
  const a = num(angleDeg);
  if (Math.abs(a) < 1e-9) return lineEdge(x0, y0, x1, y1);
  return {
    type: "arc",
    x0: num(x0),
    y0: num(y0),
    x1: num(x1),
    y1: num(y1),
    angleDeg: a,
  };
}

/** Polyline points → consecutive line edges (open or closed ring). */
export function pointsToEdges(points, { closed = true } = {}) {
  const pts = (points || []).map((p) => [num(p[0]), num(p[1])]);
  if (pts.length < 2) return [];
  let list = pts;
  if (closed && list.length >= 2) {
    const a = list[0];
    const b = list[list.length - 1];
    if (a[0] === b[0] && a[1] === b[1]) list = list.slice(0, -1);
  }
  const edges = [];
  for (let i = 0; i < list.length - 1; i++) {
    edges.push(lineEdge(list[i][0], list[i][1], list[i + 1][0], list[i + 1][1]));
  }
  if (closed && list.length >= 3) {
    const last = list[list.length - 1];
    const first = list[0];
    edges.push(lineEdge(last[0], last[1], first[0], first[1]));
  }
  return edges;
}

/**
 * MakerHub Outline segment: { StartPoint:{X,Y}, EndPoint:{X,Y}, Angle }
 * Angle in degrees; 0 => line. Sign = arc orientation (CCW positive).
 */
export function fromMakerHubOutline(segments) {
  const edges = [];
  for (const seg of segments || []) {
    const s = seg.StartPoint || seg.startPoint || {};
    const e = seg.EndPoint || seg.endPoint || {};
    const x0 = num(s.X ?? s.x);
    const y0 = num(s.Y ?? s.y);
    const x1 = num(e.X ?? e.x);
    const y1 = num(e.Y ?? e.y);
    const angle = num(seg.Angle ?? seg.angle);
    edges.push(arcEdge(x0, y0, x1, y1, angle));
  }
  return edges;
}

export function toMakerHubOutline(edges) {
  return (edges || []).map((ed) => ({
    StartPoint: { X: ed.x0, Y: ed.y0, Z: 0 },
    EndPoint: { X: ed.x1, Y: ed.y1, Z: 0 },
    Angle: ed.type === "arc" ? num(ed.angleDeg) : 0,
  }));
}

/**
 * Arc through start→end with central angle angleDeg (signed).
 * Returns intermediate points excluding start, including end.
 */
export function tessellateArc(x0, y0, x1, y1, angleDeg, segments = 0) {
  const ang = (num(angleDeg) * Math.PI) / 180;
  if (Math.abs(ang) < 1e-12) return [[x1, y1]];
  const dx = x1 - x0;
  const dy = y1 - y0;
  const chord = Math.hypot(dx, dy);
  if (chord < 1e-12) return [[x1, y1]];
  const sinHalf = Math.sin(ang / 2);
  if (Math.abs(sinHalf) < 1e-12) return [[x1, y1]];
  const radius = Math.abs(chord / (2 * sinHalf));
  // mid-chord → center direction (perpendicular)
  const mx = (x0 + x1) / 2;
  const my = (y0 + y1) / 2;
  const nx = -dy / chord;
  const ny = dx / chord;
  const h = Math.sqrt(Math.max(0, radius * radius - (chord * 0.5) ** 2));
  // sign: positive angle → CCW, center on left of directed chord
  const side = ang > 0 ? 1 : -1;
  // when |ang| > pi, center is on the other side of chord mid
  const obtuse = Math.abs(ang) > Math.PI ? -1 : 1;
  const cx = mx + nx * h * side * obtuse;
  const cy = my + ny * h * side * obtuse;

  let a0 = Math.atan2(y0 - cy, x0 - cx);
  let a1 = Math.atan2(y1 - cy, x1 - cx);
  // sweep from a0 by ang
  let sweep = ang;
  // normalize a1 to a0 + sweep
  const nSeg =
    segments > 0
      ? segments
      : Math.max(4, Math.ceil((Math.abs(ang) / (Math.PI / 12)) )); // ~15deg
  const out = [];
  for (let i = 1; i <= nSeg; i++) {
    const t = i / nSeg;
    const a = a0 + sweep * t;
    out.push([cx + Math.cos(a) * radius, cy + Math.sin(a) * radius]);
  }
  // snap end
  out[out.length - 1] = [x1, y1];
  return out;
}

/** Edges → polyline points (closed ring, first==last optional). */
export function edgesToPoints(edges, { close = true } = {}) {
  const list = edges || [];
  if (!list.length) return [];
  const pts = [[list[0].x0, list[0].y0]];
  for (const ed of list) {
    if (ed.type === "arc" && Math.abs(num(ed.angleDeg)) >= 1e-9) {
      const mid = tessellateArc(ed.x0, ed.y0, ed.x1, ed.y1, ed.angleDeg);
      for (const p of mid) pts.push(p);
    } else {
      pts.push([ed.x1, ed.y1]);
    }
  }
  if (close) return closeRing(pts);
  return pts;
}

export function toPolyline(edges, opts) {
  return edgesToPoints(edges, opts);
}

export function addLineEdge(edges, x0, y0, x1, y1) {
  return [...(edges || []), lineEdge(x0, y0, x1, y1)];
}

export function removeEdge(edges, index) {
  const list = [...(edges || [])];
  const i = Number(index);
  if (i < 0 || i >= list.length) return list;
  list.splice(i, 1);
  return list;
}

/** Set sweep angle on edge; 0 demotes to line. */
export function setEdgeAngle(edges, index, angleDeg) {
  const list = (edges || []).map((e) => ({ ...e }));
  const i = Number(index);
  if (i < 0 || i >= list.length) return list;
  const ed = list[i];
  const a = num(angleDeg);
  if (Math.abs(a) < 1e-9) {
    list[i] = lineEdge(ed.x0, ed.y0, ed.x1, ed.y1);
  } else {
    list[i] = arcEdge(ed.x0, ed.y0, ed.x1, ed.y1, a);
  }
  return list;
}

/** Ensure panel.outline.edges exists and points match tessellation. */
export function syncOutlineFromEdges(panel) {
  const next = structuredClone(panel);
  const edges = next.outline?.edges;
  if (!Array.isArray(edges) || !edges.length) {
    const pts = next.outline?.points || [];
    next.outline = {
      points: pts,
      edges: pointsToEdges(pts, { closed: true }),
    };
    return next;
  }
  next.outline = {
    edges: edges.map((e) =>
      e.type === "arc"
        ? arcEdge(e.x0, e.y0, e.x1, e.y1, e.angleDeg)
        : lineEdge(e.x0, e.y0, e.x1, e.y1)
    ),
    points: edgesToPoints(edges, { close: true }),
  };
  return next;
}

export function ensureOutlineEdges(panel) {
  if (Array.isArray(panel?.outline?.edges) && panel.outline.edges.length) {
    return syncOutlineFromEdges(panel);
  }
  const pts = panel?.outline?.points || [];
  const next = structuredClone(panel);
  next.outline = {
    points: pts,
    edges: pointsToEdges(pts, { closed: true }),
  };
  return next;
}
