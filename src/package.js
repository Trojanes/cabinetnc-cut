/** Validate / normalize cabinetnc packages.

Primary on-disk: cabinetnc.woodjob (multi-file) → flattened via woodjobToCutPackage.
Legacy: cabinetnc.cut-package single JSON.
*/

export const SCHEMA = "cabinetnc.cut-package";
export const SCHEMA_VERSION = 1;
export const WOODJOB_FORMAT = "cabinetnc.woodjob";
export const WOODJOB_SCHEMA_VERSION = 2;

export function mapFeatureKind(featureType) {
  switch (featureType) {
    case "drill":
      return "holeVertical";
    case "groove":
      return "grooveVertical";
    case "throughCutout":
      return "throughCutout";
    case "pocket":
      return "pocket";
    default:
      return featureType || "unknown";
  }
}

/** Assemble woodjob file map { "manifest.json": obj, ... } → cut-package shape. */
export function woodjobToCutPackage(files) {
  const errors = [];
  const warnings = [];
  const manifest = files?.["manifest.json"] || files?.manifest;
  if (!manifest || typeof manifest !== "object") {
    return {
      ok: false,
      errors: [{ code: "manifest", path: "manifest.json", msg: "missing manifest.json" }],
      warnings,
      package: null,
    };
  }
  if (manifest.format !== WOODJOB_FORMAT) {
    errors.push({
      code: "format",
      path: "manifest.format",
      msg: `expected "${WOODJOB_FORMAT}" (got ${JSON.stringify(manifest.format)})`,
    });
  }
  if (manifest.encryption?.mode && manifest.encryption.mode !== "none") {
    errors.push({
      code: "encryption",
      path: "manifest.encryption.mode",
      msg: `encrypted woodjob not supported (mode=${manifest.encryption.mode})`,
    });
  }
  if (Number(manifest.schemaVersion) !== WOODJOB_SCHEMA_VERSION) {
    warnings.push({
      code: "schemaVersion",
      path: "manifest.schemaVersion",
      msg: `schemaVersion ${manifest.schemaVersion} — viewer targets v${WOODJOB_SCHEMA_VERSION}`,
    });
  }

  const matThickness = new Map();
  for (const m of files?.["materials.json"]?.materials || []) {
    if (m?.materialId) matThickness.set(String(m.materialId), Number(m.thicknessMm) || 0);
  }

  const sheets = (files?.["sheets.json"]?.sheetTypes || files?.["sheets.json"]?.sheets || []).map((s, i) => ({
    sheetId: s.sheetId || `S${i}`,
    material: s.materialId || s.material,
    thicknessMm: matThickness.get(String(s.materialId)) || Number(s.thicknessMm) || 0,
    widthMm: Number(s.widthMm) || 0,
    lengthMm: Number(s.heightMm) || Number(s.lengthMm) || 0,
    heightMm: Number(s.heightMm) || Number(s.lengthMm) || 0,
    marginMm: Number(s.marginMm) || 0,
    kerfMm: Number(s.kerfMm) || 0,
    partClearanceMm: Number(s.partClearanceMm) || 0,
  }));

  if (files?.["relationships.json"]) {
    warnings.push({
      code: "relationships",
      path: "relationships.json",
      msg: "relationships present — not used in nest/CAM yet",
    });
  }

  const parts = files?.["parts.json"]?.parts || [];
  if (!parts.length) {
    errors.push({ code: "parts_empty", path: "parts.parts", msg: "parts[] is empty" });
  }

  const panels = [];
  parts.forEach((p, i) => {
    const geom = p?.geometry || {};
    let points = Array.isArray(geom.nestingPolygon) ? geom.nestingPolygon : null;
    if (!points || points.length < 3) {
      points = tessellateEdges(geom.outerContour?.edges);
      if (points.length >= 3) {
        warnings.push({
          code: "tessellate",
          path: `parts[${i}].geometry`,
          msg: "no nestingPolygon — tessellated edges",
        });
      }
    }
    if (!points || points.length < 3) {
      errors.push({
        code: "outline",
        path: `parts[${i}].geometry`,
        msg: `panel ${p?.panelId || i}: need ≥3 outline points`,
      });
      return;
    }

    const innerById = new Map();
    for (const ic of geom.innerContours || []) {
      if (ic?.id && Array.isArray(ic.polygon) && ic.polygon.length >= 3) {
        innerById.set(String(ic.id), ic.polygon);
      }
    }

    const features = (p.features || []).map((f, fi) => {
      let x = Number(f.x) || 0;
      let y = Number(f.y) || 0;
      if (Array.isArray(f.center) && f.center.length >= 2) {
        x = Number(f.center[0]) || 0;
        y = Number(f.center[1]) || 0;
      }
      let path = Array.isArray(f.path) ? f.path : null;
      if (f.geometryRef && innerById.has(String(f.geometryRef))) {
        path = innerById.get(String(f.geometryRef));
      }
      return {
        featureId: f.featureId || f.id || `F${fi}`,
        kind: mapFeatureKind(f.featureType || f.kind),
        x,
        y,
        diameterMm: f.diameterMm,
        depthMm: f.depthMm,
        widthMm: f.widthMm,
        path,
      };
    });

    const ori = p.orientation || {};
    panels.push({
      panelId: p.panelId || `P${i}`,
      name: p.name,
      material: p.materialId || p.material,
      thicknessMm: Number(p.thicknessMm) || matThickness.get(String(p.materialId)) || 0,
      quantity: Math.max(1, Number(p.quantity) || 1),
      grainDirection: ori.grainDirection ?? p.grainDirection ?? null,
      allowedRotations: Array.isArray(ori.allowedRotations) ? ori.allowedRotations : p.allowedRotations,
      outline: { points, closed: true, frame: "panelLocal" },
      features,
    });
  });

  if (errors.length) {
    return { ok: false, errors, warnings, package: null };
  }

  const jobId = manifest.jobId || files?.["job.json"]?.jobId || null;
  return {
    ok: true,
    errors: [],
    warnings,
    package: {
      schema: SCHEMA,
      schemaVersion: SCHEMA_VERSION,
      sourceFormat: WOODJOB_FORMAT,
      jobId,
      units: manifest.coordinateUnit || "mm",
      sheets,
      panels,
    },
  };
}

function tessellateEdges(edges, arcSegments = 12) {
  if (!Array.isArray(edges) || !edges.length) return [];
  const pts = [];
  const add = (p) => {
    if (!pts.length || dist2(pts[pts.length - 1], p) > 1e-8) pts.push(p);
  };
  for (const e of edges) {
    const start = xy(e.start);
    const end = xy(e.end);
    if (!start || !end) continue;
    add(start);
    if (e.type === "arc") {
      const c = xy(e.center);
      if (c) {
        const cw = !!e.clockwise;
        let a0 = Math.atan2(start[1] - c[1], start[0] - c[0]);
        let a1 = Math.atan2(end[1] - c[1], end[0] - c[0]);
        let sweep = a1 - a0;
        if (cw) {
          while (sweep > 0) sweep -= Math.PI * 2;
          if (Math.abs(sweep) < 1e-9) sweep = -Math.PI * 2;
        } else {
          while (sweep < 0) sweep += Math.PI * 2;
          if (Math.abs(sweep) < 1e-9) sweep = Math.PI * 2;
        }
        const r = Math.hypot(start[0] - c[0], start[1] - c[1]);
        for (let s = 1; s < arcSegments; s++) {
          const t = s / arcSegments;
          const a = a0 + sweep * t;
          add([c[0] + r * Math.cos(a), c[1] + r * Math.sin(a)]);
        }
      }
    }
    add(end);
  }
  if (pts.length >= 2 && dist2(pts[0], pts[pts.length - 1]) < 1e-6) pts.pop();
  return pts;
}

function xy(v) {
  if (Array.isArray(v) && v.length >= 2) return [Number(v[0]), Number(v[1])];
  if (v && typeof v === "object") return [Number(v.x) || 0, Number(v.y) || 0];
  return null;
}

function dist2(a, b) {
  const dx = a[0] - b[0];
  const dy = a[1] - b[1];
  return dx * dx + dy * dy;
}

/** If file list looks like a woodjob set, assemble; else null. */
export function tryAssembleWoodJobFromFileMap(fileMap) {
  if (!fileMap || typeof fileMap !== "object") return null;
  const keys = Object.keys(fileMap);
  const hasManifest = keys.some((k) => /(^|\/)manifest\.json$/i.test(k));
  const hasParts = keys.some((k) => /(^|\/)parts\.json$/i.test(k));
  if (!hasManifest || !hasParts) return null;
  const normalized = {};
  for (const [k, v] of Object.entries(fileMap)) {
    const base = k.split(/[/\\]/).pop();
    if (base) normalized[base] = v;
  }
  return woodjobToCutPackage(normalized);
}

/** Normalize loose JSON into a cut-package shape (inject schema if panels present). */
export function normalizeCutPackage(raw) {
  if (!raw || typeof raw !== "object") return raw;
  const next = { ...raw };
  if (!next.schema && Array.isArray(next.panels)) {
    next.schema = SCHEMA;
    if (next.schemaVersion == null) next.schemaVersion = SCHEMA_VERSION;
  }
  if (next.schema === "cabinetnc.cut-project" && next.package) {
    return normalizeCutPackage(next.package);
  }
  // woodjob single-bundle (rare): { format, parts, ... }
  if (next.format === WOODJOB_FORMAT && Array.isArray(next.parts) && !Array.isArray(next.panels)) {
    const mapped = woodjobToCutPackage({
      "manifest.json": next,
      "parts.json": { parts: next.parts },
      "materials.json": { materials: next.materials || [] },
      "sheets.json": { sheetTypes: next.sheetTypes || next.sheets || [] },
      "job.json": next.job || { jobId: next.jobId },
    });
    return mapped.ok ? mapped.package : next;
  }
  return next;
}

export function validateCutPackage(raw) {
  const errors = [];
  const warnings = [];
  if (!raw || typeof raw !== "object") {
    return {
      ok: false,
      errors: [{ code: "root", path: "$", msg: "JSON root must be an object" }],
      warnings,
      package: null,
    };
  }

  let pkg = raw;
  if (raw.schema === "cabinetnc.cut-project") {
    if (!raw.package || typeof raw.package !== "object") {
      return {
        ok: false,
        errors: [{ code: "project_package", path: "$.package", msg: "cut-project missing package object" }],
        warnings,
        package: null,
      };
    }
    pkg = normalizeCutPackage(raw.package);
    warnings.push({
      code: "unwrapped_project",
      path: "$",
      msg: "imported cut-project package body (session applied separately)",
    });
  } else {
    pkg = normalizeCutPackage(raw);
  }

  if (pkg.schema !== SCHEMA) {
    if (Array.isArray(pkg.panels) && pkg.panels.length) {
      warnings.push({
        code: "schema_injected",
        path: "$.schema",
        msg: `missing schema — treated as "${SCHEMA}"`,
      });
      pkg = { ...pkg, schema: SCHEMA, schemaVersion: pkg.schemaVersion ?? SCHEMA_VERSION };
    } else {
      errors.push({
        code: "schema",
        path: "$.schema",
        msg: `schema must be "${SCHEMA}" (got ${JSON.stringify(raw.schema)})`,
      });
    }
  }
  if (Number(pkg.schemaVersion) !== SCHEMA_VERSION) {
    warnings.push({
      code: "schemaVersion",
      path: "$.schemaVersion",
      msg: `schemaVersion ${pkg.schemaVersion} — viewer targets v${SCHEMA_VERSION}`,
    });
  }

  const panels = Array.isArray(pkg.panels) ? pkg.panels : [];
  if (!panels.length) {
    errors.push({ code: "panels_empty", path: "$.panels", msg: "panels[] is empty — need at least one panel" });
  }
  panels.forEach((panel, i) => {
    const id = panel?.panelId || `#${i}`;
    const base = `$.panels[${i}]`;
    if (!panel?.panelId) {
      warnings.push({
        code: "panelId",
        path: `${base}.panelId`,
        msg: `panel index ${i} has no panelId`,
      });
    }
    const pts = panel?.outline?.points;
    if (!Array.isArray(pts) || pts.length < 3) {
      errors.push({
        code: "outline",
        path: `${base}.outline.points`,
        msg: `panel ${id}: outline.points needs ≥3 points (got ${Array.isArray(pts) ? pts.length : 0})`,
      });
    } else {
      for (let j = 0; j < pts.length; j++) {
        const p = pts[j];
        if (!Array.isArray(p) || p.length < 2 || !Number.isFinite(Number(p[0])) || !Number.isFinite(Number(p[1]))) {
          errors.push({
            code: "outline_pt",
            path: `${base}.outline.points[${j}]`,
            msg: `panel ${id}: point[${j}] must be [x,y] numbers`,
          });
          break;
        }
      }
    }
    const feats = panel?.features;
    if (!Array.isArray(feats) || feats.length === 0) {
      warnings.push({
        code: "features",
        path: `${base}.features`,
        msg: `panel ${id}: no features (outline-only)`,
      });
    }
  });

  const errMsgs = errors.map((e) => (typeof e === "string" ? e : e.msg));
  const warnMsgs = warnings.map((w) => (typeof w === "string" ? w : w.msg));
  return {
    ok: errors.length === 0,
    errors: errMsgs,
    warnings: warnMsgs,
    errorDetails: errors,
    warningDetails: warnings,
    package: errors.length ? null : pkg,
  };
}

export function formatValidationReport(result) {
  if (!result) return "";
  const lines = [];
  for (const e of result.errorDetails || []) {
    const row = typeof e === "string" ? e : `${e.path || "?"} · ${e.msg}`;
    lines.push(`✗ ${row}`);
  }
  for (const w of result.warningDetails || []) {
    const row = typeof w === "string" ? w : `${w.path || "?"} · ${w.msg}`;
    lines.push(`! ${row}`);
  }
  return lines.join("\n");
}

export function panelsById(pkg) {
  const map = new Map();
  for (const panel of pkg?.panels || []) {
    if (panel?.panelId) map.set(String(panel.panelId), panel);
  }
  return map;
}

export function sheetOptions(pkg) {
  const template = Array.isArray(pkg?.sheets) && pkg.sheets[0] ? pkg.sheets[0] : null;
  const tw = Number(template?.widthMm) || 1220;
  const th = Number(template?.lengthMm) || Number(template?.heightMm) || 2440;
  const nestCount = Number(pkg?.nestResult?.sheetCount) || 0;
  const nestSize = pkg?.nestResult?.sheetSize;
  const widthMm = Number(nestSize?.widthMm) || tw;
  const lengthMm = Number(nestSize?.lengthMm) || th;
  const count = Math.max(nestCount, Array.isArray(pkg?.sheets) ? pkg.sheets.length : 0, 1);

  return Array.from({ length: count }, (_, i) => {
    const s = pkg?.sheets?.[i];
    return {
      index: i,
      id: s?.sheetId || `S${i + 1}`,
      widthMm: Number(s?.widthMm) || widthMm,
      lengthMm: Number(s?.lengthMm) || Number(s?.heightMm) || lengthMm,
      label: `S${i + 1} ${Number(s?.widthMm) || widthMm}x${Number(s?.lengthMm) || Number(s?.heightMm) || lengthMm}`,
    };
  });
}

export function placementsForSheet(pkg, sheetIndex) {
  const all = pkg?.nestResult?.placements || [];
  return all.filter((p) => Number(p.sheetIndex || 0) === Number(sheetIndex));
}

/** Merge multiple cut-packages: concat panels (prefix ids on clash), keep first sheets. */
export function mergeCutPackages(pkgs) {
  const list = (pkgs || []).filter(Boolean);
  if (!list.length) return null;
  const base = structuredClone
    ? structuredClone(list[0])
    : JSON.parse(JSON.stringify(list[0]));
  const seen = new Set((base.panels || []).map((p) => String(p.panelId)));
  for (let i = 1; i < list.length; i++) {
    const pkg = list[i];
    for (const panel of pkg.panels || []) {
      let id = String(panel.panelId || `P${seen.size + 1}`);
      if (seen.has(id)) id = `${id}_m${i}`;
      seen.add(id);
      base.panels.push({ ...panel, panelId: id });
    }
  }
  delete base.nestResult;
  base.source = {
    ...(base.source || {}),
    exportId: `merge_${list.length}`,
    designName: base.source?.designName || `merged×${list.length}`,
  };
  return base;
}
