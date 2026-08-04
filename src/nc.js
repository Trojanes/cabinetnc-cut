/** NC from cut ops + machine profile.
 * ponytail: G0/G1/F/S only — no tool-change or arc; upgrade per dialect later.
 */

const OP_RANK = { contour: 0, drill: 1, groove: 2 };

function groupBySheet(items) {
  const bySheet = new Map();
  for (const item of items) {
    const idx = Number(item.sheetIndex) || 0;
    if (!bySheet.has(idx)) bySheet.set(idx, []);
    bySheet.get(idx).push(item);
  }
  return [...bySheet.keys()].sort((a, b) => a - b).map((idx) => ({
    idx,
    items: bySheet.get(idx),
  }));
}

function sortOps(items) {
  return [...items].sort((a, b) => {
    if (a.panelId !== b.panelId) return String(a.panelId).localeCompare(String(b.panelId));
    return (OP_RANK[a.op] ?? 9) - (OP_RANK[b.op] ?? 9);
  });
}

function fmt(n) {
  const v = Number(n) || 0;
  return Math.round(v * 1000) / 1000;
}

/** Positive depth levels from stepdown (last = full depth). stepdown<=0 → single pass. */
export function contourPassDepths(totalDepthMm, stepdownMm) {
  const total = Math.abs(Number(totalDepthMm) || 0);
  const step = Math.abs(Number(stepdownMm) || 0);
  if (total <= 0) return [];
  if (!(step > 0) || step >= total - 1e-9) return [total];
  const depths = [];
  for (let d = step; d < total - 1e-9; d += step) {
    depths.push(Math.round(d * 1000) / 1000);
  }
  depths.push(total);
  return depths;
}

function emitContour(lines, c, profile) {
  const path = c.path || [];
  if (path.length < 3) return;
  const safeZ = Number(profile.safeZMm) || 5;
  const total = Math.abs(Number(c.cutDepthMm) || Number(profile.contourDepthMm) || 18);
  const passes = contourPassDepths(total, profile.contourStepdownMm);
  const feed = Number(profile.feedXyMmMin) || 3000;
  const feedZ = Number(profile.feedZMmMin) || 500;
  lines.push(
    `(contour ${c.panelId}${c.toolOffsetMm ? ` offset=${c.toolOffsetMm}` : ""}${passes.length > 1 ? ` passes=${passes.length}` : ""})`
  );
  lines.push(`G0 X${fmt(path[0][0])} Y${fmt(path[0][1])}`);
  for (let p = 0; p < passes.length; p++) {
    const z = -passes[p];
    if (passes.length > 1) lines.push(`(pass ${p + 1}/${passes.length} Z${fmt(z)})`);
    lines.push(`G1 Z${fmt(z)} F${feedZ}`);
    for (let i = 1; i < path.length; i++) {
      lines.push(`G1 X${fmt(path[i][0])} Y${fmt(path[i][1])} F${feed}`);
    }
    lines.push(`G1 X${fmt(path[0][0])} Y${fmt(path[0][1])} F${feed}`);
    if (p < passes.length - 1) lines.push(`G0 Z${safeZ}`);
  }
  lines.push(`G0 Z${safeZ}`);
}

function emitDrill(lines, d, profile) {
  const safeZ = Number(profile.safeZMm) || 5;
  const total = Math.abs(Number(d.depthMm) || 0);
  const peck = Math.abs(Number(profile.drillPeckMm) || 0);
  const feedZ = Number(profile.feedZMmMin) || 500;
  lines.push(`(drill ${d.panelId} dia=${d.diameterMm}${peck > 0 && peck < total ? ` peck=${peck}` : ""})`);
  lines.push(`G0 X${fmt(d.sheetX)} Y${fmt(d.sheetY)}`);
  if (!(peck > 0) || peck >= total - 1e-9) {
    lines.push(`G1 Z${fmt(-total)} F${feedZ}`);
    lines.push(`G0 Z${safeZ}`);
    return;
  }
  for (let z = peck; z < total - 1e-9; z += peck) {
    lines.push(`G1 Z${fmt(-z)} F${feedZ}`);
    lines.push(`G0 Z${safeZ}`);
  }
  lines.push(`G1 Z${fmt(-total)} F${feedZ}`);
  lines.push(`G0 Z${safeZ}`);
}

function emitGroove(lines, g, profile) {
  const path = g.path || [];
  if (path.length < 2) return;
  const safeZ = Number(profile.safeZMm) || 5;
  const z = -(Math.abs(Number(g.depthMm) || 0));
  const feed = Number(profile.feedXyMmMin) || 3000;
  lines.push(`(groove ${g.panelId} w=${g.widthMm})`);
  lines.push(`G0 X${fmt(path[0][0])} Y${fmt(path[0][1])}`);
  lines.push(`G1 Z${fmt(z)} F${Number(profile.feedZMmMin) || 500}`);
  for (let i = 1; i < path.length; i++) {
    lines.push(`G1 X${fmt(path[i][0])} Y${fmt(path[i][1])} F${feed}`);
  }
  lines.push(`G0 Z${safeZ}`);
}

/**
 * @param {object[]} ops attached (placed) ops
 * @param {object} [profile] machine profile
 */
export function opsToNc(ops, profile = {}) {
  const safeZ = Number(profile.safeZMm) || 5;
  const rpm = Number(profile.spindleRpm) || 0;
  const list = (ops || []).filter((o) => o.placed);
  const contours = list.filter((o) => o.op === "contour" && Array.isArray(o.path) && o.path.length >= 3);
  const drills = list.filter((o) => o.op === "drill" && o.sheetX != null);
  const grooves = list.filter((o) => o.op === "groove" && Array.isArray(o.path) && o.path.length >= 2);
  const all = sortOps([...contours, ...drills, ...grooves]);

  const lines = [
    `(cabinetnc-cut nc · ${profile.id || "default"} · ${profile.name || ""} · ${profile.dialect || "generic"})`.trim(),
    `(wcs: sheet SW origin · X+ right · Y+ back · Z+ up · units mm)`,
  ];
  if (profile.originNote) {
    lines.push(`(origin: ${String(profile.originNote).replace(/[()]/g, "")})`);
  }
  lines.push("G21", "G90");
  if (profile.dialect === "fanuc_like") {
    lines.push("G17", "G40", "G49", "G80");
  }
  if (rpm > 0) lines.push(`S${Math.round(rpm)} M3`);
  lines.push(`G0 Z${safeZ}`);

  for (const { idx, items } of groupBySheet(all)) {
    lines.push(`(sheet ${idx + 1})`);
    for (const item of sortOps(items)) {
      if (item.op === "contour") emitContour(lines, item, profile);
      else if (item.op === "drill") emitDrill(lines, item, profile);
      else if (item.op === "groove") emitGroove(lines, item, profile);
    }
  }

  if (rpm > 0) lines.push("M5");
  const end = String(profile.programEnd || "M2").toUpperCase();
  lines.push(end === "M30" ? "M30" : "M2");
  return lines.join("\n") + "\n";
}

/** @deprecated use opsToNc */
export function drillsToSimpleNc(ops, opts = {}) {
  return opsToNc(ops, {
    id: "legacy_stub",
    name: "legacy stub",
    safeZMm: Number(opts.safeZ) || 5,
    feedXyMmMin: 3000,
    feedZMmMin: 500,
    spindleRpm: 0,
    toolDiameterMm: 0,
  });
}
