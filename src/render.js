/** Canvas renderer — nest view + hit-test + dimension labels. */

import { placementAabb, aabbsConflict, findNestCollisions } from "./pack.js";

function rotatePoint(x, y, deg) {
  const r = (deg * Math.PI) / 180;
  const c = Math.cos(r);
  const s = Math.sin(r);
  return [x * c - y * s, x * s + y * c];
}

function transformOutline(points, offsetX, offsetY, rotationDeg) {
  return (points || []).map(([x, y]) => {
    const [rx, ry] = rotatePoint(Number(x) || 0, Number(y) || 0, rotationDeg || 0);
    return [rx + (offsetX || 0), ry + (offsetY || 0)];
  });
}

function nestView(canvas, sheet) {
  const cssW = canvas.clientWidth || 800;
  const cssH = canvas.clientHeight || 600;
  const sheetW = Math.max(1, Number(sheet.widthMm) || 1220);
  const sheetH = Math.max(1, Number(sheet.lengthMm) || 2440);
  const pad = 28;
  const scale = Math.min((cssW - pad * 2) / sheetW, (cssH - pad * 2) / sheetH);
  const ox = (cssW - sheetW * scale) / 2;
  const oy = (cssH - sheetH * scale) / 2;
  const toScreen = (x, y) => [ox + x * scale, oy + (sheetH - y) * scale];
  const toSheet = (sx, sy) => [(sx - ox) / scale, sheetH - (sy - oy) / scale];
  return { cssW, cssH, sheetW, sheetH, scale, ox, oy, pad, toScreen, toSheet };
}

export { nestView };

function drawGrid(ctx, toScreen, sheetW, sheetH, scale, step = 50) {
  const major = step * 2;
  ctx.save();
  for (let x = 0; x <= sheetW + 0.01; x += step) {
    const [sx0, sy0] = toScreen(x, 0);
    const [, sy1] = toScreen(x, sheetH);
    ctx.beginPath();
    ctx.moveTo(sx0, sy0);
    ctx.lineTo(sx0, sy1);
    const isMajor = Math.abs(x % major) < 0.01 || Math.abs(x) < 0.01;
    ctx.strokeStyle = isMajor ? "#ccc" : "#e8e8e8";
    ctx.lineWidth = 1;
    ctx.stroke();
  }
  for (let y = 0; y <= sheetH + 0.01; y += step) {
    const [sx0, sy0] = toScreen(0, y);
    const [sx1] = toScreen(sheetW, y);
    ctx.beginPath();
    ctx.moveTo(sx0, sy0);
    ctx.lineTo(sx1, sy0);
    const isMajor = Math.abs(y % major) < 0.01 || Math.abs(y) < 0.01;
    ctx.strokeStyle = isMajor ? "#ccc" : "#e8e8e8";
    ctx.lineWidth = 1;
    ctx.stroke();
  }
  ctx.restore();
}

/**
 * Keep panel bbox inside sheet (optional border inset).
 * @returns {{ offsetX: number, offsetY: number }}
 */
export function clampPlacementOnSheet(panel, place, sheetW, sheetH, borderMm = 0) {
  const border = Math.max(0, Number(borderMm) || 0);
  const pts = transformOutline(
    panel?.outline?.points,
    0,
    0,
    Number(place.rotationDeg) || 0
  );
  if (!pts.length) {
    return { offsetX: Number(place.offsetX) || 0, offsetY: Number(place.offsetY) || 0 };
  }
  const local = outlineBbox(pts);
  let ox = Number(place.offsetX) || 0;
  let oy = Number(place.offsetY) || 0;
  const minX = local.minX + ox;
  const minY = local.minY + oy;
  const maxX = local.maxX + ox;
  const maxY = local.maxY + oy;
  if (minX < border) ox += border - minX;
  if (minY < border) oy += border - minY;
  if (maxX > sheetW - border) ox -= maxX - (sheetW - border);
  if (maxY > sheetH - border) oy -= maxY - (sheetH - border);
  return {
    offsetX: Math.round(ox * 10) / 10,
    offsetY: Math.round(oy * 10) / 10,
  };
}

/**
 * Clamp to sheet+border; if AABB conflicts with other placements (spacing),
 * return fallback offsets (last good / original). Pure — wire from main drag.
 */
export function resolveNestPlacement({
  panel,
  place,
  panelId,
  otherPlacements,
  panelsById,
  sheetW,
  sheetH,
  spacingMm = 12,
  borderMm = 15,
  fallback,
}) {
  const clamped = clampPlacementOnSheet(panel, place, sheetW, sheetH, borderMm);
  const candidate = {
    panelId: panelId || place?.panelId,
    offsetX: clamped.offsetX,
    offsetY: clamped.offsetY,
    rotationDeg: Number(place?.rotationDeg) || 0,
    sheetIndex: Number(place?.sheetIndex) || 0,
  };
  const box = placementAabb(panel, candidate);
  const gap = Number(spacingMm) || 0;
  const id = String(panelId || place?.panelId || "");
  for (const op of otherPlacements || []) {
    if (String(op.panelId) === id) continue;
    if (Number(op.sheetIndex || 0) !== Number(candidate.sheetIndex || 0)) continue;
    const other = panelsById?.get?.(String(op.panelId));
    if (!other) continue;
    if (aabbsConflict(box, placementAabb(other, op), gap)) {
      const fb = fallback || place;
      return {
        offsetX: Number(fb?.offsetX) || 0,
        offsetY: Number(fb?.offsetY) || 0,
        blocked: true,
      };
    }
  }
  return { ...clamped, blocked: false };
}

export function snapMm(v, step = 10) {
  const s = Number(step) || 10;
  return Math.round(Number(v) / s) * s;
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

function outlineBbox(pts) {
  const xs = pts.map((p) => p[0]);
  const ys = pts.map((p) => p[1]);
  return {
    minX: Math.min(...xs),
    minY: Math.min(...ys),
    maxX: Math.max(...xs),
    maxY: Math.max(...ys),
    width: Math.max(...xs) - Math.min(...xs),
    height: Math.max(...ys) - Math.min(...ys),
  };
}

function drawDimH(ctx, toScreen, x0, x1, y, label, outward = -1) {
  const [a0, ay] = toScreen(x0, y);
  const [a1] = toScreen(x1, y);
  const midX = (a0 + a1) / 2;
  const tick = 6 * outward;
  ctx.strokeStyle = "#333";
  ctx.fillStyle = "#333";
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.moveTo(a0, ay);
  ctx.lineTo(a0, ay + tick);
  ctx.moveTo(a1, ay);
  ctx.lineTo(a1, ay + tick);
  ctx.moveTo(a0, ay + tick * 0.5);
  ctx.lineTo(a1, ay + tick * 0.5);
  ctx.stroke();
  ctx.font = "11px sans-serif";
  ctx.textAlign = "center";
  ctx.textBaseline = outward < 0 ? "bottom" : "top";
  ctx.fillText(label, midX, ay + tick + (outward < 0 ? -2 : 2));
  ctx.textAlign = "left";
  ctx.textBaseline = "alphabetic";
}

function drawDimV(ctx, toScreen, y0, y1, x, label, outward = -1) {
  const [ax, a0] = toScreen(x, y0);
  const [, a1] = toScreen(x, y1);
  const midY = (a0 + a1) / 2;
  const tick = 6 * outward;
  ctx.strokeStyle = "#333";
  ctx.fillStyle = "#333";
  ctx.lineWidth = 1;
  ctx.beginPath();
  ctx.moveTo(ax, a0);
  ctx.lineTo(ax + tick, a0);
  ctx.moveTo(ax, a1);
  ctx.lineTo(ax + tick, a1);
  ctx.moveTo(ax + tick * 0.5, a0);
  ctx.lineTo(ax + tick * 0.5, a1);
  ctx.stroke();
  ctx.save();
  ctx.translate(ax + tick + (outward < 0 ? -2 : 2), midY);
  ctx.rotate(-Math.PI / 2);
  ctx.font = "11px sans-serif";
  ctx.textAlign = "center";
  ctx.textBaseline = outward < 0 ? "bottom" : "top";
  ctx.fillText(label, 0, 0);
  ctx.restore();
}

function fmtMm(v) {
  const n = Number(v) || 0;
  return Math.abs(n - Math.round(n)) < 0.05 ? String(Math.round(n)) : n.toFixed(1);
}

export function drawNest({
  canvas,
  sheet,
  panelsById,
  placements,
  selectedPanelId,
  topPanelId,
  showAllDims = false,
  spacingMm = 12,
  opsOverlay = null,
  opsHighlightIndex = -1,
  opsToolhead = null,
}) {
  const ctx = canvas.getContext("2d");
  const dpr = window.devicePixelRatio || 1;
  const view = nestView(canvas, sheet);
  const { cssW, cssH, sheetW, sheetH, scale, toScreen } = view;
  canvas.width = Math.floor(cssW * dpr);
  canvas.height = Math.floor(cssH * dpr);
  ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
  ctx.clearRect(0, 0, cssW, cssH);

  const [sx0, sy0] = toScreen(0, sheetH);
  ctx.fillStyle = "#fff";
  ctx.strokeStyle = "#000";
  ctx.lineWidth = 1;
  ctx.fillRect(sx0, sy0, sheetW * scale, sheetH * scale);
  drawGrid(ctx, toScreen, sheetW, sheetH, scale, 50);
  ctx.strokeStyle = "#000";
  ctx.lineWidth = 1;
  ctx.strokeRect(sx0, sy0, sheetW * scale, sheetH * scale);

  // sheet outer dims
  drawDimH(ctx, toScreen, 0, sheetW, sheetH, `${fmtMm(sheetW)}`, -1);
  drawDimV(ctx, toScreen, 0, sheetH, 0, `${fmtMm(sheetH)}`, -1);

  const hitPolys = [];
  const panelList = [...(panelsById?.values?.() || [])];
  const conflictIds = new Set();
  for (const hit of findNestCollisions(panelList, placements, spacingMm)) {
    conflictIds.add(String(hit.panelIdA));
    conflictIds.add(String(hit.panelIdB));
  }

  // Draw selected / top panel last so it stacks above others while overlapping.
  const topId = topPanelId != null && topPanelId !== ""
    ? String(topPanelId)
    : selectedPanelId != null && selectedPanelId !== ""
      ? String(selectedPanelId)
      : null;
  const drawList = [...(placements || [])];
  if (topId) {
    drawList.sort((a, b) => {
      const at = String(a.panelId) === topId ? 1 : 0;
      const bt = String(b.panelId) === topId ? 1 : 0;
      return at - bt;
    });
  }

  for (const place of drawList) {
    const panel = panelsById.get(String(place.panelId));
    if (!panel) continue;
    const pts = transformOutline(
      panel.outline?.points,
      Number(place.offsetX) || 0,
      Number(place.offsetY) || 0,
      Number(place.rotationDeg) || 0
    );
    if (pts.length < 3) continue;
    hitPolys.push({ panelId: String(place.panelId), pts });

    const active = selectedPanelId && selectedPanelId === String(place.panelId);
    const conflict = conflictIds.has(String(place.panelId));
    ctx.beginPath();
    pts.forEach((p, i) => {
      const [x, y] = toScreen(p[0], p[1]);
      if (i === 0) ctx.moveTo(x, y);
      else ctx.lineTo(x, y);
    });
    ctx.closePath();
    ctx.fillStyle = conflict ? (active ? "#f5c6c6" : "#f0d0d0") : active ? "#cde" : "#eee";
    ctx.strokeStyle = conflict ? "#c00" : active ? "#06c" : "#000";
    ctx.lineWidth = active || conflict ? 2 : 1;
    ctx.fill();
    ctx.stroke();

    for (const feat of panel.features || []) {
      const kind = String(feat.kind || "");
      if (kind === "holeVertical") {
        const [hx, hy] = rotatePoint(
          Number(feat.x) || 0,
          Number(feat.y) || 0,
          Number(place.rotationDeg) || 0
        );
        const [cx, cy] = toScreen(
          hx + (Number(place.offsetX) || 0),
          hy + (Number(place.offsetY) || 0)
        );
        const r = Math.max(1.5, ((Number(feat.diameterMm) || 8) / 2) * scale);
        ctx.beginPath();
        ctx.arc(cx, cy, r, 0, Math.PI * 2);
        ctx.strokeStyle = "#00c";
        ctx.lineWidth = 1;
        ctx.stroke();
        if (active) {
          ctx.fillStyle = "#00c";
          ctx.font = "10px sans-serif";
          ctx.fillText(`⌀${fmtMm(feat.diameterMm)}`, cx + r + 2, cy);
        }
      } else if (kind === "grooveVertical" && Array.isArray(feat.path)) {
        const path = feat.path.map(([x, y]) => {
          const [rx, ry] = rotatePoint(
            Number(x) || 0,
            Number(y) || 0,
            Number(place.rotationDeg) || 0
          );
          return toScreen(
            rx + (Number(place.offsetX) || 0),
            ry + (Number(place.offsetY) || 0)
          );
        });
        if (path.length >= 2) {
          ctx.beginPath();
          path.forEach(([x, y], i) => (i === 0 ? ctx.moveTo(x, y) : ctx.lineTo(x, y)));
          ctx.strokeStyle = "#c00";
          ctx.lineWidth = Math.max(1, (Number(feat.widthMm) || 6) * scale * 0.3);
          ctx.stroke();
        }
      }
    }

    const box = outlineBbox(pts);
    const [lx, ly] = toScreen(box.minX, box.maxY);
    ctx.fillStyle = active ? "#06c" : "#000";
    ctx.font = active ? "bold 11px sans-serif" : "11px sans-serif";
    ctx.fillText(String(place.panelId), lx + 2, ly + 12);

    if (active || showAllDims) {
      drawDimH(
        ctx,
        toScreen,
        box.minX,
        box.maxX,
        box.minY,
        `${fmtMm(box.width)}`,
        1
      );
      drawDimV(
        ctx,
        toScreen,
        box.minY,
        box.maxY,
        box.maxX,
        `${fmtMm(box.height)}`,
        1
      );
    }
  }

  if (!(placements || []).length) {
    ctx.fillStyle = "#666";
    ctx.font = "12px sans-serif";
    ctx.fillText("no placements — click NewRect / Demo", sx0 + 8, sy0 + 20);
  }

  // CAM stage: sheet-space ops markers (contour path / drill / groove)
  if (Array.isArray(opsOverlay) && opsOverlay.length) {
    const hi = Number.isFinite(Number(opsHighlightIndex)) ? Number(opsHighlightIndex) : -1;
    opsOverlay.forEach((op, idx) => {
      const active = hi < 0 || idx === hi;
      const dim = hi >= 0 && !active;
      if (op.op === "contour" && Array.isArray(op.path) && op.path.length >= 2) {
        ctx.beginPath();
        op.path.forEach(([x, y], i) => {
          const [sx, sy] = toScreen(Number(x) || 0, Number(y) || 0);
          if (i === 0) ctx.moveTo(sx, sy);
          else ctx.lineTo(sx, sy);
        });
        const [sx0p, sy0p] = toScreen(Number(op.path[0][0]) || 0, Number(op.path[0][1]) || 0);
        ctx.lineTo(sx0p, sy0p);
        ctx.strokeStyle = active ? "#0a4" : "#2a6";
        ctx.lineWidth = active ? 2.5 : 1.5;
        ctx.globalAlpha = dim ? 0.25 : 1;
        ctx.setLineDash(active ? [] : [4, 3]);
        ctx.stroke();
        ctx.setLineDash([]);
        ctx.globalAlpha = 1;
      } else if (op.op === "drill" && op.sheetX != null) {
        const [cx, cy] = toScreen(Number(op.sheetX), Number(op.sheetY));
        const r = Math.max(2, ((Number(op.diameterMm) || 8) / 2) * scale);
        ctx.globalAlpha = dim ? 0.25 : 1;
        ctx.beginPath();
        ctx.arc(cx, cy, r, 0, Math.PI * 2);
        ctx.strokeStyle = active ? "#04f" : "#06c";
        ctx.lineWidth = active ? 2.5 : 1.5;
        ctx.stroke();
        ctx.beginPath();
        ctx.moveTo(cx - r, cy);
        ctx.lineTo(cx + r, cy);
        ctx.moveTo(cx, cy - r);
        ctx.lineTo(cx, cy + r);
        ctx.stroke();
        ctx.globalAlpha = 1;
      } else if (op.op === "groove" && Array.isArray(op.path) && op.path.length >= 2) {
        ctx.beginPath();
        op.path.forEach(([x, y], i) => {
          const [sx, sy] = toScreen(Number(x) || 0, Number(y) || 0);
          if (i === 0) ctx.moveTo(sx, sy);
          else ctx.lineTo(sx, sy);
        });
        ctx.globalAlpha = dim ? 0.25 : 1;
        ctx.strokeStyle = active ? "#e60" : "#c40";
        ctx.lineWidth = Math.max(active ? 2.5 : 1.5, (Number(op.widthMm) || 6) * scale * 0.25);
        ctx.stroke();
        ctx.globalAlpha = 1;
      }
    });
    if (opsToolhead && Number.isFinite(Number(opsToolhead.x)) && Number.isFinite(Number(opsToolhead.y))) {
      const [hx, hy] = toScreen(Number(opsToolhead.x), Number(opsToolhead.y));
      ctx.beginPath();
      ctx.arc(hx, hy, 6, 0, Math.PI * 2);
      ctx.fillStyle = "#c00";
      ctx.fill();
      ctx.strokeStyle = "#fff";
      ctx.lineWidth = 1.5;
      ctx.stroke();
      ctx.beginPath();
      ctx.moveTo(hx - 10, hy);
      ctx.lineTo(hx + 10, hy);
      ctx.moveTo(hx, hy - 10);
      ctx.lineTo(hx, hy + 10);
      ctx.strokeStyle = "#c00";
      ctx.lineWidth = 1;
      ctx.stroke();
    }
  }

  return { view, hitPolys };
}

/** Pick topmost panel under canvas CSS coords, or null. */
export function hitTestNest(canvas, sheet, panelsById, placements, cssX, cssY) {
  const { toSheet, sheetW, sheetH } = nestView(canvas, sheet);
  const [mx, my] = toSheet(cssX, cssY);
  if (mx < 0 || my < 0 || mx > sheetW || my > sheetH) return null;

  // reverse order: last drawn = top
  const list = [...(placements || [])].reverse();
  for (const place of list) {
    const panel = panelsById.get(String(place.panelId));
    if (!panel) continue;
    const pts = transformOutline(
      panel.outline?.points,
      Number(place.offsetX) || 0,
      Number(place.offsetY) || 0,
      Number(place.rotationDeg) || 0
    );
    if (pts.length >= 3 && pointInPoly(mx, my, pts)) {
      return String(place.panelId);
    }
  }
  return null;
}
