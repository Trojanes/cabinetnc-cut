/** SVG → cut-package: rect / polygon / path + transform + <use href="#id">.
 * ponytail: no clipPath / nested viewBox — group transform only via element transform attrs.
 */

function num(v, d = 0) {
  const n = Number(v);
  return Number.isFinite(n) ? n : d;
}

/** SVG transform list → matrix {a,b,c,d,e,f} */
export function parseTransform(str) {
  let a = 1;
  let b = 0;
  let c = 0;
  let d = 1;
  let e = 0;
  let f = 0;
  const s = String(str || "").trim();
  if (!s) return { a, b, c, d, e, f };

  function mul(a2, b2, c2, d2, e2, f2) {
    const na = a * a2 + c * b2;
    const nb = b * a2 + d * b2;
    const nc = a * c2 + c * d2;
    const nd = b * c2 + d * d2;
    const ne = a * e2 + c * f2 + e;
    const nf = b * e2 + d * f2 + f;
    a = na;
    b = nb;
    c = nc;
    d = nd;
    e = ne;
    f = nf;
  }

  const re = /(matrix|translate|scale|rotate|skewX|skewY)\s*\(([^)]*)\)/gi;
  let m;
  while ((m = re.exec(s))) {
    const kind = m[1].toLowerCase();
    const args = m[2]
      .trim()
      .split(/[\s,]+/)
      .map(Number)
      .filter((v) => Number.isFinite(v));
    if (kind === "matrix" && args.length >= 6) {
      mul(args[0], args[1], args[2], args[3], args[4], args[5]);
    } else if (kind === "translate") {
      mul(1, 0, 0, 1, args[0] || 0, args[1] || 0);
    } else if (kind === "scale") {
      const sx = args[0] ?? 1;
      const sy = args[1] ?? sx;
      mul(sx, 0, 0, sy, 0, 0);
    } else if (kind === "rotate") {
      const ang = ((args[0] || 0) * Math.PI) / 180;
      const cos = Math.cos(ang);
      const sin = Math.sin(ang);
      const cx = args[1] || 0;
      const cy = args[2] || 0;
      if (cx || cy) mul(1, 0, 0, 1, cx, cy);
      mul(cos, sin, -sin, cos, 0, 0);
      if (cx || cy) mul(1, 0, 0, 1, -cx, -cy);
    } else if (kind === "skewx") {
      mul(1, 0, Math.tan(((args[0] || 0) * Math.PI) / 180), 1, 0, 0);
    } else if (kind === "skewy") {
      mul(1, Math.tan(((args[0] || 0) * Math.PI) / 180), 0, 1, 0, 0);
    }
  }
  return { a, b, c, d, e, f };
}

export function applyMatrix(pts, m) {
  if (!m) return pts;
  return (pts || []).map(([x, y]) => [m.a * x + m.c * y + m.e, m.b * x + m.d * y + m.f]);
}

export function multiplyMatrix(m1, m2) {
  return {
    a: m1.a * m2.a + m1.c * m2.b,
    b: m1.b * m2.a + m1.d * m2.b,
    c: m1.a * m2.c + m1.c * m2.d,
    d: m1.b * m2.c + m1.d * m2.d,
    e: m1.a * m2.e + m1.c * m2.f + m1.e,
    f: m1.b * m2.e + m1.d * m2.f + m1.f,
  };
}

function identity() {
  return { a: 1, b: 0, c: 0, d: 1, e: 0, f: 0 };
}

function panelFromPoints(pts, id, opts) {
  if (!pts || pts.length < 3) return null;
  let ring = pts.map((p) => [num(p[0]), num(p[1])]);
  const a0 = ring[0];
  const b0 = ring[ring.length - 1];
  if (Math.hypot(a0[0] - b0[0], a0[1] - b0[1]) < 1e-6) ring = ring.slice(0, -1);
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

export function pathDToPolylines(d) {
  const tokens = String(d || "")
    .replace(/,/g, " ")
    .replace(/([MmLlHhVvCcSsQqTtAaZz])/g, " $1 ")
    .trim()
    .split(/\s+/)
    .filter(Boolean);
  const polys = [];
  let cur = [];
  let x = 0;
  let y = 0;
  let sx = 0;
  let sy = 0;
  let i = 0;
  let cmd = "L";

  function flush() {
    if (cur.length >= 3) polys.push(cur);
    cur = [];
  }
  function take() {
    return num(tokens[i++]);
  }

  while (i < tokens.length) {
    const t = tokens[i];
    if (/^[MmLlHhVvCcSsQqTtAaZz]$/.test(t)) {
      cmd = t;
      i += 1;
      if (cmd === "Z" || cmd === "z") {
        if (cur.length) {
          cur.push([sx, sy]);
          flush();
        }
        x = sx;
        y = sy;
        continue;
      }
    }
    const rel = cmd === cmd.toLowerCase();
    const C = cmd.toUpperCase();
    if (C === "M") {
      flush();
      x = rel ? x + take() : take();
      y = rel ? y + take() : take();
      sx = x;
      sy = y;
      cur = [[x, y]];
      cmd = rel ? "l" : "L";
      continue;
    }
    if (C === "L") {
      x = rel ? x + take() : take();
      y = rel ? y + take() : take();
      cur.push([x, y]);
      continue;
    }
    if (C === "H") {
      x = rel ? x + take() : take();
      cur.push([x, y]);
      continue;
    }
    if (C === "V") {
      y = rel ? y + take() : take();
      cur.push([x, y]);
      continue;
    }
    if (C === "C") {
      const x1 = rel ? x + take() : take();
      const y1 = rel ? y + take() : take();
      const x2 = rel ? x + take() : take();
      const y2 = rel ? y + take() : take();
      const nx = rel ? x + take() : take();
      const ny = rel ? y + take() : take();
      for (let s = 1; s <= 8; s++) {
        const tt = s / 8;
        const u = 1 - tt;
        cur.push([
          u * u * u * x + 3 * u * u * tt * x1 + 3 * u * tt * tt * x2 + tt * tt * tt * nx,
          u * u * u * y + 3 * u * u * tt * y1 + 3 * u * tt * tt * y2 + tt * tt * tt * ny,
        ]);
      }
      x = nx;
      y = ny;
      continue;
    }
    if (C === "Q") {
      const x1 = rel ? x + take() : take();
      const y1 = rel ? y + take() : take();
      const nx = rel ? x + take() : take();
      const ny = rel ? y + take() : take();
      for (let s = 1; s <= 6; s++) {
        const tt = s / 6;
        const u = 1 - tt;
        cur.push([
          u * u * x + 2 * u * tt * x1 + tt * tt * nx,
          u * u * y + 2 * u * tt * y1 + tt * tt * ny,
        ]);
      }
      x = nx;
      y = ny;
      continue;
    }
    if (C === "A") {
      take();
      take();
      take();
      take();
      take();
      x = rel ? x + take() : take();
      y = rel ? y + take() : take();
      cur.push([x, y]);
      continue;
    }
    if (C === "S" || C === "T") {
      const skip = C === "S" ? 2 : 0;
      for (let k = 0; k < skip; k++) take();
      x = rel ? x + take() : take();
      y = rel ? y + take() : take();
      cur.push([x, y]);
      continue;
    }
    i += 1;
  }
  flush();
  return polys;
}

function extractAttrs(tag) {
  const attrs = {};
  const re = /([a-zA-Z_:][\w:.-]*)\s*=\s*("([^"]*)"|'([^']*)')/g;
  let m;
  while ((m = re.exec(tag))) {
    attrs[m[1]] = m[3] ?? m[4] ?? "";
  }
  return attrs;
}

function ringsFromElement(tagName, attrs) {
  const name = tagName.toLowerCase();
  if (name === "rect") {
    const x = num(attrs.x);
    const y = num(attrs.y);
    const w = num(attrs.width);
    const h = num(attrs.height);
    if (w <= 0 || h <= 0) return [];
    return [
      [
        [x, y],
        [x + w, y],
        [x + w, y + h],
        [x, y + h],
      ],
    ];
  }
  if (name === "polygon") {
    const nums = String(attrs.points || "")
      .trim()
      .split(/[\s,]+/)
      .map(Number)
      .filter((v) => Number.isFinite(v));
    const pts = [];
    for (let i = 0; i + 1 < nums.length; i += 2) pts.push([nums[i], nums[i + 1]]);
    return pts.length >= 3 ? [pts] : [];
  }
  if (name === "path") return pathDToPolylines(attrs.d || "");
  return [];
}

export function svgToCutPackage(text, opts = {}) {
  const src = String(text || "");
  const byId = new Map();
  const emitted = [];

  // Pass 1: defs by id (local rings, no parent transform)
  const shapeRe = /<(rect|polygon|path)\b([^>]*)\/?>/gi;
  let m;
  while ((m = shapeRe.exec(src))) {
    const tag = m[1].toLowerCase();
    const attrs = extractAttrs(`<x ${m[2]}>`);
    const rings = ringsFromElement(tag, attrs);
    if (!rings.length) continue;
    const local = parseTransform(attrs.transform || "");
    if (attrs.id) byId.set(attrs.id, { rings, transform: local });
  }

  // Pass 2: emit direct shapes
  shapeRe.lastIndex = 0;
  while ((m = shapeRe.exec(src))) {
    const tag = m[1].toLowerCase();
    const attrs = extractAttrs(`<x ${m[2]}>`);
    const rings = ringsFromElement(tag, attrs);
    const local = parseTransform(attrs.transform || "");
    for (const ring of rings) emitted.push(applyMatrix(ring, local));
  }

  // Pass 3: <use href="#id">
  const useRe = /<use\b([^>]*)\/?>/gi;
  while ((m = useRe.exec(src))) {
    const attrs = extractAttrs(`<x ${m[1]}>`);
    const href = attrs.href || attrs["xlink:href"] || "";
    const id = href.replace(/^#/, "");
    const def = byId.get(id);
    if (!def) continue;
    const local = parseTransform(attrs.transform || "");
    const placed = multiplyMatrix(local, {
      a: 1,
      b: 0,
      c: 0,
      d: 1,
      e: num(attrs.x),
      f: num(attrs.y),
    });
    const combined = multiplyMatrix(placed, def.transform || identity());
    for (const ring of def.rings) {
      emitted.push(applyMatrix(ring, combined));
    }
  }

  const panels = [];
  let n = 0;
  for (const ring of emitted) {
    n += 1;
    const p = panelFromPoints(ring, `SVG${n}`, opts);
    if (p) panels.push(p);
  }

  if (!panels.length) {
    return { ok: false, error: "SVG: no <rect>/<polygon>/<path>/<use> outlines found" };
  }

  return {
    ok: true,
    package: {
      schema: "cabinetnc.cut-package",
      schemaVersion: 1,
      source: {
        app: "CabinetNC Cut",
        designName: opts.designName || "SVG import",
        exportId: `svg_${Date.now()}`,
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
    },
  };
}
