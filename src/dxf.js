/** Minimal DXF R12 writer for nest sheets (mm). */

function fmt(n) {
  return (Math.round((Number(n) || 0) * 1000) / 1000).toFixed(3);
}

function entityPolyline(lines, pts, layer = "PANEL") {
  if (!pts || pts.length < 2) return;
  lines.push("0", "LWPOLYLINE");
  lines.push("8", layer);
  lines.push("90", String(pts.length));
  lines.push("70", "1"); // closed
  for (const p of pts) {
    lines.push("10", fmt(p[0]));
    lines.push("20", fmt(p[1]));
  }
}

function entityCircle(lines, cx, cy, r, layer = "HOLE") {
  if (!(r > 0)) return;
  lines.push("0", "CIRCLE");
  lines.push("8", layer);
  lines.push("10", fmt(cx));
  lines.push("20", fmt(cy));
  lines.push("30", "0.0");
  lines.push("40", fmt(r));
}

function entityLine(lines, x0, y0, x1, y1, layer = "GROOVE") {
  lines.push("0", "LINE");
  lines.push("8", layer);
  lines.push("10", fmt(x0));
  lines.push("20", fmt(y0));
  lines.push("30", "0.0");
  lines.push("11", fmt(x1));
  lines.push("21", fmt(y1));
  lines.push("31", "0.0");
}

function rotatePoint(x, y, deg) {
  const r = ((Number(deg) || 0) * Math.PI) / 180;
  const c = Math.cos(r);
  const s = Math.sin(r);
  return [x * c - y * s, x * s + y * c];
}

function worldOutline(panel, place) {
  const ox = Number(place?.offsetX) || 0;
  const oy = Number(place?.offsetY) || 0;
  const rot = Number(place?.rotationDeg) || 0;
  const pts = panel?.outline?.points;
  if (!Array.isArray(pts) || pts.length < 3) return [];
  return pts.map(([x, y]) => {
    const [rx, ry] = rotatePoint(Number(x) || 0, Number(y) || 0, rot);
    return [rx + ox, ry + oy];
  });
}

function worldFeaturePoint(x, y, place) {
  const ox = Number(place?.offsetX) || 0;
  const oy = Number(place?.offsetY) || 0;
  const rot = Number(place?.rotationDeg) || 0;
  const [rx, ry] = rotatePoint(Number(x) || 0, Number(y) || 0, rot);
  return [rx + ox, ry + oy];
}

/**
 * @param {object} pkg cut-package with nestResult
 * @param {number} [sheetIndex=0]
 * @param {{ includeFeatures?: boolean }} [opts]
 */
export function nestToDxf(pkg, sheetIndex = 0, opts = {}) {
  const includeFeatures = opts.includeFeatures !== false;
  const lines = [
    "0",
    "SECTION",
    "2",
    "HEADER",
    "9",
    "$INSUNITS",
    "70",
    "4",
    "0",
    "ENDSEC",
    "0",
    "SECTION",
    "2",
    "ENTITIES",
  ];

  const sheetW =
    Number(pkg?.nestResult?.sheetSize?.widthMm) ||
    Number(pkg?.sheets?.[0]?.widthMm) ||
    1220;
  const sheetH =
    Number(pkg?.nestResult?.sheetSize?.lengthMm) ||
    Number(pkg?.sheets?.[0]?.lengthMm) ||
    2440;
  entityPolyline(
    lines,
    [
      [0, 0],
      [sheetW, 0],
      [sheetW, sheetH],
      [0, sheetH],
    ],
    "SHEET"
  );

  const byId = new Map((pkg?.panels || []).map((p) => [String(p.panelId), p]));
  for (const place of pkg?.nestResult?.placements || []) {
    if (Number(place.sheetIndex || 0) !== Number(sheetIndex)) continue;
    const panel = byId.get(String(place.panelId));
    if (!panel) continue;
    const pts = worldOutline(panel, place);
    entityPolyline(lines, pts, "PANEL");

    if (!includeFeatures) continue;
    for (const feat of panel.features || []) {
      const kind = String(feat.kind || "");
      if (kind === "holeVertical") {
        const [cx, cy] = worldFeaturePoint(feat.x, feat.y, place);
        entityCircle(lines, cx, cy, (Number(feat.diameterMm) || 0) / 2, "HOLE");
      } else if (kind === "grooveVertical" && Array.isArray(feat.path) && feat.path.length >= 2) {
        for (let i = 0; i < feat.path.length - 1; i++) {
          const [x0, y0] = worldFeaturePoint(feat.path[i][0], feat.path[i][1], place);
          const [x1, y1] = worldFeaturePoint(feat.path[i + 1][0], feat.path[i + 1][1], place);
          entityLine(lines, x0, y0, x1, y1, "GROOVE");
        }
      }
    }
  }

  lines.push("0", "ENDSEC", "0", "EOF");
  return lines.join("\n") + "\n";
}
