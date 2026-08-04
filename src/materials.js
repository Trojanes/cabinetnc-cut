/** Sheet / material helpers for MakerHub-like inspector. */

export function sheetSummary(sheet) {
  if (!sheet) return null;
  return {
    sheetId: sheet.sheetId || "—",
    material: sheet.material || sheet.colorTag || "—",
    thicknessMm: Number(sheet.thicknessMm) || 0,
    widthMm: Number(sheet.widthMm) || 0,
    lengthMm: Number(sheet.lengthMm) || 0,
  };
}

export function materialsFromPackage(pkg) {
  const map = new Map();
  for (const s of pkg?.sheets || []) {
    const name = s.material || s.colorTag || "unknown";
    const prev = map.get(name) || { material: name, sheets: 0, thicknessMm: Number(s.thicknessMm) || 0 };
    prev.sheets += 1;
    map.set(name, prev);
  }
  for (const p of pkg?.panels || []) {
    const name = p.material || p.colorTag;
    if (!name) continue;
    if (!map.has(name)) map.set(name, { material: name, sheets: 0, thicknessMm: Number(p.thicknessMm) || 0 });
  }
  return [...map.values()];
}

export function nestSettingsOf(pkg) {
  const s = pkg?.nestSettings || {};
  return {
    spacingMm: Number(s.spacingMm) || 12,
    borderMm: Number(s.borderMm) || 15,
    allowRotation: Boolean(s.allowRotation),
  };
}

/** Ensure sheets[] exists; patch stock W×L / material / thickness on all entries (single stock template). */
export function applyStockSheet(pkg, patch = {}) {
  if (!pkg) return null;
  if (!Array.isArray(pkg.sheets) || !pkg.sheets.length) {
    pkg.sheets = [
      {
        sheetId: "S1",
        material: "stock",
        thicknessMm: 18,
        widthMm: 1220,
        lengthMm: 2440,
      },
    ];
  }
  const w = Number(patch.widthMm);
  const l = Number(patch.lengthMm);
  const t = Number(patch.thicknessMm);
  const mat =
    patch.material != null && String(patch.material).trim()
      ? String(patch.material).trim()
      : null;
  for (const s of pkg.sheets) {
    if (Number.isFinite(w) && w > 0) s.widthMm = w;
    if (Number.isFinite(l) && l > 0) s.lengthMm = l;
    if (Number.isFinite(t) && t > 0) s.thicknessMm = t;
    if (mat) {
      s.material = mat;
      s.colorTag = mat;
    }
  }
  return sheetSummary(pkg.sheets[0]);
}
