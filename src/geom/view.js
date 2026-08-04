/** Panel-local geometry edit canvas (not nest layout). */

import { num, bbox } from "./poly.js";
import {
  isAxisAlignedRect,
  panelBbox,
  moveHole,
  moveGroovePoint,
} from "./panel.js";

const HANDLE = 7;

function fmtMm(v) {
  const n = Number(v) || 0;
  return Math.abs(n - Math.round(n)) < 0.05 ? String(Math.round(n)) : n.toFixed(1);
}

export function geomView(canvas, panel, extraPoints = []) {
  const cssW = canvas.clientWidth || 800;
  const cssH = canvas.clientHeight || 600;
  let box = panelBbox(panel);
  const extras = extraPoints || [];
  if (extras.length) {
    const xb = bbox(extras);
    box = {
      minX: Math.min(box.minX, xb.minX),
      minY: Math.min(box.minY, xb.minY),
      maxX: Math.max(box.maxX, xb.maxX),
      maxY: Math.max(box.maxY, xb.maxY),
      width: 0,
      height: 0,
    };
    box.width = box.maxX - box.minX;
    box.height = box.maxY - box.minY;
  }
  const padMm = Math.max(box.width, box.height, 100) * 0.12 + 40;
  const worldW = Math.max(box.width + padMm * 2, 100);
  const worldH = Math.max(box.height + padMm * 2, 100);
  const originX = box.minX - padMm;
  const originY = box.minY - padMm;
  const scale = Math.min((cssW - 24) / worldW, (cssH - 24) / worldH);
  const ox = (cssW - worldW * scale) / 2;
  const oy = (cssH - worldH * scale) / 2;
  const toScreen = (x, y) => [
    ox + (x - originX) * scale,
    oy + (worldH - (y - originY)) * scale,
  ];
  const toLocal = (sx, sy) => [
    originX + (sx - ox) / scale,
    originY + worldH - (sy - oy) / scale,
  ];
  return { cssW, cssH, scale, ox, oy, originX, originY, worldW, worldH, box, toScreen, toLocal };
}

function drawGrid(ctx, view, step = 50) {
  const { toScreen, originX, originY, worldW, worldH } = view;
  const x0 = Math.floor(originX / step) * step;
  const y0 = Math.floor(originY / step) * step;
  for (let x = x0; x <= originX + worldW + step; x += step) {
    const [sx0, sy0] = toScreen(x, originY);
    const [, sy1] = toScreen(x, originY + worldH);
    ctx.beginPath();
    ctx.moveTo(sx0, sy0);
    ctx.lineTo(sx0, sy1);
    ctx.strokeStyle = Math.abs(x % (step * 2)) < 0.01 ? "#ccc" : "#e8e8e8";
    ctx.stroke();
  }
  for (let y = y0; y <= originY + worldH + step; y += step) {
    const [sx0, sy0] = toScreen(originX, y);
    const [sx1] = toScreen(originX + worldW, y);
    ctx.beginPath();
    ctx.moveTo(sx0, sy0);
    ctx.lineTo(sx1, sy0);
    ctx.strokeStyle = Math.abs(y % (step * 2)) < 0.01 ? "#ccc" : "#e8e8e8";
    ctx.stroke();
  }
}

function drawDimH(ctx, toScreen, x0, x1, y, label) {
  const [a0, ay] = toScreen(x0, y);
  const [a1] = toScreen(x1, y);
  ctx.strokeStyle = "#333";
  ctx.fillStyle = "#333";
  ctx.beginPath();
  ctx.moveTo(a0, ay + 8);
  ctx.lineTo(a0, ay + 14);
  ctx.moveTo(a1, ay + 8);
  ctx.lineTo(a1, ay + 14);
  ctx.moveTo(a0, ay + 11);
  ctx.lineTo(a1, ay + 11);
  ctx.stroke();
  ctx.font = "11px sans-serif";
  ctx.textAlign = "center";
  ctx.fillText(label, (a0 + a1) / 2, ay + 26);
  ctx.textAlign = "left";
}

function drawDimV(ctx, toScreen, y0, y1, x, label) {
  const [ax, a0] = toScreen(x, y0);
  const [, a1] = toScreen(x, y1);
  ctx.strokeStyle = "#333";
  ctx.fillStyle = "#333";
  ctx.beginPath();
  ctx.moveTo(ax + 8, a0);
  ctx.lineTo(ax + 14, a0);
  ctx.moveTo(ax + 8, a1);
  ctx.lineTo(ax + 14, a1);
  ctx.moveTo(ax + 11, a0);
  ctx.lineTo(ax + 11, a1);
  ctx.stroke();
  ctx.save();
  ctx.translate(ax + 22, (a0 + a1) / 2);
  ctx.rotate(-Math.PI / 2);
  ctx.font = "11px sans-serif";
  ctx.textAlign = "center";
  ctx.fillText(label, 0, 0);
  ctx.restore();
}

function drawHandle(ctx, sx, sy, fill = "#06c") {
  ctx.fillStyle = fill;
  ctx.strokeStyle = "#fff";
  ctx.lineWidth = 1;
  ctx.fillRect(sx - HANDLE / 2, sy - HANDLE / 2, HANDLE, HANDLE);
  ctx.strokeRect(sx - HANDLE / 2, sy - HANDLE / 2, HANDLE, HANDLE);
}

function resizeHandles(box) {
  const mx = (box.minX + box.maxX) / 2;
  const my = (box.minY + box.maxY) / 2;
  return [
    { id: "e", x: box.maxX, y: my },
    { id: "w", x: box.minX, y: my },
    { id: "n", x: mx, y: box.maxY },
    { id: "s", x: mx, y: box.minY },
  ];
}

function pointInPoly(x, y, pts) {
  let inside = false;
  for (let i = 0, j = pts.length - 1; i < pts.length; j = i++) {
    const xi = pts[i][0];
    const yi = pts[i][1];
    const xj = pts[j][0];
    const yj = pts[j][1];
    const hit = yi > y !== yj > y && x < ((xj - xi) * (y - yi)) / (yj - yi + 1e-12) + xi;
    if (hit) inside = !inside;
  }
  return inside;
}

function nearScreen(sx, sy, tx, ty, tol = HANDLE + 2) {
  return Math.hypot(sx - tx, sy - ty) <= tol;
}

/** Scale panel content from old bbox into new axis-aligned bounds. */
export function resizeFromEdges(panel, { minX, minY, maxX, maxY }) {
  const box = panelBbox(panel);
  const w0 = Math.max(box.width, 1e-6);
  const h0 = Math.max(box.height, 1e-6);
  const w1 = Math.max(maxX - minX, 10);
  const h1 = Math.max(maxY - minY, 10);
  const mapped = (pts) =>
    (pts || []).map((p) => {
      const u = (num(p[0]) - box.minX) / w0;
      const v = (num(p[1]) - box.minY) / h0;
      return [minX + u * w1, minY + v * h1];
    });
  const next = structuredClone(panel);
  next.outline = {
    points: [
      [minX, minY],
      [maxX, minY],
      [maxX, maxY],
      [minX, maxY],
    ],
    edges: [
      { type: "line", x0: minX, y0: minY, x1: maxX, y1: minY },
      { type: "line", x0: maxX, y0: minY, x1: maxX, y1: maxY },
      { type: "line", x0: maxX, y0: maxY, x1: minX, y1: maxY },
      { type: "line", x0: minX, y0: maxY, x1: minX, y1: minY },
    ],
  };
  next.features = (panel.features || []).map((f) => {
    if (f.kind === "holeVertical") {
      const [[x, y]] = mapped([[f.x, f.y]]);
      return { ...f, x, y };
    }
    if (f.kind === "grooveVertical") {
      return { ...f, path: mapped(f.path) };
    }
    return { ...f };
  });
  next.holes = [];
  for (const f of next.features) {
    if (f.kind !== "holeVertical") continue;
    const r = num(f.diameterMm, 8) / 2;
    const ring = [];
    for (let i = 0; i < 16; i++) {
      const a = (i / 16) * Math.PI * 2;
      ring.push([f.x + Math.cos(a) * r, f.y + Math.sin(a) * r]);
    }
    next.holes.push({
      id: f.id,
      points: [...ring, [ring[0][0], ring[0][1]]],
      source: "holeVertical",
    });
  }
  return next;
}

export function drawGeomPanel(canvas, panel, opts = {}) {
  const toolpath = opts.toolpathPoints;
  const ctx = canvas.getContext("2d");
  const dpr = window.devicePixelRatio || 1;
  const view = geomView(canvas, panel, toolpath);
  const { cssW, cssH, toScreen } = view;
  const panelBox = panelBbox(panel);
  canvas.width = Math.floor(cssW * dpr);
  canvas.height = Math.floor(cssH * dpr);
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  ctx.clearRect(0, 0, cssW, cssH);
  ctx.fillStyle = "#f4f4f4";
  ctx.fillRect(0, 0, cssW, cssH);

  drawGrid(ctx, view, 50);

  const pts = panel?.outline?.points || [];
  if (pts.length >= 3) {
    ctx.beginPath();
    pts.forEach((p, i) => {
      const [x, y] = toScreen(p[0], p[1]);
      if (i === 0) ctx.moveTo(x, y);
      else ctx.lineTo(x, y);
    });
    ctx.closePath();
    ctx.fillStyle = "#e8f0ff";
    ctx.strokeStyle = "#06c";
    ctx.lineWidth = 2;
    ctx.fill();
    ctx.stroke();
  }

  if (Array.isArray(toolpath) && toolpath.length >= 3) {
    ctx.beginPath();
    toolpath.forEach((p, i) => {
      const [x, y] = toScreen(num(p[0]), num(p[1]));
      if (i === 0) ctx.moveTo(x, y);
      else ctx.lineTo(x, y);
    });
    ctx.closePath();
    ctx.setLineDash([6, 4]);
    ctx.strokeStyle = "#a60";
    ctx.lineWidth = 1.5;
    ctx.stroke();
    ctx.setLineDash([]);
  }

  for (const h of panel?.holes || []) {
    const hp = h.points || [];
    if (hp.length < 3) continue;
    ctx.beginPath();
    hp.forEach((p, i) => {
      const [x, y] = toScreen(p[0], p[1]);
      if (i === 0) ctx.moveTo(x, y);
      else ctx.lineTo(x, y);
    });
    ctx.closePath();
    ctx.fillStyle = "#fff";
    ctx.strokeStyle = "#00c";
    ctx.lineWidth = 1;
    ctx.fill();
    ctx.stroke();
  }

  for (const f of panel?.features || []) {
    if (f.kind === "holeVertical") {
      const [cx, cy] = toScreen(num(f.x), num(f.y));
      const r = Math.max(3, (num(f.diameterMm, 8) / 2) * view.scale);
      ctx.beginPath();
      ctx.arc(cx, cy, r, 0, Math.PI * 2);
      ctx.strokeStyle = "#00c";
      ctx.lineWidth = 2;
      ctx.stroke();
      drawHandle(ctx, cx, cy, "#00c");
      ctx.fillStyle = "#00c";
      ctx.font = "10px sans-serif";
      ctx.fillText(`⌀${fmtMm(f.diameterMm)}`, cx + r + 4, cy);
    } else if (f.kind === "grooveVertical" && Array.isArray(f.path)) {
      const path = f.path.map((p) => toScreen(num(p[0]), num(p[1])));
      if (path.length >= 2) {
        ctx.beginPath();
        path.forEach(([x, y], i) => (i === 0 ? ctx.moveTo(x, y) : ctx.lineTo(x, y)));
        ctx.strokeStyle = "#c00";
        ctx.lineWidth = Math.max(2, num(f.widthMm, 6) * view.scale * 0.3);
        ctx.stroke();
        path.forEach(([x, y]) => drawHandle(ctx, x, y, "#c00"));
      }
    }
  }

  drawDimH(ctx, toScreen, panelBox.minX, panelBox.maxX, panelBox.minY, `${fmtMm(panelBox.width)}`);
  drawDimV(ctx, toScreen, panelBox.minY, panelBox.maxY, panelBox.maxX, `${fmtMm(panelBox.height)}`);

  if (isAxisAlignedRect(panel)) {
    for (const h of resizeHandles(panelBox)) {
      const [sx, sy] = toScreen(h.x, h.y);
      drawHandle(ctx, sx, sy, "#333");
    }
  }

  ctx.fillStyle = "#333";
  ctx.font = "12px sans-serif";
  ctx.fillText(String(panel?.panelId || ""), 8, 16);
  const engine = opts.toolpathEngine ? ` · ${opts.toolpathEngine}` : "";
  ctx.fillText(
    opts.toolpathPoints ? `Geom · 刀路预览${engine}` : "Geom · panel-local mm",
    8,
    32
  );

  return view;
}

export function hitTestGeom(canvas, panel, cssX, cssY) {
  if (!panel) return null;
  const view = geomView(canvas, panel);
  const box = panelBbox(panel);

  if (isAxisAlignedRect(panel)) {
    for (const h of resizeHandles(box)) {
      const [sx, sy] = view.toScreen(h.x, h.y);
      if (nearScreen(cssX, cssY, sx, sy)) {
        return { type: "resize", edge: h.id };
      }
    }
  }

  for (const f of panel.features || []) {
    if (f.kind === "holeVertical") {
      const [sx, sy] = view.toScreen(num(f.x), num(f.y));
      // larger hit than handle — holes are easy to miss at high zoom-out
      if (nearScreen(cssX, cssY, sx, sy, Math.max(HANDLE + 10, 16))) {
        return { type: "hole", featureId: f.id };
      }
    } else if (f.kind === "grooveVertical" && Array.isArray(f.path)) {
      for (let i = 0; i < f.path.length; i++) {
        const [sx, sy] = view.toScreen(num(f.path[i][0]), num(f.path[i][1]));
        if (nearScreen(cssX, cssY, sx, sy, Math.max(HANDLE + 8, 14))) {
          return { type: "groovePoint", featureId: f.id, pointIndex: i };
        }
      }
    }
  }

  const [lx, ly] = view.toLocal(cssX, cssY);
  const pts = panel.outline?.points || [];
  if (pts.length >= 3 && pointInPoly(lx, ly, pts)) {
    return { type: "panel" };
  }
  return null;
}

export function applyGeomDrag(panel, drag, localX, localY) {
  if (!drag || !panel) return panel;
  const x = num(localX);
  const y = num(localY);
  if (drag.type === "hole") return moveHole(panel, drag.featureId, x, y);
  if (drag.type === "groovePoint") {
    return moveGroovePoint(panel, drag.featureId, drag.pointIndex, x, y);
  }
  if (drag.type === "resize") {
    const box = panelBbox(panel);
    let { minX, minY, maxX, maxY } = box;
    if (drag.edge === "e") maxX = Math.max(x, minX + 10);
    if (drag.edge === "w") minX = Math.min(x, maxX - 10);
    if (drag.edge === "n") maxY = Math.max(y, minY + 10);
    if (drag.edge === "s") minY = Math.min(y, maxY - 10);
    return resizeFromEdges(panel, { minX, minY, maxX, maxY });
  }
  return panel;
}
