/** 2D polyline helpers (mm). Cabinet-panel geometry kernel. */

export function num(v, d = 0) {
  const n = Number(v);
  return Number.isFinite(n) ? n : d;
}

export function bbox(points) {
  const pts = points || [];
  if (!pts.length) return { minX: 0, minY: 0, maxX: 0, maxY: 0, width: 0, height: 0 };
  let minX = Infinity;
  let minY = Infinity;
  let maxX = -Infinity;
  let maxY = -Infinity;
  for (const p of pts) {
    const x = num(p[0]);
    const y = num(p[1]);
    if (x < minX) minX = x;
    if (y < minY) minY = y;
    if (x > maxX) maxX = x;
    if (y > maxY) maxY = y;
  }
  return { minX, minY, maxX, maxY, width: maxX - minX, height: maxY - minY };
}

export function closeRing(points) {
  const pts = (points || []).map((p) => [num(p[0]), num(p[1])]);
  if (pts.length < 3) return pts;
  const a = pts[0];
  const b = pts[pts.length - 1];
  if (a[0] !== b[0] || a[1] !== b[1]) pts.push([a[0], a[1]]);
  return pts;
}

/** Shoelace area (signed). Absolute for magnitude. */
export function signedArea(points) {
  const pts = closeRing(points);
  let a = 0;
  for (let i = 0; i < pts.length - 1; i++) {
    a += pts[i][0] * pts[i + 1][1] - pts[i + 1][0] * pts[i][1];
  }
  return a / 2;
}

export function area(points) {
  return Math.abs(signedArea(points));
}

export function perimeter(points) {
  const pts = closeRing(points);
  let len = 0;
  for (let i = 0; i < pts.length - 1; i++) {
    const dx = pts[i + 1][0] - pts[i][0];
    const dy = pts[i + 1][1] - pts[i][1];
    len += Math.hypot(dx, dy);
  }
  return len;
}

export function translate(points, dx, dy) {
  const x = num(dx);
  const y = num(dy);
  return (points || []).map((p) => [num(p[0]) + x, num(p[1]) + y]);
}

export function rotate(points, deg, ox = 0, oy = 0) {
  const r = (num(deg) * Math.PI) / 180;
  const c = Math.cos(r);
  const s = Math.sin(r);
  const cx = num(ox);
  const cy = num(oy);
  return (points || []).map((p) => {
    const x = num(p[0]) - cx;
    const y = num(p[1]) - cy;
    return [cx + x * c - y * s, cy + x * s + y * c];
  });
}

export function scale(points, sx, sy = sx, ox = 0, oy = 0) {
  const ax = num(sx, 1);
  const ay = num(sy, ax);
  const cx = num(ox);
  const cy = num(oy);
  return (points || []).map((p) => [
    cx + (num(p[0]) - cx) * ax,
    cy + (num(p[1]) - cy) * ay,
  ]);
}

/** Normalize so bbox min → (0,0). Returns { points, dx, dy }. */
export function normalizeToOrigin(points) {
  const box = bbox(points);
  return {
    points: translate(points, -box.minX, -box.minY),
    dx: box.minX,
    dy: box.minY,
  };
}

export function pointInPolygon(x, y, points) {
  const pts = closeRing(points);
  let inside = false;
  for (let i = 0, j = pts.length - 1; i < pts.length; j = i++) {
    const xi = pts[i][0];
    const yi = pts[i][1];
    const xj = pts[j][0];
    const yj = pts[j][1];
    const intersect =
      yi > y !== yj > y && x < ((xj - xi) * (y - yi)) / (yj - yi + 1e-12) + xi;
    if (intersect) inside = !inside;
  }
  return inside;
}

function orient(ax, ay, bx, by, cx, cy) {
  return (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
}

function onSeg(ax, ay, bx, by, cx, cy) {
  return (
    Math.min(ax, bx) - 1e-9 <= cx &&
    cx <= Math.max(ax, bx) + 1e-9 &&
    Math.min(ay, by) - 1e-9 <= cy &&
    cy <= Math.max(ay, by) + 1e-9
  );
}

function segmentsCross(a, b, c, d) {
  const o1 = orient(a[0], a[1], b[0], b[1], c[0], c[1]);
  const o2 = orient(a[0], a[1], b[0], b[1], d[0], d[1]);
  const o3 = orient(c[0], c[1], d[0], d[1], a[0], a[1]);
  const o4 = orient(c[0], c[1], d[0], d[1], b[0], b[1]);
  if (o1 * o2 < 0 && o3 * o4 < 0) return true;
  if (Math.abs(o1) < 1e-9 && onSeg(a[0], a[1], b[0], b[1], c[0], c[1])) return true;
  if (Math.abs(o2) < 1e-9 && onSeg(a[0], a[1], b[0], b[1], d[0], d[1])) return true;
  if (Math.abs(o3) < 1e-9 && onSeg(c[0], c[1], d[0], d[1], a[0], a[1])) return true;
  if (Math.abs(o4) < 1e-9 && onSeg(c[0], c[1], d[0], d[1], b[0], b[1])) return true;
  return false;
}

/** True if closed rings overlap (edge cross or either vertex inside the other). */
export function polygonsOverlap(a, b) {
  const pa = closeRing(a);
  const pb = closeRing(b);
  if (pa.length < 4 || pb.length < 4) return false;
  for (let i = 0; i < pa.length - 1; i++) {
    for (let j = 0; j < pb.length - 1; j++) {
      if (segmentsCross(pa[i], pa[i + 1], pb[j], pb[j + 1])) return true;
    }
  }
  if (pointInPolygon(pa[0][0], pa[0][1], pb)) return true;
  if (pointInPolygon(pb[0][0], pb[0][1], pa)) return true;
  return false;
}

export function rectOutline(width, height, x0 = 0, y0 = 0) {
  const w = Math.max(0, num(width));
  const h = Math.max(0, num(height));
  const x = num(x0);
  const y = num(y0);
  return [
    [x, y],
    [x + w, y],
    [x + w, y + h],
    [x, y + h],
  ];
}

/**
 * Naive outward/inward offset for axis-aligned rectangle only.
 * ponytail: general polygon offset needs Clipper — upgrade path there.
 */
export function offsetRect(points, delta) {
  const box = bbox(points);
  const d = num(delta);
  const w = box.width + 2 * d;
  const h = box.height + 2 * d;
  if (w <= 0 || h <= 0) return [];
  return rectOutline(w, h, box.minX - d, box.minY - d);
}
