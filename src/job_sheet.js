/** Job sheet HTML for print / shop floor (MakerHub-depth M5/M6). */

export function buildJobSheetHtml(pkg, profile = {}, extras = {}) {
  const name = pkg?.source?.designName || pkg?.source?.exportId || "job";
  const panels = pkg?.panels || [];
  const nest = pkg?.nestResult;
  const stats = nest?.stats || {};
  const mats = [...new Set(panels.map((p) => p.material || p.colorTag || "—"))];
  const rows = panels
    .map((p) => {
      const place = (nest?.placements || []).find((x) => String(x.panelId) === String(p.panelId));
      const w = Math.round(Number(p.bbox?.widthMm) || 0);
      const h = Math.round(Number(p.bbox?.heightMm) || 0);
      const sheet = place ? `S${Number(place.sheetIndex || 0) + 1}` : "—";
      const lock = place?.locked ? "L" : "";
      return `<tr><td>${p.panelId}</td><td>${w}×${h}</td><td>${sheet}</td><td>${lock}</td></tr>`;
    })
    .join("");
  const preflight = extras.preflightText || "";
  return `<!DOCTYPE html>
<html lang="zh-CN"><head><meta charset="utf-8"/>
<title>Job · ${escapeHtml(name)}</title>
<style>
  body{font:13px/1.4 system-ui,sans-serif;margin:24px;color:#111}
  h1{font-size:18px;margin:0 0 8px}
  .meta{color:#444;margin-bottom:16px;white-space:pre-wrap}
  table{border-collapse:collapse;width:100%;max-width:720px}
  th,td{border:1px solid #ccc;padding:4px 8px;text-align:left}
  th{background:#f4f4f4}
  @media print{button{display:none}}
</style></head><body>
<button onclick="print()">打印</button>
<h1>CabinetNC Cut · ${escapeHtml(name)}</h1>
<div class="meta">导出: ${escapeHtml(pkg?.source?.exportId || "—")}
机型: ${escapeHtml(profile.name || profile.id || "—")} · Ø${profile.toolDiameterMm ?? "—"}
板件: ${panels.length} · 排版: ${nest?.placements?.length || 0} / 未排 ${stats.unplacedCount ?? nest?.unplacedCount ?? "—"}
利用率: ${stats.utilizationPct != null ? Number(stats.utilizationPct).toFixed(1) + "%" : "—"}
材料: ${escapeHtml(mats.join(", "))}
${preflight ? "预检:\n" + escapeHtml(preflight) : ""}
</div>
<table><thead><tr><th>板件</th><th>尺寸</th><th>板</th><th>锁</th></tr></thead>
<tbody>${rows || "<tr><td colspan=4>—</td></tr>"}</tbody></table>
</body></html>`;
}

function escapeHtml(s) {
  return String(s ?? "")
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;");
}
