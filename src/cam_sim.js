/** CAM toolpath playhead helpers (MakerHub-depth M4). */

export function simOpsList(ops) {
  return (ops || []).filter((o) => o && o.placed);
}

export function clampSimIndex(index, count) {
  const n = Math.max(0, Number(count) || 0);
  if (n <= 0) return 0;
  let i = Number(index) || 0;
  i = ((i % n) + n) % n;
  return i;
}

export function simStep(index, count, delta) {
  return clampSimIndex((Number(index) || 0) + (Number(delta) || 0), count);
}

export function describeSimOp(op, index, count) {
  if (!op) return `— / ${count}`;
  const tag =
    op.op === "drill"
      ? `drill ${op.panelId} ⌀${op.diameterMm || "?"}`
      : op.op === "groove"
        ? `groove ${op.panelId}`
        : `contour ${op.panelId}`;
  return `${(Number(index) || 0) + 1}/${count} · ${tag}`;
}

/** Flatten placed ops into point-level frames for path animation. */
export function expandSimFrames(ops) {
  const frames = [];
  const list = simOpsList(ops);
  list.forEach((op, opIndex) => {
    if (op.op === "drill" && op.sheetX != null) {
      frames.push({
        opIndex,
        pointIndex: 0,
        x: Number(op.sheetX) || 0,
        y: Number(op.sheetY) || 0,
        op,
        kind: "drill",
      });
      return;
    }
    const path = Array.isArray(op.path) ? op.path : [];
    if (path.length < 1) return;
    const closed = op.op === "contour" && path.length >= 3;
    const n = closed ? path.length + 1 : path.length;
    for (let i = 0; i < n; i++) {
      const pt = path[i % path.length];
      frames.push({
        opIndex,
        pointIndex: i,
        x: Number(pt[0]) || 0,
        y: Number(pt[1]) || 0,
        op,
        kind: op.op === "groove" ? "groove" : "contour",
      });
    }
  });
  return frames;
}

export function describeSimFrame(frame, index, count) {
  if (!frame) return `— / ${count}`;
  const op = frame.op;
  const tag =
    frame.kind === "drill"
      ? `drill ${op.panelId}`
      : `${frame.kind} ${op.panelId} pt${frame.pointIndex}`;
  return `${(Number(index) || 0) + 1}/${count} · ${tag} @(${Math.round(frame.x)},${Math.round(frame.y)})`;
}
