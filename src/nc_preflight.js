/** NC shop preflight before export (MakerHub-depth M5). */

function fmt(n) {
  return Math.round((Number(n) || 0) * 1000) / 1000;
}

function pathPoints(op) {
  if (op?.op === "drill") {
    if (op.sheetX == null || op.sheetY == null) return [];
    return [[Number(op.sheetX), Number(op.sheetY)]];
  }
  return Array.isArray(op?.path) ? op.path : [];
}

/**
 * @returns {{ ok: boolean, issues: { level: 'error'|'warn', code: string, msg: string }[] }}
 */
export function preflightNc(ops, profile = {}, sheet = null) {
  const issues = [];
  const placed = (ops || []).filter((o) => o && o.placed);
  if (!placed.length) {
    issues.push({
      level: "error",
      code: "no_ops",
      msg: "无已排工序 — 先排版并启用轮廓/钻孔/拉槽",
    });
    return { ok: false, issues };
  }

  const sheetW = Number(sheet?.widthMm) || Number(sheet?.w) || 0;
  const sheetH = Number(sheet?.lengthMm) || Number(sheet?.heightMm) || Number(sheet?.h) || 0;
  if (sheetW > 0 && sheetH > 0) {
    let oob = 0;
    for (const op of placed) {
      for (const pt of pathPoints(op)) {
        const x = Number(pt[0]) || 0;
        const y = Number(pt[1]) || 0;
        if (x < -0.5 || y < -0.5 || x > sheetW + 0.5 || y > sheetH + 0.5) oob += 1;
      }
    }
    if (oob) {
      issues.push({
        level: "error",
        code: "out_of_sheet",
        msg: `${oob} 个刀位点超出板材 ${fmt(sheetW)}×${fmt(sheetH)}`,
      });
    }
  }

  const feed = Number(profile.feedXyMmMin);
  if (!Number.isFinite(feed) || feed <= 0) {
    issues.push({
      level: "error",
      code: "bad_feed",
      msg: "XY 进给无效",
    });
  }
  const rpm = Number(profile.spindleRpm);
  if (!Number.isFinite(rpm) || rpm <= 0) {
    issues.push({
      level: "warn",
      code: "no_spindle",
      msg: "主轴转速未设置",
    });
  }
  if (profile.offsetContours && !(Number(profile.toolDiameterMm) > 0)) {
    issues.push({
      level: "warn",
      code: "offset_no_tool",
      msg: "刀补开启但刀径为 0",
    });
  }

  const ok = !issues.some((i) => i.level === "error");
  return { ok, issues };
}

export function formatPreflight(report) {
  if (!report?.issues?.length) return "预检通过";
  return report.issues.map((i) => `${i.level === "error" ? "✗" : "!"} ${i.msg}`).join("\n");
}
