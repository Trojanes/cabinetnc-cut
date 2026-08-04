/** DXF → cut-package: LWPOLYLINE(+bulge), CIRCLE, ARC, BLOCK+INSERT explode.
 * ponytail: no nested INSERT depth>8, no ATTRIB/SPLINE — upgrade later.
 */

function num(v, d = 0) {
  const n = Number(v);
  return Number.isFinite(n) ? n : d;
}

export function expandBulge(p0, p1, bulge, segments = 8) {
  const b = Number(bulge) || 0;
  if (Math.abs(b) < 1e-12) return [[p1[0], p1[1]]];
  const x0 = p0[0];
  const y0 = p0[1];
  const x1 = p1[0];
  const y1 = p1[1];
  const dx = x1 - x0;
  const dy = y1 - y0;
  const ang = 4 * Math.atan(b);
  const chord = Math.hypot(dx, dy);
  if (chord < 1e-12) return [[x1, y1]];
  const radius = chord / (2 * Math.sin(ang / 2));
  const mx = (x0 + x1) / 2;
  const my = (y0 + y1) / 2;
  const nx = -dy / chord;
  const ny = dx / chord;
  const h = Math.sqrt(Math.max(0, radius * radius - (chord * 0.5) ** 2)) * Math.sign(b);
  const cx = mx + nx * h;
  const cy = my + ny * h;
  const a0 = Math.atan2(y0 - cy, x0 - cx);
  const n = Math.max(2, Math.round((segments * Math.abs(ang)) / Math.PI));
  const pts = [];
  for (let i = 1; i <= n; i++) {
    const t = i / n;
    const a = a0 + ang * t;
    pts.push([cx + radius * Math.cos(a), cy + radius * Math.sin(a)]);
  }
  pts[pts.length - 1] = [x1, y1];
  return pts;
}

function sampleArc(cx, cy, r, a0deg, a1deg, segments = 24) {
  let a0 = (num(a0deg) * Math.PI) / 180;
  let a1 = (num(a1deg) * Math.PI) / 180;
  while (a1 < a0) a1 += Math.PI * 2;
  const pts = [];
  const n = Math.max(4, segments);
  for (let i = 0; i <= n; i++) {
    const t = i / n;
    const a = a0 + (a1 - a0) * t;
    pts.push([cx + r * Math.cos(a), cy + r * Math.sin(a)]);
  }
  return pts;
}

function sampleCircle(cx, cy, r, segments = 32) {
  const pts = [];
  for (let i = 0; i < segments; i++) {
    const a = (i / segments) * Math.PI * 2;
    pts.push([cx + r * Math.cos(a), cy + r * Math.sin(a)]);
  }
  return pts;
}

function transformPoint(x, y, xf) {
  const sx = num(xf.sx, 1);
  const sy = num(xf.sy, 1);
  const rad = ((num(xf.rot) || 0) * Math.PI) / 180;
  const c = Math.cos(rad);
  const s = Math.sin(rad);
  const px = x * sx;
  const py = y * sy;
  return [num(xf.x) + px * c - py * s, num(xf.y) + px * s + py * c];
}

function transformRing(ring, xf) {
  return (ring || []).map(([x, y]) => transformPoint(x, y, xf));
}

function shapeToRing(sh) {
  if (sh.kind === "lwpoly") {
    const verts = sh.verts || [];
    if (verts.length < 2) return null;
    if (sh.closed && verts.length >= 2) {
      const ring = [[verts[0].x, verts[0].y]];
      for (let i = 0; i < verts.length; i++) {
        const a = verts[i];
        const b = verts[(i + 1) % verts.length];
        for (const p of expandBulge([a.x, a.y], [b.x, b.y], a.bulge || 0)) ring.push(p);
      }
      return ring;
    }
    if (verts.length >= 3) {
      const ring = [[verts[0].x, verts[0].y]];
      for (let i = 0; i < verts.length - 1; i++) {
        const a = verts[i];
        const b = verts[i + 1];
        for (const p of expandBulge([a.x, a.y], [b.x, b.y], a.bulge || 0)) ring.push(p);
      }
      return ring;
    }
    return null;
  }
  if (sh.kind === "circle" && sh.r > 0) return sampleCircle(sh.cx, sh.cy, sh.r);
  if (sh.kind === "arc" && sh.r > 0) {
    const span = (((num(sh.a1) - num(sh.a0)) % 360) + 360) % 360;
    if (span > 350) return sampleArc(sh.cx, sh.cy, sh.r, sh.a0, sh.a1);
  }
  return null;
}

function panelFromRing(pts, id, opts) {
  if (!pts || pts.length < 3) return null;
  let ring = pts.map((p) => [num(p[0]), num(p[1])]);
  const a = ring[0];
  const b = ring[ring.length - 1];
  if (Math.hypot(a[0] - b[0], a[1] - b[1]) < 1e-6) ring = ring.slice(0, -1);
  if (ring.length < 3) return null;
  const xs = ring.map((p) => p[0]);
  const ys = ring.map((p) => p[1]);
  const minX = Math.min(...xs);
  const minY = Math.min(...ys);
  const norm = ring.map(([x, y]) => [x - minX, y - minY]);
  const w = Math.max(...norm.map((p) => p[0]));
  const h = Math.max(...norm.map((p) => p[1]));
  return {
    panelId: id,
    name: id,
    material: opts.material || "imported",
    thicknessMm: Number(opts.thicknessMm) || 18,
    bbox: { widthMm: w, heightMm: h },
    rotatable: true,
    outline: { points: norm },
    features: [],
  };
}

function parsePairs(text) {
  const lines = String(text || "").split(/\r?\n/);
  const pairs = [];
  for (let i = 0; i + 1 < lines.length; i += 2) {
    pairs.push([String(lines[i]).trim(), lines[i + 1] ?? ""]);
  }
  return pairs;
}

function readEntityFields(entity, code, v) {
  if (entity.kind === "lwpoly") {
    if (code === "70") {
      if ((Number(v) || 0) & 1) entity.closed = true;
    } else if (code === "10") entity._x = num(v);
    else if (code === "20") entity.verts.push({ x: entity._x, y: num(v), bulge: 0 });
    else if (code === "42" && entity.verts.length) {
      entity.verts[entity.verts.length - 1].bulge = num(v);
    }
  } else if (entity.kind === "arc" || entity.kind === "circle") {
    if (code === "10") entity.cx = num(v);
    else if (code === "20") entity.cy = num(v);
    else if (code === "40") entity.r = num(v);
    else if (code === "50") entity.a0 = num(v);
    else if (code === "51") entity.a1 = num(v);
  } else if (entity.kind === "insert") {
    if (code === "2") entity.name = String(v).trim();
    else if (code === "10") entity.x = num(v);
    else if (code === "20") entity.y = num(v);
    else if (code === "41") entity.sx = num(v, 1);
    else if (code === "42") entity.sy = num(v, 1);
    else if (code === "50") entity.rot = num(v);
  }
}

function startEntity(type) {
  if (type === "LWPOLYLINE" || type === "POLYLINE") {
    return { kind: "lwpoly", closed: false, verts: [], _x: 0 };
  }
  if (type === "ARC") return { kind: "arc", cx: 0, cy: 0, r: 0, a0: 0, a1: 0 };
  if (type === "CIRCLE") return { kind: "circle", cx: 0, cy: 0, r: 0 };
  if (type === "INSERT") {
    return { kind: "insert", name: "", x: 0, y: 0, sx: 1, sy: 1, rot: 0 };
  }
  return null;
}

/** Collect BLOCK definitions and ENTITIES list (including INSERT). */
export function parseDxfStructure(text) {
  const pairs = parsePairs(text);
  const blocks = new Map(); // name → shapes[]
  const entities = [];
  let section = null;
  let entity = null;
  let blockName = null;
  let blockShapes = null;

  function pushEntity() {
    if (!entity) return;
    if (section === "BLOCKS" && blockShapes) blockShapes.push(entity);
    else if (section === "ENTITIES") entities.push(entity);
    entity = null;
  }

  for (let i = 0; i < pairs.length; i++) {
    const [code, val] = pairs[i];
    const v = String(val).trim();

    if (code === "0" && v === "SECTION") {
      pushEntity();
      const namePair = pairs[i + 1];
      section =
        namePair && namePair[0] === "2" ? String(namePair[1]).trim().toUpperCase() : null;
      continue;
    }
    if (code === "0" && v === "ENDSEC") {
      pushEntity();
      if (section === "BLOCKS" && blockName && blockShapes) {
        blocks.set(blockName, blockShapes);
        blockName = null;
        blockShapes = null;
      }
      section = null;
      continue;
    }

    if (section === "BLOCKS") {
      if (code === "0" && v === "BLOCK") {
        pushEntity();
        if (blockName && blockShapes) blocks.set(blockName, blockShapes);
        blockName = null;
        blockShapes = [];
        continue;
      }
      if (code === "0" && v === "ENDBLK") {
        pushEntity();
        if (blockName && blockShapes) blocks.set(blockName, blockShapes);
        blockName = null;
        blockShapes = null;
        continue;
      }
      if (blockShapes && !blockName && code === "2") {
        blockName = v;
        continue;
      }
    }

    if (section !== "BLOCKS" && section !== "ENTITIES") continue;

    if (code === "0") {
      pushEntity();
      entity = startEntity(v);
      continue;
    }
    if (entity) readEntityFields(entity, code, v);
  }
  pushEntity();
  return { blocks, entities };
}

function explodeShapes(shapes, blocks, xf = { x: 0, y: 0, sx: 1, sy: 1, rot: 0 }, depth = 0) {
  const out = [];
  if (depth > 8) return out;
  for (const sh of shapes || []) {
    if (sh.kind === "insert") {
      const def = blocks.get(sh.name);
      if (!def) continue;
      const local = {
        x: num(sh.x),
        y: num(sh.y),
        sx: num(sh.sx, 1),
        sy: num(sh.sy, 1),
        rot: num(sh.rot),
      };
      const nested = explodeShapes(def, blocks, { x: 0, y: 0, sx: 1, sy: 1, rot: 0 }, depth + 1);
      for (const ring of nested) {
        let pts = transformRing(ring, local);
        pts = transformRing(pts, xf);
        out.push(pts);
      }
      continue;
    }
    const ring = shapeToRing(sh);
    if (!ring) continue;
    out.push(transformRing(ring, xf));
  }
  return out;
}

export function dxfToCutPackage(text, opts = {}) {
  const { blocks, entities } = parseDxfStructure(text);
  let skippedInsert = 0;
  for (const e of entities) {
    if (e.kind === "insert" && !blocks.has(e.name)) skippedInsert += 1;
  }

  const rings = explodeShapes(entities, blocks);
  const panels = [];
  let n = 0;
  for (const ring of rings) {
    if (!ring || ring.length < 3) continue;
    n += 1;
    const p = panelFromRing(ring, `DXF${n}`, opts);
    if (p) panels.push(p);
  }

  if (!panels.length) {
    const extra = skippedInsert ? ` (unknown INSERT×${skippedInsert})` : "";
    return { ok: false, error: `DXF: no closed geometry${extra}` };
  }

  const pkg = {
    schema: "cabinetnc.cut-package",
    schemaVersion: 1,
    source: {
      app: "CabinetNC Cut",
      designName: opts.designName || "DXF import",
      exportId: `dxf_${Date.now()}`,
    },
    units: "mm",
    sheets: [
      {
        sheetId: "S1",
        material: opts.material || "imported",
        thicknessMm: Number(opts.thicknessMm) || 18,
        widthMm: 1220,
        lengthMm: 2440,
      },
    ],
    panels,
  };
  if (skippedInsert) pkg.warnings = [`DXF unknown INSERT×${skippedInsert}`];
  if (blocks.size) pkg.source.blocks = blocks.size;
  return { ok: true, package: pkg };
}
