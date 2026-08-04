import {
  validateCutPackage,
  panelsById,
  sheetOptions,
  placementsForSheet,
  formatValidationReport,
  mergeCutPackages,
  tryAssembleWoodJobFromFileMap,
} from "./package.js";
import { dxfToCutPackage } from "./dxf_import.js";
import { svgToCutPackage } from "./svg_import.js";
import { drawNest, hitTestNest, nestView, clampPlacementOnSheet, resolveNestPlacement, snapMm } from "./render.js";
import { packPanels, nestStats } from "./pack.js";
import { verifyNestPoly, verifyNestGapAsync } from "./nest_verify.js";
import { featuresToOps, attachOpsToNest, applyContourToolOffsetAsync, filterOpsEnabled } from "./ops.js";
import { opsToNc } from "./nc.js";
import { preflightNc, formatPreflight } from "./nc_preflight.js";
import { simOpsList, clampSimIndex, simStep, describeSimOp, expandSimFrames, describeSimFrame } from "./cam_sim.js";
import { MACHINE_PROFILES, getMachineProfile, toolRadiusMm, withProfileOverrides } from "./machine.js";
import { buildJobSheetHtml } from "./job_sheet.js";
import { nestToDxf } from "./dxf.js";
import { buildProjectDoc, parseProjectDoc } from "./project.js";
import { listRecent, pushRecent, getRecent, clearRecent } from "./recent.js";
import {
  sheetSummary,
  materialsFromPackage,
  nestSettingsOf,
  applyStockSheet,
} from "./materials.js";
import {
  loadLibrary,
  saveLibrary,
  upsertMaterial,
  upsertTool,
  librarySummary,
  defaultLibrary,
} from "./library.js";
import {
  fromCutPackagePanel,
  writeBackToPackage,
  translateFeatures,
  rotatePanel,
  addVerticalHole,
  addVerticalGroove,
  createRectPanel,
  toCutPackagePanel,
  panelMetrics,
  panelBbox,
  drawGeomPanel,
  hitTestGeom,
  applyGeomDrag,
  geomView,
  offsetPolygonAsync,
  differencePolygonAsync,
} from "./geom/index.js";

const els = {
  fileInput: document.getElementById("fileInput"),
  importFolderBtn: document.getElementById("importFolderBtn"),
  loadDemoBtn: document.getElementById("loadDemoBtn"),
  loadUnplacedBtn: document.getElementById("loadUnplacedBtn"),
  repackBtn: document.getElementById("repackBtn"),
  exportBtn: document.getElementById("exportBtn"),
  exportOpsBtn: document.getElementById("exportOpsBtn"),
  exportNcBtn: document.getElementById("exportNcBtn"),
  exportDxfBtn: document.getElementById("exportDxfBtn"),
  exportJobBtn: document.getElementById("exportJobBtn"),
  saveProjectBtn: document.getElementById("saveProjectBtn"),
  machineSelect: document.getElementById("machineSelect"),
  viewGeomBtn: document.getElementById("viewGeomBtn"),
  viewNestBtn: document.getElementById("viewNestBtn"),
  viewCamBtn: document.getElementById("viewCamBtn"),
  viewOutBtn: document.getElementById("viewOutBtn"),
  workflowMeta: document.getElementById("workflowMeta"),
  workflowProgress: document.getElementById("workflowProgress"),
  projectTree: document.getElementById("projectTree"),
  recentList: document.getElementById("recentList"),
  clearRecentBtn: document.getElementById("clearRecentBtn"),
  outPreview: document.getElementById("outPreview"),
  outActionsSection: document.getElementById("outActionsSection"),
  geomToolsSection: document.getElementById("geomToolsSection"),
  materialSection: document.getElementById("materialSection"),
  nestReportSection: document.getElementById("nestReportSection"),
  nestReportMeta: document.getElementById("nestReportMeta"),
  nestUnplacedList: document.getElementById("nestUnplacedList"),
  materialMeta: document.getElementById("materialMeta"),
  nestSpacing: document.getElementById("nestSpacing"),
  nestBorder: document.getElementById("nestBorder"),
  nestAllowRotChk: document.getElementById("nestAllowRotChk"),
  nestEngineSelect: document.getElementById("nestEngineSelect"),
  lockAllBtn: document.getElementById("lockAllBtn"),
  unlockAllBtn: document.getElementById("unlockAllBtn"),
  applyNestSettingsBtn: document.getElementById("applyNestSettingsBtn"),
  nestVerifyBtn: document.getElementById("nestVerifyBtn"),
  stockWidth: document.getElementById("stockWidth"),
  stockLength: document.getElementById("stockLength"),
  stockThick: document.getElementById("stockThick"),
  stockMaterial: document.getElementById("stockMaterial"),
  applyStockBtn: document.getElementById("applyStockBtn"),
  toolLibSection: document.getElementById("toolLibSection"),
  toolLibMeta: document.getElementById("toolLibMeta"),
  camParamsSection: document.getElementById("camParamsSection"),
  camSafeZ: document.getElementById("camSafeZ"),
  camContourDepth: document.getElementById("camContourDepth"),
  camStepdown: document.getElementById("camStepdown"),
  camPeck: document.getElementById("camPeck"),
  camOffsetChk: document.getElementById("camOffsetChk"),
  camEnableContour: document.getElementById("camEnableContour"),
  camEnableDrill: document.getElementById("camEnableDrill"),
  camEnableGroove: document.getElementById("camEnableGroove"),
  camApplyBtn: document.getElementById("camApplyBtn"),
  camParamsHint: document.getElementById("camParamsHint"),
  camSimPrevBtn: document.getElementById("camSimPrevBtn"),
  camSimPlayBtn: document.getElementById("camSimPlayBtn"),
  camSimNextBtn: document.getElementById("camSimNextBtn"),
  camSimMeta: document.getElementById("camSimMeta"),
  preflightMeta: document.getElementById("preflightMeta"),
  postDialect: document.getElementById("postDialect"),
  postProgramEnd: document.getElementById("postProgramEnd"),
  postOriginNote: document.getElementById("postOriginNote"),
  postApplyBtn: document.getElementById("postApplyBtn"),
  featSection: document.getElementById("featSection"),
  opsSection: document.getElementById("opsSection"),
  sheetLabel: document.getElementById("sheetLabel"),
  overlapLabel: document.getElementById("overlapLabel"),
  allowOverlapChk: document.getElementById("allowOverlapChk"),
  lockPlaceBtn: document.getElementById("lockPlaceBtn"),
  metaBox: document.getElementById("metaBox"),
  geomMeta: document.getElementById("geomMeta"),
  geomMoveBtn: document.getElementById("geomMoveBtn"),
  geomRotBtn: document.getElementById("geomRotBtn"),
  geomHoleBtn: document.getElementById("geomHoleBtn"),
  geomGrooveBtn: document.getElementById("geomGrooveBtn"),
  geomNewBtn: document.getElementById("geomNewBtn"),
  toolpathChk: document.getElementById("toolpathChk"),
  holeBoolChk: document.getElementById("holeBoolChk"),
  toolpathDelta: document.getElementById("toolpathDelta"),
  toolpathMeta: document.getElementById("toolpathMeta"),
  panelList: document.getElementById("panelList"),
  featList: document.getElementById("featList"),
  opsList: document.getElementById("opsList"),
  warnList: document.getElementById("warnList"),
  materialLibList: document.getElementById("materialLibList"),
  toolLibList: document.getElementById("toolLibList"),
  matNameInput: document.getElementById("matNameInput"),
  matThickInput: document.getElementById("matThickInput"),
  matAddBtn: document.getElementById("matAddBtn"),
  toolDiaInput: document.getElementById("toolDiaInput"),
  toolRpmInput: document.getElementById("toolRpmInput"),
  toolFeedXyInput: document.getElementById("toolFeedXyInput"),
  toolFeedZInput: document.getElementById("toolFeedZInput"),
  toolSaveBtn: document.getElementById("toolSaveBtn"),
  toolApplyBtn: document.getElementById("toolApplyBtn"),
  statusLeft: document.getElementById("statusLeft"),
  statusRight: document.getElementById("statusRight"),
  sheetSelect: document.getElementById("sheetSelect"),
  stageHint: document.getElementById("stageHint"),
  canvas: document.getElementById("nestCanvas"),
};

let state = {
  package: null,
  warnings: [],
  selectedPanelId: null,
  sheetIndex: 0,
  viewMode: "geom", // geom | nest | cam | out
  allowOverlap: false, // Nest: when false, drop cannot overlap others
  showToolpath: false,
  showHoleBool: false,
  toolpathDeltaMm: -6,
  toolpath: null, // { points, engine, mode, panelId, delta }
  toolpathSeq: 0,
  machineId: MACHINE_PROFILES[0].id,
  drag: null,
  draftGeom: null,
  selectedToolId: null,
  machineOverrides: {}, // machineId → partial profile fields
  camSimIndex: 0,
  camSimPlaying: false,
  camSimTimer: null,
  camSimMode: "point", // "op" | "point"
  folderWatch: null, // { dirHandle, timer, seen: Map }
};

/** Workshop library (M2) — hydrated from localStorage. */
let library = typeof localStorage !== "undefined" ? loadLibrary() : defaultLibrary();

function effectiveProfile() {
  return withProfileOverrides(
    getMachineProfile(state.machineId),
    state.machineOverrides[state.machineId]
  );
}

function fillToolForm(tool) {
  if (!tool) return;
  if (els.toolDiaInput) els.toolDiaInput.value = String(tool.diameterMm ?? 12);
  if (els.toolRpmInput) els.toolRpmInput.value = String(tool.spindleRpm ?? 18000);
  if (els.toolFeedXyInput) els.toolFeedXyInput.value = String(tool.feedXyMmMin ?? 3000);
  if (els.toolFeedZInput) els.toolFeedZInput.value = String(tool.feedZMmMin ?? 500);
}

function readToolForm() {
  return {
    diameterMm: Number(els.toolDiaInput?.value) || 0,
    spindleRpm: Number(els.toolRpmInput?.value) || 0,
    feedXyMmMin: Number(els.toolFeedXyInput?.value) || 0,
    feedZMmMin: Number(els.toolFeedZInput?.value) || 0,
  };
}

function setHint(text) {
  els.stageHint.textContent = text;
}

function syncExportBtn() {
  if (els.exportBtn) els.exportBtn.disabled = !state.package;
  if (els.exportOpsBtn) els.exportOpsBtn.disabled = !state.package;
  if (els.exportDxfBtn) els.exportDxfBtn.disabled = !state.package;
  if (els.exportJobBtn) els.exportJobBtn.disabled = !state.package;
  if (els.saveProjectBtn) els.saveProjectBtn.disabled = !state.package;
  if (els.repackBtn) els.repackBtn.disabled = !state.package;
  // exportNcBtn gated by refreshPreflight (bounds / empty ops)
  if (!state.package && els.exportNcBtn) els.exportNcBtn.disabled = true;
  const hasSel = !!(state.package && state.selectedPanelId);
  if (els.geomMoveBtn) els.geomMoveBtn.disabled = !hasSel;
  if (els.geomRotBtn) els.geomRotBtn.disabled = !hasSel;
  if (els.geomHoleBtn) els.geomHoleBtn.disabled = !hasSel;
  if (els.geomGrooveBtn) els.geomGrooveBtn.disabled = !hasSel;
  if (els.lockPlaceBtn) {
    const place = findPlacement(state.selectedPanelId);
    els.lockPlaceBtn.disabled = !place;
    els.lockPlaceBtn.textContent = place?.locked ? "解锁摆位" : "锁定摆位";
  }
}

function selectedRawPanel() {
  return (state.package?.panels || []).find((p) => p.panelId === state.selectedPanelId) || null;
}

function renderGeomMeta() {
  if (!els.geomMeta) return;
  const g = currentGeomPanel();
  if (!g) {
    els.geomMeta.classList.add("empty");
    els.geomMeta.textContent = "选板后可编辑";
    return;
  }
  const m = panelMetrics(g);
  els.geomMeta.classList.remove("empty");
  els.geomMeta.textContent = [
    `板件: ${g.panelId} · 视图 ${state.viewMode}`,
    `包围盒: ${m.bbox.width.toFixed(1)} × ${m.bbox.height.toFixed(1)} @ (${m.bbox.minX.toFixed(0)},${m.bbox.minY.toFixed(0)})`,
    `面积: ${m.areaMm2.toFixed(0)} mm² · 周长 ${m.perimeterMm.toFixed(0)} mm`,
    `特征: ${g.features.length} · 孔环 ${g.holes.length}`,
  ].join("\n");
}

function applyGeomEdit(mutator) {
  const raw = selectedRawPanel();
  if (!raw || !state.package) return;
  const geom = mutator(fromCutPackagePanel(raw));
  // plan: writeBack clears nestResult; Nest view / Repack rebuilds
  state.package = writeBackToPackage(state.package, geom);
  state.draftGeom = null;
  state.selectedPanelId = geom.panelId;
  setHint(`geom edit · ${geom.panelId}`);
  renderAll();
}

function currentGeomPanel() {
  if (state.draftGeom) return state.draftGeom;
  const raw = selectedRawPanel();
  return raw ? fromCutPackagePanel(raw) : null;
}

function commitDraftGeom() {
  if (!state.draftGeom || !state.package) return;
  state.package = writeBackToPackage(state.package, state.draftGeom);
  state.selectedPanelId = state.draftGeom.panelId;
  state.draftGeom = null;
}

const STAGE_HINT = {
  geom: "几何: 拖孔/槽端点/边手柄",
  nest: "排版: 拖摆位 · 锁定后 Repack 不挪 · 放下校验重叠",
  cam: "刀路: 绿虚线=轮廓 · 蓝十字=孔 · 橙线=槽（只读）",
  out: "输出: 预览 NC · 导出 JSON/Ops/NC/DXF",
};

function setViewMode(mode) {
  const allowed = ["geom", "nest", "cam", "out"];
  const next = allowed.includes(mode) ? mode : "geom";
  if (state.viewMode === "cam" && next !== "cam") stopCamSimPlay();
  state.viewMode = next;
  state.drag = null;
  state.draftGeom = null;

  for (const [id, m] of [
    ["viewGeomBtn", "geom"],
    ["viewNestBtn", "nest"],
    ["viewCamBtn", "cam"],
    ["viewOutBtn", "out"],
  ]) {
    els[id]?.classList.toggle("active", state.viewMode === m);
  }

  const sheetUi = state.viewMode === "nest" || state.viewMode === "cam" ? "" : "none";
  if (els.sheetLabel) els.sheetLabel.style.display = sheetUi;
  if (els.overlapLabel) els.overlapLabel.style.display = state.viewMode === "nest" ? "" : "none";
  if (els.lockPlaceBtn) els.lockPlaceBtn.style.display = state.viewMode === "nest" ? "" : "none";
  syncStageRails();

  const showCanvas = state.viewMode !== "out";
  if (els.canvas) els.canvas.hidden = !showCanvas;
  if (els.outPreview) els.outPreview.hidden = showCanvas;

  if (
    (state.viewMode === "nest" || state.viewMode === "cam") &&
    state.package &&
    !state.package.nestResult?.placements?.length
  ) {
    runShelfPack(state.package);
  }

  let hint = STAGE_HINT[state.viewMode] || "";
  if (state.viewMode === "nest" && state.allowOverlap) hint = "排版: 拖摆位（允许重叠）";
  setHint(hint);
  if (els.workflowMeta) {
    els.workflowMeta.textContent = `阶段 ${state.viewMode} · geom → nest → ops → nc`;
  }
  renderAll();
}

/** Right-rail panels follow MakerHub stage focus. */
function syncStageRails() {
  const m = state.viewMode;
  if (els.geomToolsSection) els.geomToolsSection.hidden = !(m === "geom");
  if (els.featSection) els.featSection.hidden = !(m === "geom");
  if (els.materialSection) els.materialSection.hidden = !(m === "nest");
  if (els.nestReportSection) els.nestReportSection.hidden = !(m === "nest");
  if (els.toolLibSection) els.toolLibSection.hidden = !(m === "cam" || m === "out");
  if (els.camParamsSection) els.camParamsSection.hidden = m !== "cam";
  if (els.opsSection) els.opsSection.hidden = !(m === "cam" || m === "nest");
  if (els.outActionsSection) els.outActionsSection.hidden = m !== "out";
}

function workflowFlags(pkg) {
  const hasPkg = !!pkg?.panels?.length;
  const hasNest = !!(pkg?.nestResult?.placements?.length);
  const ops = hasPkg ? featuresToOps(pkg.panels) : [];
  const hasOps = ops.length > 0;
  const hasNc = hasNest && hasOps;
  return { hasPkg, hasNest, hasOps, hasNc };
}

function renderWorkflowProgress(pkg) {
  if (!els.workflowProgress) return;
  const f = workflowFlags(pkg);
  const stages = [
    { id: "geom", done: f.hasPkg, label: "几何" },
    { id: "nest", done: f.hasNest, label: "排版" },
    { id: "cam", done: f.hasOps, label: "刀路" },
    { id: "out", done: f.hasNc, label: "输出" },
  ];
  els.workflowProgress.innerHTML = "";
  for (const s of stages) {
    const d = document.createElement("span");
    d.className = "wf-dot";
    d.title = s.label;
    if (s.done) d.classList.add("done");
    if (s.id === state.viewMode) d.classList.add("current");
    els.workflowProgress.appendChild(d);
  }
}

function buildPlacedOps(pkg, profile = effectiveProfile()) {
  return filterOpsEnabled(
    attachOpsToNest(featuresToOps(pkg?.panels || []), pkg?.nestResult),
    profile
  );
}

function renderMaterialPanel(pkg) {
  if (!els.materialMeta) return;
  if (!pkg) {
    els.materialMeta.classList.add("empty");
    els.materialMeta.textContent = "—";
    if (els.applyNestSettingsBtn) els.applyNestSettingsBtn.disabled = true;
    if (els.applyStockBtn) els.applyStockBtn.disabled = true;
    return;
  }
  const sheets = sheetOptions(pkg);
  const sheet = pkg.sheets?.[state.sheetIndex] || pkg.sheets?.[0];
  const sum = sheetSummary(sheet);
  const mats = materialsFromPackage(pkg);
  const nest = nestSettingsOf(pkg);
  els.materialMeta.classList.remove("empty");
  els.materialMeta.textContent = [
    sum
      ? `${sum.sheetId} · ${sum.material} · ${sum.thicknessMm}mm`
      : "无板材",
    sum ? `${sum.widthMm} × ${sum.lengthMm} mm` : "",
    `材料库: ${mats.map((m) => `${m.material}(×${m.sheets})`).join(", ") || "—"}`,
    `当前板索引 ${state.sheetIndex + 1}/${Math.max(1, sheets.length)}`,
  ]
    .filter(Boolean)
    .join("\n");

  if (sum) {
    if (els.stockWidth) els.stockWidth.value = String(sum.widthMm || 1220);
    if (els.stockLength) els.stockLength.value = String(sum.lengthMm || 2440);
    if (els.stockThick) els.stockThick.value = String(sum.thicknessMm || 18);
    if (els.stockMaterial) els.stockMaterial.value = sum.material === "—" ? "" : String(sum.material);
  }
  if (els.nestSpacing) els.nestSpacing.value = String(nest.spacingMm);
  if (els.nestBorder) els.nestBorder.value = String(nest.borderMm);
  if (els.nestAllowRotChk) els.nestAllowRotChk.checked = nest.allowRotation;
  if (els.nestEngineSelect) {
    els.nestEngineSelect.value = pkg.nestSettings?.packEngine || "auto";
  }
  if (els.applyNestSettingsBtn) els.applyNestSettingsBtn.disabled = false;
  if (els.applyStockBtn) els.applyStockBtn.disabled = false;
}

function renderNestReport(pkg) {
  if (!els.nestReportMeta) return;
  if (!pkg?.nestResult) {
    els.nestReportMeta.classList.add("empty");
    els.nestReportMeta.textContent = "尚未排版 — Repack 或进入排版阶段";
    if (els.nestUnplacedList) els.nestUnplacedList.innerHTML = "";
    return;
  }
  const spacing = Number(pkg.nestSettings?.spacingMm) || 12;
  const stats = pkg.nestResult.stats || nestStats(pkg.panels, pkg.nestResult, spacing);
  // cache for status bar
  if (!pkg.nestResult.stats) pkg.nestResult.stats = stats;
  const usedM2 = (stats.usedAreaMm2 / 1e6).toFixed(3);
  const sheetM2 = (stats.sheetAreaMm2 / 1e6).toFixed(3);
  els.nestReportMeta.classList.remove("empty");
  els.nestReportMeta.textContent = [
    `引擎: ${pkg.nestResult.engine || "—"}`,
    `利用率: ${Number(stats.utilizationPct).toFixed(1)}%`,
    `用料: ${usedM2} m² / 板材 ${sheetM2} m² ×${stats.sheetCount}`,
    `已排: ${pkg.nestResult.placements?.length || 0} · 未排: ${stats.unplacedCount}`,
    `碰撞对: ${stats.collisionCount}`,
    `锁定: ${(pkg.nestResult.placements || []).filter((p) => p.locked).length}`,
  ].join("\n");

  if (els.nestUnplacedList) {
    els.nestUnplacedList.innerHTML = "";
    const unplaced = pkg.nestResult.unplaced || [];
    if (!unplaced.length && stats.collisionCount === 0) {
      const li = document.createElement("li");
      li.textContent = "无未排 · 无碰撞";
      els.nestUnplacedList.appendChild(li);
    } else {
      for (const id of unplaced) {
        const li = document.createElement("li");
        li.textContent = `未排 ${id}`;
        els.nestUnplacedList.appendChild(li);
      }
      for (const hit of stats.collisions || []) {
        const li = document.createElement("li");
        li.textContent = `碰撞 ${hit.panelIdA} ↔ ${hit.panelIdB} S${(hit.sheetIndex || 0) + 1}`;
        els.nestUnplacedList.appendChild(li);
      }
    }
  }
}

function renderLibraryLists() {
  if (els.materialLibList) {
    els.materialLibList.innerHTML = "";
    for (const m of library.materials || []) {
      const li = document.createElement("li");
      li.textContent = `${m.name} · ${m.thicknessMm}mm`;
      li.title = m.id;
      li.addEventListener("click", () => {
        if (els.matNameInput) els.matNameInput.value = m.name;
        if (els.matThickInput) els.matThickInput.value = String(m.thicknessMm);
        setHint(`材料库 · ${m.name}`);
      });
      els.materialLibList.appendChild(li);
    }
  }
  if (els.toolLibList) {
    els.toolLibList.innerHTML = "";
    for (const t of library.tools || []) {
      const li = document.createElement("li");
      li.textContent = `${t.name} · Ø${t.diameterMm}`;
      if (t.id === state.selectedToolId || t.machineId === state.machineId) li.classList.add("active");
      li.addEventListener("click", () => {
        state.selectedToolId = t.id;
        fillToolForm(t);
        if (t.machineId && els.machineSelect) {
          els.machineSelect.value = t.machineId;
          state.machineId = t.machineId;
          applyMachineProfileToToolpathUi(effectiveProfile());
          renderToolLib();
          renderStatusBar();
          renderLibraryLists();
          setHint(`刀具库 · ${t.name}`);
          if (state.viewMode === "out") refreshOutPreview();
        } else {
          renderLibraryLists();
        }
      });
      els.toolLibList.appendChild(li);
    }
  }
}

function renderStatusBar() {
  if (!els.statusLeft && !els.statusRight) return;
  const pkg = state.package;
  const profile = effectiveProfile();
  const nest = pkg?.nestResult;
  const util =
    nest?.stats?.utilizationPct != null
      ? `${Number(nest.stats.utilizationPct).toFixed(1)}%`
      : "—";
  const locked = (nest?.placements || []).filter((p) => p.locked).length;
  const lib = librarySummary(library);
  const ov = state.machineOverrides[state.machineId] ? "·改" : "";
  if (els.statusLeft) {
    els.statusLeft.textContent = pkg
      ? `${state.viewMode.toUpperCase()} · 板 ${pkg.panels?.length || 0} · 排 ${nest?.placements?.length || 0} · util ${util} · 锁 ${locked}`
      : `${state.viewMode.toUpperCase()} · 未加载`;
  }
  if (els.statusRight) {
    els.statusRight.textContent = `${profile.name}${ov} Ø${profile.toolDiameterMm} · 库 材${lib.materialCount}/刀${lib.toolCount}`;
  }
}

function addMaterialFromUi() {
  const name = (els.matNameInput?.value || "").trim() || "material";
  const thicknessMm = Number(els.matThickInput?.value) || 18;
  library = upsertMaterial(library, { name, thicknessMm });
  saveLibrary(library);
  renderLibraryLists();
  renderStatusBar();
  setHint(`已添加材料 · ${name} ${thicknessMm}mm`);
}

function renderToolLib() {
  if (!els.toolLibMeta) return;
  const p = effectiveProfile();
  const ov = state.machineOverrides[state.machineId];
  els.toolLibMeta.classList.remove("empty");
  els.toolLibMeta.textContent = [
    `机型: ${p.name}`,
    `方言: ${p.dialect || "generic"} · 结束 ${p.programEnd || "M2"}`,
    `刀具: Ø${p.toolDiameterMm} → R${toolRadiusMm(p)} mm${ov ? " (已覆盖)" : ""}`,
    `安全Z: ${p.safeZMm} mm`,
    `进给: XY ${p.feedXyMmMin} · Z ${p.feedZMmMin}`,
    `主轴: S${p.spindleRpm} · 轮廓深 ${p.contourDepthMm ?? "—"} · 分层 ${p.contourStepdownMm || 0}`,
    `刀补轮廓: ${p.offsetContours ? "开 (内缩)" : "关"}`,
    `工序: C${p.enableContour !== false ? "开" : "关"}/D${p.enableDrill !== false ? "开" : "关"}/G${p.enableGroove !== false ? "开" : "关"}`,
  ].join("\n");
}

function renderCamParams() {
  const p = effectiveProfile();
  if (els.camSafeZ) els.camSafeZ.value = String(p.safeZMm ?? 5);
  if (els.camContourDepth) els.camContourDepth.value = String(p.contourDepthMm ?? 18);
  if (els.camStepdown) els.camStepdown.value = String(p.contourStepdownMm ?? 0);
  if (els.camPeck) els.camPeck.value = String(p.drillPeckMm ?? 0);
  if (els.camOffsetChk) els.camOffsetChk.checked = p.offsetContours !== false;
  if (els.camEnableContour) els.camEnableContour.checked = p.enableContour !== false;
  if (els.camEnableDrill) els.camEnableDrill.checked = p.enableDrill !== false;
  if (els.camEnableGroove) els.camEnableGroove.checked = p.enableGroove !== false;
  if (els.camParamsHint) {
    els.camParamsHint.classList.remove("empty");
    els.camParamsHint.textContent = `当前机型 ${p.name} · 改后点应用写入覆盖`;
  }
}

function applyCamParamsFromUi() {
  const safeZMm = Number(els.camSafeZ?.value);
  const contourDepthMm = Number(els.camContourDepth?.value);
  const contourStepdownMm = Number(els.camStepdown?.value);
  const drillPeckMm = Number(els.camPeck?.value);
  state.machineOverrides[state.machineId] = {
    ...(state.machineOverrides[state.machineId] || {}),
    safeZMm: Number.isFinite(safeZMm) ? safeZMm : 5,
    contourDepthMm: Number.isFinite(contourDepthMm) ? contourDepthMm : 18,
    contourStepdownMm: Number.isFinite(contourStepdownMm) ? Math.max(0, contourStepdownMm) : 0,
    drillPeckMm: Number.isFinite(drillPeckMm) ? Math.max(0, drillPeckMm) : 0,
    offsetContours: !!els.camOffsetChk?.checked,
    enableContour: !!els.camEnableContour?.checked,
    enableDrill: !!els.camEnableDrill?.checked,
    enableGroove: !!els.camEnableGroove?.checked,
  };
  const p = effectiveProfile();
  applyMachineProfileToToolpathUi(p);
  renderToolLib();
  renderCamParams();
  renderStatusBar();
  refreshOutPreview();
  renderAll();
  setHint(
    `刀路参数已应用 · Z${p.safeZMm} 深${p.contourDepthMm} 分层${p.contourStepdownMm || 0} 啄${p.drillPeckMm || 0}`
  );
}

function applyStockFromUi() {
  if (!state.package) return;
  const widthMm = Number(els.stockWidth?.value);
  const lengthMm = Number(els.stockLength?.value);
  const thicknessMm = Number(els.stockThick?.value);
  const material = (els.stockMaterial?.value || "").trim();
  const sum = applyStockSheet(state.package, { widthMm, lengthMm, thicknessMm, material });
  const locked = lockedPlacementsOf(state.package);
  delete state.package.nestResult;
  runShelfPack(state.package, locked);
  setHint(
    sum
      ? `stock · ${sum.material} ${sum.widthMm}×${sum.lengthMm}×${sum.thicknessMm}`
      : "stock applied"
  );
  renderAll();
}

function renderPostWizard() {
  const p = effectiveProfile();
  if (els.postDialect) els.postDialect.value = p.dialect === "fanuc_like" ? "fanuc_like" : "generic";
  if (els.postProgramEnd) els.postProgramEnd.value = String(p.programEnd || "M2").toUpperCase() === "M30" ? "M30" : "M2";
  if (els.postOriginNote) els.postOriginNote.value = p.originNote || "";
}

function applyPostWizardFromUi() {
  state.machineOverrides[state.machineId] = {
    ...(state.machineOverrides[state.machineId] || {}),
    dialect: els.postDialect?.value || "generic",
    programEnd: els.postProgramEnd?.value || "M2",
    originNote: (els.postOriginNote?.value || "").trim() || undefined,
  };
  // clear empty originNote key
  if (!state.machineOverrides[state.machineId].originNote) {
    delete state.machineOverrides[state.machineId].originNote;
  }
  renderToolLib();
  renderPostWizard();
  renderStatusBar();
  refreshOutPreview();
  const p = effectiveProfile();
  setHint(`后置已应用 · ${p.dialect} · ${p.programEnd}${p.originNote ? ` · ${p.originNote}` : ""}`);
}

function saveSelectedToolFromUi() {
  const form = readToolForm();
  const cur =
    (library.tools || []).find((t) => t.id === state.selectedToolId) ||
    (library.tools || []).find((t) => t.machineId === state.machineId);
  library = upsertTool(library, {
    id: cur?.id,
    name: cur?.name || `tool_${state.machineId}`,
    machineId: cur?.machineId || state.machineId,
    ...form,
    offsetContours: cur?.offsetContours ?? true,
  });
  state.selectedToolId = library.tools[library.tools.length - 1]?.id || cur?.id;
  // keep selection on upsert of existing
  if (cur?.id) state.selectedToolId = cur.id;
  saveLibrary(library);
  renderLibraryLists();
  setHint(`已保存刀具 · Ø${form.diameterMm}`);
}

function applyToolToMachineFromUi() {
  const form = readToolForm();
  state.machineOverrides[state.machineId] = {
    ...(state.machineOverrides[state.machineId] || {}),
    toolDiameterMm: form.diameterMm,
    spindleRpm: form.spindleRpm,
    feedXyMmMin: form.feedXyMmMin,
    feedZMmMin: form.feedZMmMin,
  };
  const profile = effectiveProfile();
  applyMachineProfileToToolpathUi(profile);
  // also sync library row
  const cur =
    (library.tools || []).find((t) => t.id === state.selectedToolId) ||
    (library.tools || []).find((t) => t.machineId === state.machineId);
  if (cur) {
    library = upsertTool(library, { ...cur, ...form });
    saveLibrary(library);
  }
  renderToolLib();
  renderLibraryLists();
  renderStatusBar();
  if (state.viewMode === "out") refreshOutPreview();
  setHint(`已应用到机型 · ${profile.name} Ø${form.diameterMm}`);
}

function applyNestSettingsFromUi() {
  if (!state.package) return;
  const spacingMm = Number(els.nestSpacing?.value);
  const borderMm = Number(els.nestBorder?.value);
  const packEngine = els.nestEngineSelect?.value || "auto";
  state.package.nestSettings = {
    ...(state.package.nestSettings || {}),
    spacingMm: Number.isFinite(spacingMm) ? spacingMm : 12,
    borderMm: Number.isFinite(borderMm) ? borderMm : 15,
    allowRotation: !!els.nestAllowRotChk?.checked,
    packEngine: ["auto", "blf", "shelf", "poly"].includes(packEngine) ? packEngine : "auto",
  };
  const locked = lockedPlacementsOf(state.package);
  delete state.package.nestResult;
  runShelfPack(state.package, locked);
  setHint(
    `nest · ${state.package.nestSettings.packEngine} · gap ${state.package.nestSettings.spacingMm} border ${state.package.nestSettings.borderMm}`
  );
  renderAll();
}

async function runNestVerify() {
  if (!state.package?.nestResult?.placements?.length) {
    setHint("无摆位可校验");
    return;
  }
  const spacing = Number(state.package.nestSettings?.spacingMm) || 12;
  const poly = verifyNestPoly(
    state.package.panels,
    state.package.nestResult.placements,
    spacing
  );
  let gap = poly;
  try {
    gap = await verifyNestGapAsync(
      state.package.panels,
      state.package.nestResult.placements,
      spacing,
      offsetPolygonAsync
    );
  } catch {
    /* keep poly */
  }
  const report = gap.hitCount >= poly.hitCount ? gap : poly;
  if (els.nestReportMeta) {
    els.nestReportMeta.classList.remove("empty");
    const prev = els.nestReportMeta.textContent || "";
    els.nestReportMeta.textContent =
      prev +
      `\n校验[${report.engine}]: ${report.ok ? "通过" : `碰撞 ${report.hitCount}`}` +
      (report.hits || [])
        .slice(0, 8)
        .map((h) => `\n  ${h.panelIdA}↔${h.panelIdB} S${(h.sheetIndex || 0) + 1}`)
        .join("");
  }
  setHint(
    report.ok
      ? `校验通过 · ${report.engine}`
      : `校验发现 ${report.hitCount} 对 · ${report.engine}`
  );
}

function setAllPlacementsLocked(locked) {
  if (!state.package?.nestResult?.placements?.length) {
    setHint("无摆位可锁定");
    return;
  }
  for (const p of state.package.nestResult.placements) {
    p.locked = !!locked;
  }
  setHint(locked ? `已锁定全部 ${state.package.nestResult.placements.length} 块` : "已全部解锁");
  renderAll();
}

function nestSheetMetrics() {
  const sheet = currentSheet();
  const view = nestView(els.canvas, sheet);
  const settings = state.package?.nestSettings || {};
  return {
    view,
    sheetW: view.sheetW,
    sheetH: view.sheetH,
    spacingMm: Number(settings.spacingMm) || 12,
    borderMm: Number(settings.borderMm) || 15,
  };
}

/** Live drag: sheet clamp only — never blocked by other panels. */
function previewNestOffset(panelId, offsetX, offsetY, rotationDeg) {
  const panel = panelsById(state.package).get(String(panelId));
  if (!panel) return null;
  const { sheetW, sheetH, borderMm } = nestSheetMetrics();
  const clamped = clampPlacementOnSheet(
    panel,
    { offsetX, offsetY, rotationDeg: Number(rotationDeg) || 0 },
    sheetW,
    sheetH,
    borderMm
  );
  setPlacementOffset(panelId, clamped.offsetX, clamped.offsetY);
  return { ...clamped, overlapping: nestOffsetOverlaps(panelId, clamped.offsetX, clamped.offsetY, rotationDeg) };
}

function nestOffsetOverlaps(panelId, offsetX, offsetY, rotationDeg) {
  const panel = panelsById(state.package).get(String(panelId));
  if (!panel) return false;
  const { sheetW, sheetH, spacingMm, borderMm } = nestSheetMetrics();
  const resolved = resolveNestPlacement({
    panel,
    place: {
      panelId,
      offsetX,
      offsetY,
      rotationDeg: Number(rotationDeg) || 0,
      sheetIndex: state.sheetIndex,
    },
    panelId,
    otherPlacements: state.package?.nestResult?.placements || [],
    panelsById: panelsById(state.package),
    sheetW,
    sheetH,
    spacingMm,
    borderMm,
    fallback: { offsetX, offsetY },
  });
  // If candidate conflicts, resolveNestPlacement returns fallback with blocked:true
  // when fallback === candidate coords and it overlaps, blocked is true.
  return !!resolved.blocked;
}

/**
 * Commit drop / nudge: if overlap disallowed and final pose conflicts, revert to fallback.
 */
function commitNestOffset(panelId, offsetX, offsetY, rotationDeg, fallback) {
  const panel = panelsById(state.package).get(String(panelId));
  if (!panel) return null;
  const { sheetW, sheetH, spacingMm, borderMm } = nestSheetMetrics();
  const place = {
    offsetX,
    offsetY,
    rotationDeg: Number(rotationDeg) || 0,
    sheetIndex: state.sheetIndex,
    panelId,
  };
  const fb = fallback || {
    offsetX: Number(findPlacement(panelId)?.offsetX) || 0,
    offsetY: Number(findPlacement(panelId)?.offsetY) || 0,
  };
  if (state.allowOverlap) {
    const clamped = clampPlacementOnSheet(panel, place, sheetW, sheetH, borderMm);
    setPlacementOffset(panelId, clamped.offsetX, clamped.offsetY);
    return { ...clamped, blocked: false };
  }
  const resolved = resolveNestPlacement({
    panel,
    place,
    panelId,
    otherPlacements: state.package?.nestResult?.placements || [],
    panelsById: panelsById(state.package),
    sheetW,
    sheetH,
    spacingMm,
    borderMm,
    fallback: fb,
  });
  setPlacementOffset(panelId, resolved.offsetX, resolved.offsetY);
  return resolved;
}

function runShelfPack(pkg, lockedPlacements = []) {
  const sheets = sheetOptions(pkg);
  const nest = packPanels(
    pkg.panels,
    sheets[0],
    pkg.nestSettings?.spacingMm,
    pkg.nestSettings?.borderMm,
    {
      allowRotation: Boolean(pkg.nestSettings?.allowRotation),
      lockedPlacements,
      engine: pkg.nestSettings?.packEngine || "auto",
    }
  );
  pkg.nestResult = nest;
  return nest;
}

function lockedPlacementsOf(pkg) {
  return (pkg?.nestResult?.placements || []).filter((p) => p && p.locked);
}

function repack() {
  if (!state.package) return;
  const locked = lockedPlacementsOf(state.package);
  delete state.package.nestResult;
  const nest = runShelfPack(state.package, locked);
  const extra = [];
  if (nest.unplacedCount) extra.push(`${nest.unplacedCount} unplaced`);
  if (locked.length) extra.push(`${locked.length} locked kept`);
  state.warnings = [
    ...(state.warnings || []).filter((w) => !String(w).includes("unplaced")),
    ...extra,
  ];
  state.sheetIndex = 0;
  setHint(
    `repacked · ${nest.sheetCount} sheet(s)${locked.length ? ` · ${locked.length} locked` : ""}`
  );
  renderAll();
}

function toggleLockSelected() {
  const place = findPlacement(state.selectedPanelId);
  if (!place) {
    setHint("先选中已排版板件再锁定");
    return;
  }
  place.locked = !place.locked;
  setHint(place.locked ? `已锁定 ${place.panelId}` : `已解锁 ${place.panelId}`);
  renderAll();
}

function downloadPackage() {
  if (!state.package) return;
  const blob = new Blob([JSON.stringify(state.package, null, 2)], {
    type: "application/json",
  });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  const id = state.package.source?.exportId || "cut";
  a.href = url;
  a.download = `cut_package_${id}.json`;
  a.click();
  URL.revokeObjectURL(url);
  setHint("已导出当前包（含 nestResult）");
}

function downloadOps() {
  if (!state.package) return;
  const ops = buildPlacedOps(state.package);
  const payload = {
    schema: "cabinetnc.cut-ops",
    schemaVersion: 1,
    sourceExportId: state.package.source?.exportId || null,
    opCount: ops.length,
    ops,
  };
  const blob = new Blob([JSON.stringify(payload, null, 2)], {
    type: "application/json",
  });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `cut_ops_${state.package.source?.exportId || "cut"}.json`;
  a.click();
  URL.revokeObjectURL(url);
  setHint(`exported ${ops.length} ops`);
}

async function downloadNc() {
  if (!state.package) return;
  const profile = effectiveProfile();
  let ops = buildPlacedOps(state.package, profile);
  const radius = toolRadiusMm(profile);
  if (profile.offsetContours && radius > 0) {
    ops = await applyContourToolOffsetAsync(ops, radius, offsetPolygonAsync);
    const jsContours = ops.filter(
      (o) => o.op === "contour" && o.placed && o.offsetEngine === "js"
    );
    if (jsContours.length) {
      setHint(
        `NC 已阻止导出：轮廓刀补需要 native Clipper（cabinetnc_offset），当前退回 JS AABB ×${jsContours.length}`
      );
      return;
    }
  }
  const report = preflightNc(ops, profile, currentSheet());
  await refreshPreflight();
  if (!report.ok) {
    setHint(`NC 预检失败 · ${report.issues.map((i) => i.msg).join("; ")}`);
    return;
  }
  const nc = opsToNc(ops, profile);
  const blob = new Blob([nc], { type: "text/plain" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `cut_${profile.id}_${state.package.source?.exportId || "cut"}.nc`;
  a.click();
  URL.revokeObjectURL(url);
  const nC = ops.filter((o) => o.op === "contour" && o.placed).length;
  const nD = ops.filter((o) => o.op === "drill" && o.placed).length;
  const nG = ops.filter((o) => o.op === "groove" && o.placed).length;
  setHint(`exported NC · ${profile.name} · C${nC}/D${nD}/G${nG}`);
}

function downloadDxf() {
  if (!state.package) return;
  const dxf = nestToDxf(state.package, state.sheetIndex);
  const blob = new Blob([dxf], { type: "application/dxf" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `nest_S${state.sheetIndex + 1}_${state.package.source?.exportId || "cut"}.dxf`;
  a.click();
  URL.revokeObjectURL(url);
  setHint(`exported DXF · sheet ${state.sheetIndex + 1}`);
}

function downloadJobSheet() {
  if (!state.package) return;
  const html = buildJobSheetHtml(state.package, effectiveProfile(), {
    preflightText: els.preflightMeta?.textContent || "",
  });
  const blob = new Blob([html], { type: "text/html" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `job_${state.package.source?.exportId || "cut"}.html`;
  a.click();
  URL.revokeObjectURL(url);
  setHint("exported job sheet HTML");
}

function downloadProject() {
  if (!state.package) return;
  const doc = buildProjectDoc(state.package, {
    machineId: state.machineId,
    allowOverlap: state.allowOverlap,
    showToolpath: state.showToolpath,
    toolpathDeltaMm: state.toolpathDeltaMm,
    viewMode: state.viewMode,
    selectedPanelId: state.selectedPanelId,
    sheetIndex: state.sheetIndex,
    library,
    machineOverrides: state.machineOverrides,
  });
  const label = state.package.source?.designName || state.package.source?.exportId || "project";
  pushRecent(label, doc);
  renderRecentList();
  const blob = new Blob([JSON.stringify(doc, null, 2)], { type: "application/json" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = `project_${state.package.source?.exportId || "cut"}.cut.json`;
  a.click();
  URL.revokeObjectURL(url);
  setHint("saved project .cut.json · 已记入最近");
}

function rememberCurrentAsRecent(label) {
  if (!state.package) return;
  const doc = buildProjectDoc(state.package, {
    machineId: state.machineId,
    allowOverlap: state.allowOverlap,
    showToolpath: state.showToolpath,
    toolpathDeltaMm: state.toolpathDeltaMm,
    viewMode: state.viewMode,
    selectedPanelId: state.selectedPanelId,
    sheetIndex: state.sheetIndex,
    library,
    machineOverrides: state.machineOverrides,
  });
  pushRecent(label || state.package.source?.designName || "session", doc);
  renderRecentList();
}

function renderRecentList() {
  if (!els.recentList) return;
  els.recentList.innerHTML = "";
  const items = listRecent();
  if (!items.length) {
    const li = document.createElement("li");
    li.textContent = "无 — 保存工程后出现";
    els.recentList.appendChild(li);
    return;
  }
  for (const e of items) {
    const li = document.createElement("li");
    const when = e.savedAt ? e.savedAt.slice(0, 16).replace("T", " ") : "";
    li.textContent = `${e.label} · ${when}`;
    li.title = e.id;
    li.addEventListener("click", () => {
      const hit = getRecent(e.id);
      if (!hit?.doc) {
        setHint("最近项丢失");
        return;
      }
      const parsed = parseProjectDoc(hit.doc);
      if (!parsed.ok) {
        setHint(`最近项无效: ${parsed.error}`);
        return;
      }
      applyPackage(parsed.package);
      applySession(parsed.session);
      setHint(`已恢复 · ${e.label}`);
    });
    els.recentList.appendChild(li);
  }
}

function applySession(session) {
  if (!session) return;
  if (session.machineId) {
    state.machineId = session.machineId;
    if (els.machineSelect) els.machineSelect.value = state.machineId;
    applyMachineProfileToToolpathUi(effectiveProfile());
  }
  if (session.allowOverlap != null) {
    state.allowOverlap = Boolean(session.allowOverlap);
    if (els.allowOverlapChk) els.allowOverlapChk.checked = state.allowOverlap;
  }
  if (session.showToolpath != null) {
    state.showToolpath = Boolean(session.showToolpath);
    if (els.toolpathChk) els.toolpathChk.checked = state.showToolpath;
  }
  if (session.toolpathDeltaMm != null && els.toolpathDelta) {
    els.toolpathDelta.value = String(session.toolpathDeltaMm);
    state.toolpathDeltaMm = toolpathDelta();
  }
  if (session.selectedPanelId) state.selectedPanelId = session.selectedPanelId;
  if (session.sheetIndex != null) state.sheetIndex = Number(session.sheetIndex) || 0;
  if (session.library?.materials || session.library?.tools) {
    library = {
      ...defaultLibrary(),
      materials: session.library.materials || library.materials,
      tools: session.library.tools || library.tools,
    };
    saveLibrary(library);
  }
  if (session.machineOverrides && typeof session.machineOverrides === "object") {
    state.machineOverrides = { ...session.machineOverrides };
    applyMachineProfileToToolpathUi(effectiveProfile());
  }
  if (session.viewMode) setViewMode(session.viewMode);
}

function fillMachineSelect() {
  if (!els.machineSelect) return;
  els.machineSelect.innerHTML = "";
  for (const p of MACHINE_PROFILES) {
    const opt = document.createElement("option");
    opt.value = p.id;
    opt.textContent = p.name;
    els.machineSelect.appendChild(opt);
  }
  els.machineSelect.value = state.machineId;
}

function applyMachineProfileToToolpathUi(profile) {
  const r = toolRadiusMm(profile);
  if (els.toolpathDelta && profile.offsetContours) {
    // negative = inward tool-center path (matches NC compensation)
    els.toolpathDelta.value = String(-(r || Math.abs(state.toolpathDeltaMm) || 6));
    state.toolpathDeltaMm = toolpathDelta();
  }
}

function renderMeta(pkg, warnings, errors) {
  if (!pkg) {
    els.metaBox.classList.add("empty");
    els.metaBox.textContent = errors?.length
      ? `加载失败\n${errors.join("\n")}`
      : "尚未加载";
    return;
  }
  els.metaBox.classList.remove("empty");
  const nest = pkg.nestResult || {};
  let holes = 0;
  let grooves = 0;
  for (const panel of pkg.panels || []) {
    for (const f of panel.features || []) {
      if (f.kind === "holeVertical") holes += 1;
      else if (f.kind === "grooveVertical") grooves += 1;
    }
  }
  els.metaBox.textContent = [
    `合同: ${pkg.schema} v${pkg.schemaVersion}`,
    `设计: ${pkg.source?.designName || "—"}`,
    `导出: ${pkg.source?.exportId || "—"}`,
    `板件: ${pkg.panels?.length || 0} · 板材: ${pkg.sheets?.length || 0}`,
    `特征: 孔 ${holes} · 槽 ${grooves}`,
    `排版: ${nest.engine || "none"} · 已排 ${nest.placements?.length || 0} · 未排 ${nest.unplacedCount || 0}`,
    nest.stats
      ? `利用率: ${Number(nest.stats.utilizationPct).toFixed(1)}% · 碰撞 ${nest.stats.collisionCount}`
      : null,
    `告警: ${warnings?.length || 0}`,
  ]
    .filter(Boolean)
    .join("\n");
}

function renderFeatureList(pkg) {
  els.featList.innerHTML = "";
  const panel = (pkg?.panels || []).find((p) => p.panelId === state.selectedPanelId);
  if (!panel) {
    const li = document.createElement("li");
    li.textContent = "点选板件查看";
    els.featList.appendChild(li);
    return;
  }
  const feats = panel.features || [];
  if (!feats.length) {
    const li = document.createElement("li");
    li.textContent = "无特征（仅轮廓）";
    els.featList.appendChild(li);
    return;
  }
  for (const f of feats) {
    const li = document.createElement("li");
    if (f.kind === "holeVertical") {
      li.textContent = `孔 ${f.id || ""} · ⌀${f.diameterMm} × ${f.depthMm}mm @ (${f.x}, ${f.y})`;
    } else if (f.kind === "grooveVertical") {
      li.textContent = `槽 ${f.id || ""} · w${f.widthMm} d${f.depthMm}mm · ${f.path?.length || 0} pts`;
    } else {
      li.textContent = `${f.kind || "?"} ${f.id || ""}`;
    }
    els.featList.appendChild(li);
  }
}

function renderOpsList(pkg) {
  els.opsList.innerHTML = "";
  const ops = buildPlacedOps(pkg).filter(
    (o) => !state.selectedPanelId || o.panelId === state.selectedPanelId
  );
  if (!ops.length) {
    const li = document.createElement("li");
    li.textContent = "-";
    els.opsList.appendChild(li);
    return;
  }
  for (const o of ops) {
    const li = document.createElement("li");
    if (o.op === "drill") {
      const xy =
        o.placed && o.sheetX != null
          ? `sheet(${o.sheetX},${o.sheetY})`
          : `@(${o.x},${o.y})`;
      li.textContent = `drill ${o.panelId} ⌀${o.diameterMm} d${o.depthMm} ${xy}`;
    } else if (o.op === "contour") {
      li.textContent = `contour ${o.panelId} pts${o.path?.length || 0}${o.placed ? ` S${o.sheetIndex + 1}` : ""}`;
    } else {
      li.textContent = `groove ${o.panelId} w${o.widthMm} d${o.depthMm} pts${o.path?.length || 0}`;
    }
    els.opsList.appendChild(li);
  }
}

function renderLists(pkg, warnings) {
  const placeByPanel = new Map();
  for (const p of pkg?.nestResult?.placements || []) {
    placeByPanel.set(String(p.panelId), p);
  }
  els.panelList.innerHTML = "";
  for (const panel of pkg?.panels || []) {
    const li = document.createElement("li");
    const featN = (panel.features || []).length;
    const place = placeByPanel.get(String(panel.panelId));
    const sheetTag = place ? `S${Number(place.sheetIndex) + 1}` : "—";
    const lockTag = place?.locked ? "🔒" : "";
    li.textContent = `${lockTag}${panel.panelId} ${sheetTag} ${Math.round(panel.bbox?.widthMm || 0)}x${Math.round(panel.bbox?.heightMm || 0)} f${featN}`;
    if (state.selectedPanelId === panel.panelId) li.classList.add("active");
    li.addEventListener("click", () => {
      state.selectedPanelId = panel.panelId;
      state.draftGeom = null;
      if (place) state.sheetIndex = Number(place.sheetIndex) || 0;
      renderAll();
    });
    els.panelList.appendChild(li);
  }
  renderFeatureList(pkg);
  renderOpsList(pkg);

  els.warnList.innerHTML = "";
  const unplaced = pkg?.nestResult?.unplaced || [];
  const allWarns = [
    ...(warnings || []),
    ...unplaced.map((id) => `unplaced: ${id}`),
  ];
  for (const w of allWarns) {
    const li = document.createElement("li");
    li.textContent = w;
    els.warnList.appendChild(li);
  }
  if (!allWarns.length) {
    const li = document.createElement("li");
    li.textContent = "-";
    els.warnList.appendChild(li);
  }
}

function renderSheets(pkg) {
  const opts = sheetOptions(pkg);
  els.sheetSelect.innerHTML = "";
  for (const opt of opts) {
    const option = document.createElement("option");
    option.value = String(opt.index);
    option.textContent = opt.label;
    els.sheetSelect.appendChild(option);
  }
  if (state.sheetIndex >= opts.length) state.sheetIndex = 0;
  els.sheetSelect.value = String(state.sheetIndex);
}

function paint() {
  const pkg = state.package;
  if (state.viewMode === "out") {
    refreshOutPreview();
    return;
  }
  if (!pkg) {
    const ctx = els.canvas.getContext("2d");
    ctx.clearRect(0, 0, els.canvas.width, els.canvas.height);
    return;
  }
  if (state.viewMode === "geom") {
    const geom = currentGeomPanel();
    if (!geom) {
      const ctx = els.canvas.getContext("2d");
      const dpr = window.devicePixelRatio || 1;
      const w = els.canvas.clientWidth || 800;
      const h = els.canvas.clientHeight || 600;
      els.canvas.width = Math.floor(w * dpr);
      els.canvas.height = Math.floor(h * dpr);
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      ctx.fillStyle = "#f4f4f4";
      ctx.fillRect(0, 0, w, h);
      ctx.fillStyle = "#666";
      ctx.font = "14px sans-serif";
      ctx.fillText("Select a panel to edit geometry", 16, 28);
      return;
    }
    drawGeomPanel(els.canvas, geom, {
      toolpathPoints: state.showToolpath || state.showHoleBool ? state.toolpath?.points : null,
      toolpathEngine: state.toolpath?.engine,
    });
    return;
  }
  // nest + cam share sheet canvas; cam draws ops markers on top
  const sheets = sheetOptions(pkg);
  const sheet = sheets[state.sheetIndex] || sheets[0];
  const camOps = state.viewMode === "cam" ? camOpsForSheet(pkg, state.sheetIndex) : null;
  const frames = camOps ? expandSimFrames(camOps) : [];
  const fi = frames.length ? clampSimIndex(state.camSimIndex, frames.length) : -1;
  const head = fi >= 0 ? frames[fi] : null;
  drawNest({
    canvas: els.canvas,
    sheet,
    panelsById: panelsById(pkg),
    placements: placementsForSheet(pkg, state.sheetIndex),
    selectedPanelId: state.selectedPanelId,
    topPanelId:
      state.drag?.mode === "nest" ? state.drag.panelId : state.selectedPanelId,
    spacingMm: Number(pkg.nestSettings?.spacingMm) || 12,
    opsOverlay: camOps,
    opsHighlightIndex: head ? head.opIndex : camOps && camOps.length ? 0 : -1,
    opsToolhead: head ? { x: head.x, y: head.y } : null,
  });
}

function camOpsForSheet(pkg, sheetIndex) {
  return buildPlacedOps(pkg).filter(
    (o) => o.placed && Number(o.sheetIndex || 0) === Number(sheetIndex)
  );
}

function camSimFrames() {
  if (!state.package) return [];
  return expandSimFrames(camOpsForSheet(state.package, state.sheetIndex));
}

function renderCamSim(pkg) {
  if (!els.camSimMeta) return;
  if (state.viewMode !== "cam" || !pkg) {
    els.camSimMeta.classList.add("empty");
    els.camSimMeta.textContent = "模拟 —";
    return;
  }
  const frames = expandSimFrames(camOpsForSheet(pkg, state.sheetIndex));
  state.camSimIndex = clampSimIndex(state.camSimIndex, frames.length);
  const frame = frames[state.camSimIndex];
  els.camSimMeta.classList.remove("empty");
  els.camSimMeta.textContent =
    (frame
      ? describeSimFrame(frame, state.camSimIndex, frames.length)
      : describeSimOp(null, 0, 0)) + (state.camSimPlaying ? " · 播放中" : "");
  if (els.camSimPlayBtn) els.camSimPlayBtn.textContent = state.camSimPlaying ? "❚❚" : "▶";
}

function stopCamSimPlay() {
  state.camSimPlaying = false;
  if (state.camSimTimer) {
    clearInterval(state.camSimTimer);
    state.camSimTimer = null;
  }
  if (els.camSimPlayBtn) els.camSimPlayBtn.textContent = "▶";
}

function camSimMove(delta) {
  if (!state.package) return;
  const frames = camSimFrames();
  if (!frames.length) {
    setHint("无工序可模拟");
    return;
  }
  state.camSimIndex = simStep(state.camSimIndex, frames.length, delta);
  renderCamSim(state.package);
  paint();
}

function toggleCamSimPlay() {
  if (!state.package || state.viewMode !== "cam") return;
  const frames = camSimFrames();
  if (!frames.length) {
    setHint("无工序可模拟");
    return;
  }
  if (state.camSimPlaying) {
    stopCamSimPlay();
    renderCamSim(state.package);
    return;
  }
  state.camSimPlaying = true;
  if (els.camSimPlayBtn) els.camSimPlayBtn.textContent = "❚❚";
  state.camSimTimer = setInterval(() => {
    if (!state.package || state.viewMode !== "cam") {
      stopCamSimPlay();
      return;
    }
    const n = camSimFrames().length;
    if (!n) {
      stopCamSimPlay();
      return;
    }
    state.camSimIndex = simStep(state.camSimIndex, n, 1);
    renderCamSim(state.package);
    paint();
  }, 120);
  renderCamSim(state.package);
}

async function refreshPreflight() {
  if (!els.preflightMeta) return;
  if (!state.package) {
    els.preflightMeta.classList.add("empty");
    els.preflightMeta.textContent = "预检 —";
    return;
  }
  const profile = effectiveProfile();
  let ops = buildPlacedOps(state.package, profile);
  const radius = toolRadiusMm(profile);
  if (profile.offsetContours && radius > 0) {
    ops = await applyContourToolOffsetAsync(ops, radius, offsetPolygonAsync);
  }
  const sheet = currentSheet();
  const report = preflightNc(ops, profile, sheet);
  els.preflightMeta.classList.remove("empty");
  els.preflightMeta.textContent = formatPreflight(report);
  if (els.exportNcBtn) {
    els.exportNcBtn.disabled = !report.ok;
    els.exportNcBtn.title = report.ok ? "" : formatPreflight(report);
  }
}

async function refreshOutPreview() {
  if (!els.outPreview) return;
  if (!state.package) {
    els.outPreview.textContent = "(无工程 — Import / Demo)";
    await refreshPreflight();
    return;
  }
  const profile = effectiveProfile();
  let ops = buildPlacedOps(state.package, profile);
  const radius = toolRadiusMm(profile);
  if (profile.offsetContours && radius > 0) {
    ops = await applyContourToolOffsetAsync(ops, radius, offsetPolygonAsync);
  }
  const nc = opsToNc(ops, profile);
  els.outPreview.textContent = nc;
  await refreshPreflight();
}

function canvasCssPos(ev) {
  const rect = els.canvas.getBoundingClientRect();
  return { x: ev.clientX - rect.left, y: ev.clientY - rect.top };
}

function currentSheet() {
  const sheets = sheetOptions(state.package);
  return sheets[state.sheetIndex] || sheets[0];
}

function findPlacement(panelId) {
  return (state.package?.nestResult?.placements || []).find(
    (p) => String(p.panelId) === String(panelId)
  );
}

function setPlacementOffset(panelId, offsetX, offsetY) {
  const place = findPlacement(panelId);
  if (!place) return;
  place.offsetX = offsetX;
  place.offsetY = offsetY;
}

function onCanvasPointerDown(ev) {
  if (!state.package || !els.canvas) return;
  const { x, y } = canvasCssPos(ev);

  if (state.viewMode === "geom") {
    const geom = currentGeomPanel();
    if (!geom) return;
    const hit = hitTestGeom(els.canvas, geom, x, y);
    if (!hit || hit.type === "panel") {
      state.drag = null;
      return;
    }
    state.draftGeom = structuredClone(geom);
    state.drag = {
      mode: "geom",
      ...hit,
      moved: false,
      startGeom: structuredClone(geom),
    };
    els.canvas.setPointerCapture?.(ev.pointerId);
    els.canvas.style.cursor = "grabbing";
    paint();
    ev.preventDefault();
    return;
  }

  if (state.viewMode === "out") return;

  const sheet = currentSheet();
  const hit = hitTestNest(
    els.canvas,
    sheet,
    panelsById(state.package),
    placementsForSheet(state.package, state.sheetIndex),
    x,
    y
  );
  if (!hit) {
    state.drag = null;
    return;
  }
  state.selectedPanelId = hit;
  const place = findPlacement(hit);
  if (!place || state.viewMode === "cam") {
    renderAll();
    return;
  }
  const view = nestView(els.canvas, sheet);
  const [mx, my] = view.toSheet(x, y);
  state.drag = {
    mode: "nest",
    panelId: hit,
    startMx: mx,
    startMy: my,
    origOx: Number(place.offsetX) || 0,
    origOy: Number(place.offsetY) || 0,
    moved: false,
  };
  els.canvas.setPointerCapture?.(ev.pointerId);
  els.canvas.style.cursor = "grabbing";
  renderAll();
  ev.preventDefault();
}

function onCanvasPointerMove(ev) {
  if (!state.drag || !state.package) return;

  if (state.drag.mode === "geom") {
    const base = state.drag.startGeom || state.draftGeom || currentGeomPanel();
    if (!base) return;
    const view = geomView(els.canvas, state.drag.startGeom || base);
    const { x, y } = canvasCssPos(ev);
    const [lx, ly] = view.toLocal(x, y);
    state.draftGeom = applyGeomDrag(base, state.drag, lx, ly);
    state.drag.moved = true;
    setHint(`edit ${state.drag.type}`);
    paint();
    renderGeomMeta();
    return;
  }

  const sheet = currentSheet();
  const view = nestView(els.canvas, sheet);
  const { x, y } = canvasCssPos(ev);
  const [mx, my] = view.toSheet(x, y);
  const dx = mx - state.drag.startMx;
  const dy = my - state.drag.startMy;
  if (Math.hypot(dx, dy) > 0.5) state.drag.moved = true;
  const ox = snapMm(state.drag.origOx + dx, 10);
  const oy = snapMm(state.drag.origOy + dy, 10);
  const rot = findPlacement(state.drag.panelId)?.rotationDeg || 0;
  // Live preview: never blocked by other panels (only sheet edge).
  const preview = previewNestOffset(state.drag.panelId, ox, oy, rot);
  if (preview) {
    const overlapWarn =
      !state.allowOverlap && preview.overlapping
        ? " · 重叠，松开将还原"
        : "";
    setHint(
      `move ${state.drag.panelId} → (${preview.offsetX}, ${preview.offsetY})${overlapWarn}`
    );
  }
  paint();
  renderGeomMeta();
}

function onCanvasPointerUp(ev) {
  if (!state.drag) return;
  const drag = state.drag;
  state.drag = null;
  els.canvas.style.cursor = "grab";
  try {
    els.canvas.releasePointerCapture?.(ev.pointerId);
  } catch (_) {
    /* ignore */
  }

  if (drag.mode === "geom") {
    if (drag.moved) {
      commitDraftGeom();
      setHint(`geom saved · ${state.selectedPanelId}`);
    } else {
      state.draftGeom = null;
    }
    renderAll();
    return;
  }

  // Drop: if overlap disallowed and final pose conflicts → snap back to drag start.
  if (drag.moved) {
    const place = findPlacement(drag.panelId);
    const committed = commitNestOffset(
      drag.panelId,
      Number(place?.offsetX) || 0,
      Number(place?.offsetY) || 0,
      place?.rotationDeg,
      { offsetX: drag.origOx, offsetY: drag.origOy }
    );
    if (committed?.blocked) {
      setHint(`还原 ${drag.panelId} · 放下位置与其它板重叠`);
    } else {
      setHint(`placed ${drag.panelId}`);
    }
  } else {
    setHint(`selected ${drag.panelId}`);
  }
  renderAll();
}

function renderProjectTree(pkg) {
  if (!els.projectTree) return;
  if (!pkg) {
    els.projectTree.classList.add("empty");
    els.projectTree.textContent = "尚未加载";
    return;
  }
  els.projectTree.classList.remove("empty");
  els.projectTree.innerHTML = "";

  const root = document.createElement("div");
  root.className = "tree-node";
  root.innerHTML = `<strong>${pkg.source?.designName || pkg.source?.exportId || "工程"}</strong>`;
  els.projectTree.appendChild(root);

  const sheetsLabel = document.createElement("div");
  sheetsLabel.className = "tree-label";
  sheetsLabel.textContent = "板材";
  els.projectTree.appendChild(sheetsLabel);

  const sheetsWrap = document.createElement("div");
  sheetsWrap.className = "tree-children";
  for (const opt of sheetOptions(pkg)) {
    const node = document.createElement("div");
    node.className = "tree-node";
    const btn = document.createElement("button");
    btn.type = "button";
    btn.textContent = opt.label;
    if (Number(opt.index) === Number(state.sheetIndex)) btn.classList.add("active");
    btn.addEventListener("click", () => {
      state.sheetIndex = Number(opt.index) || 0;
      if (state.viewMode === "geom") setViewMode("nest");
      else renderAll();
    });
    node.appendChild(btn);
    sheetsWrap.appendChild(node);
  }
  els.projectTree.appendChild(sheetsWrap);

  const panelsLabel = document.createElement("div");
  panelsLabel.className = "tree-label";
  panelsLabel.textContent = "板件";
  els.projectTree.appendChild(panelsLabel);

  const panelsWrap = document.createElement("div");
  panelsWrap.className = "tree-children";
  const placeByPanel = new Map(
    (pkg.nestResult?.placements || []).map((p) => [String(p.panelId), p])
  );
  for (const panel of pkg.panels || []) {
    const node = document.createElement("div");
    node.className = "tree-node";
    const btn = document.createElement("button");
    btn.type = "button";
    const place = placeByPanel.get(String(panel.panelId));
    const tag = place ? `S${Number(place.sheetIndex) + 1}` : "—";
    btn.textContent = `${panel.panelId} · ${tag}`;
    if (state.selectedPanelId === panel.panelId) btn.classList.add("active");
    btn.addEventListener("click", () => {
      state.selectedPanelId = panel.panelId;
      state.draftGeom = null;
      if (place) state.sheetIndex = Number(place.sheetIndex) || 0;
      renderAll();
    });
    node.appendChild(btn);
    panelsWrap.appendChild(node);
  }
  els.projectTree.appendChild(panelsWrap);
}

function renderAll() {
  renderMeta(state.package, state.warnings);
  renderWorkflowProgress(state.package);
  renderProjectTree(state.package);
  renderLists(state.package, state.warnings);
  renderGeomMeta();
  renderMaterialPanel(state.package);
  renderNestReport(state.package);
  renderToolLib();
  renderCamParams();
  renderCamSim(state.package);
  renderPostWizard();
  renderLibraryLists();
  renderRecentList();
  renderStatusBar();
  if (state.package) renderSheets(state.package);
  syncExportBtn();
  syncStageRails();
  paint();
  scheduleToolpathRefresh();
  if (state.viewMode === "out") refreshOutPreview();
  else refreshPreflight();
}

function toolpathDelta() {
  const n = Number(els.toolpathDelta?.value);
  return Number.isFinite(n) ? n : state.toolpathDeltaMm;
}

function setToolpathMeta(text, empty = false) {
  if (!els.toolpathMeta) return;
  els.toolpathMeta.classList.toggle("empty", empty);
  els.toolpathMeta.textContent = text;
}

/** Debounced Clipper offset/difference via /api/offset for Geom preview. */
function scheduleToolpathRefresh() {
  const wantOffset = state.showToolpath;
  const wantBool = state.showHoleBool;
  if ((!wantOffset && !wantBool) || state.viewMode !== "geom") {
    state.toolpath = null;
    setToolpathMeta("—", true);
    return;
  }
  const geom = currentGeomPanel();
  if (!geom?.outline?.points?.length) {
    state.toolpath = null;
    setToolpathMeta("无轮廓", true);
    paint();
    return;
  }
  const seq = ++state.toolpathSeq;
  const panelId = geom.panelId;
  setToolpathMeta("计算中…");

  const holeClips = (geom.holes || [])
    .map((h) => h.points)
    .filter((p) => Array.isArray(p) && p.length >= 3);

  const run = wantBool && holeClips.length
    ? differencePolygonAsync(geom.outline.points, holeClips).then((r) => ({
        ...r,
        delta: 0,
        label: `孔布尔 · ${holeClips.length} holes`,
      }))
    : wantBool && !holeClips.length
      ? Promise.resolve({
          points: geom.outline.points,
          engine: "js",
          mode: "no_holes",
          delta: 0,
          label: "无孔",
        })
      : (() => {
          const delta = toolpathDelta();
          return offsetPolygonAsync(geom.outline.points, delta).then((r) => ({
            ...r,
            delta,
            label: `Δ${delta}mm`,
          }));
        })();

  run.then((r) => {
    if (seq !== state.toolpathSeq) return;
    state.toolpath = {
      points: r.points,
      engine: r.engine,
      mode: r.mode,
      panelId,
      delta: r.delta,
    };
    setToolpathMeta(`${r.engine}/${r.mode} · ${r.label} · ${r.points.length}pts`);
    paint();
  });
}

function applyPackage(raw) {
  const result = validateCutPackage(raw);
  if (!result.ok) {
    state.package = null;
    state.warnings = result.warnings;
    renderMeta(null, result.warnings, result.errors);
    els.panelList.innerHTML = "";
    els.warnList.innerHTML = "";
    const details = result.errorDetails?.length
      ? result.errorDetails
      : (result.errors || []).map((msg) => ({ path: "?", msg }));
    for (const e of details) {
      const li = document.createElement("li");
      li.textContent = typeof e === "string" ? e : `${e.path || "?"} · ${e.msg}`;
      els.warnList.appendChild(li);
    }
    for (const w of result.warningDetails || []) {
      const li = document.createElement("li");
      li.textContent = typeof w === "string" ? `! ${w}` : `! ${w.path} · ${w.msg}`;
      els.warnList.appendChild(li);
    }
    setHint(`校验失败 · ${result.errors?.length || 0} 错 — 见告警列表`);
    if (els.metaBox) {
      els.metaBox.classList.remove("empty");
      els.metaBox.textContent = formatValidationReport(result) || result.errors.join("\n");
    }
    paint();
    return false;
  }
  const pkg = result.package;
  if (!pkg.nestResult?.placements?.length) {
    const nest = runShelfPack(pkg);
    if (nest.unplacedCount) {
      result.warnings = [
        ...(result.warnings || []),
        `${nest.unplacedCount} panels unplaced by browser_shelf_v0`,
      ];
    }
  }
  state.package = pkg;
  state.warnings = result.warnings;
  state.selectedPanelId = pkg.panels?.[0]?.panelId || null;
  state.sheetIndex = 0;
  setHint(
    pkg.nestResult?.engine === "browser_shelf_v0"
      ? `shelf pack · ${pkg.nestResult.sheetCount || 1} sheet(s)`
      : `loaded · ${pkg.nestResult?.sheetCount || 1} sheet(s)`
  );
  renderAll();
  return true;
}

async function parseImportFile(file) {
  const name = (file.name || "").toLowerCase();
  const text = await file.text();
  if (name.endsWith(".dxf")) {
    const d = dxfToCutPackage(text, { designName: file.name });
    if (!d.ok) throw new Error(d.error || "DXF import failed");
    return { package: d.package, session: {}, label: file.name };
  }
  if (name.endsWith(".svg")) {
    const d = svgToCutPackage(text, { designName: file.name });
    if (!d.ok) throw new Error(d.error || "SVG import failed");
    return { package: d.package, session: {}, label: file.name };
  }
  let json;
  try {
    json = JSON.parse(text);
  } catch (err) {
    throw new Error(`JSON 解析失败: ${err.message || err}`);
  }
  const parsed = parseProjectDoc(json);
  if (!parsed.ok) throw new Error(parsed.error);
  return { package: parsed.package, session: parsed.session || {}, label: file.name };
}

async function importFiles(fileList) {
  const files = [...(fileList || [])].filter(Boolean);
  if (!files.length) return;
  try {
    // woodjob multi-file drop/folder: manifest.json + parts.json (+ …)
    const jsonNamed = [];
    for (const f of files) {
      const name = (f.name || "").toLowerCase();
      if (!name.endsWith(".json")) continue;
      try {
        jsonNamed.push({ name: f.name, json: JSON.parse(await f.text()) });
      } catch {
        /* skip non-json */
      }
    }
    if (jsonNamed.length >= 2) {
      const map = {};
      for (const row of jsonNamed) map[row.name] = row.json;
      const assembled = tryAssembleWoodJobFromFileMap(map);
      if (assembled) {
        if (!assembled.ok) {
          setHint(`WoodJob 校验失败: ${(assembled.errors || []).map((e) => e.msg || e).join("; ")}`);
          return;
        }
        if (!applyPackage(assembled.package)) return;
        rememberCurrentAsRecent(assembled.package.jobId || "woodjob");
        setHint(`已导入 woodjob · ${assembled.package.panels.length} 板件`);
        return;
      }
    }

    const parsedList = [];
    for (const f of files) {
      parsedList.push(await parseImportFile(f));
    }
    if (parsedList.length === 1) {
      const one = parsedList[0];
      if (!applyPackage(one.package)) return;
      applySession(one.session);
      rememberCurrentAsRecent(
        one.label.replace(/\.cut\.json$/i, "").replace(/\.json$/i, "").replace(/\.dxf$/i, "") || "import"
      );
      setHint(`已导入 · ${one.label}`);
      return;
    }
    const pkgs = parsedList.map((p) => p.package);
    const merged = mergeCutPackages(pkgs);
    if (!applyPackage(merged)) return;
    // session from first project-like file only
    const withSession = parsedList.find((p) => p.session && Object.keys(p.session).length);
    if (withSession) applySession(withSession.session);
    rememberCurrentAsRecent(`merge_${files.length}`);
    setHint(`已合并导入 ${files.length} 个文件 · ${merged.panels.length} 板件`);
  } catch (err) {
    setHint(`导入失败: ${err.message || err}`);
  }
}

async function loadDemo() {
  setHint("加载示例…");
  const res = await fetch("/samples/demo_cut_package.json");
  if (!res.ok) throw new Error(`demo fetch ${res.status}`);
  applyPackage(await res.json());
}

async function loadUnplaced() {
  setHint("加载 unplaced…");
  const res = await fetch("/samples/demo_unplaced.json");
  if (!res.ok) throw new Error(`unplaced fetch ${res.status}`);
  applyPackage(await res.json());
}

els.fileInput.addEventListener("change", async (ev) => {
  const files = ev.target.files;
  await importFiles(files);
  ev.target.value = "";
});

function stopFolderWatch() {
  if (state.folderWatch?.timer) clearInterval(state.folderWatch.timer);
  state.folderWatch = null;
  if (els.importFolderBtn) els.importFolderBtn.textContent = "文件夹监视";
}

async function scanFolderHandle(dirHandle) {
  const seen = state.folderWatch?.seen || new Map();
  const fresh = [];
  for await (const [, handle] of dirHandle.entries()) {
    if (handle.kind !== "file") continue;
    const name = handle.name.toLowerCase();
    if (
      !(
        name.endsWith(".json") ||
        name.endsWith(".dxf") ||
        name.endsWith(".svg") ||
        name.endsWith(".cut.json")
      )
    ) {
      continue;
    }
    const file = await handle.getFile();
    const key = `${handle.name}:${file.lastModified}:${file.size}`;
    if (seen.get(handle.name) !== key) {
      seen.set(handle.name, key);
      fresh.push(file);
    }
  }
  if (state.folderWatch) state.folderWatch.seen = seen;
  return fresh;
}

els.importFolderBtn?.addEventListener("click", async () => {
  if (state.folderWatch) {
    stopFolderWatch();
    setHint("已停止文件夹监视");
    return;
  }
  if (typeof window.showDirectoryPicker !== "function") {
    setHint("当前浏览器不支持文件夹选择 — 用 Import 多选或拖放");
    return;
  }
  try {
    const dir = await window.showDirectoryPicker();
    state.folderWatch = { dirHandle: dir, timer: null, seen: new Map() };
    const first = await scanFolderHandle(dir);
    if (first.length) await importFiles(first);
    else setHint("文件夹为空 — 监视中，放入 JSON/DXF/SVG 将自动导入");
    if (els.importFolderBtn) els.importFolderBtn.textContent = "停止监视";
    state.folderWatch.timer = setInterval(async () => {
      if (!state.folderWatch?.dirHandle) return;
      try {
        const files = await scanFolderHandle(state.folderWatch.dirHandle);
        if (files.length) {
          setHint(`监视到 ${files.length} 个更新…`);
          await importFiles(files);
        }
      } catch (err) {
        setHint(`监视出错: ${err.message || err}`);
        stopFolderWatch();
      }
    }, 2500);
    setHint(`监视中 · ${dir.name} · 再点按钮停止`);
  } catch (err) {
    if (err?.name === "AbortError") return;
    setHint(`文件夹导入失败: ${err.message || err}`);
  }
});

// Drag-drop import onto app shell
function isFileDrag(ev) {
  return [...(ev.dataTransfer?.types || [])].includes("Files");
}
window.addEventListener("dragover", (ev) => {
  if (!isFileDrag(ev)) return;
  ev.preventDefault();
  ev.dataTransfer.dropEffect = "copy";
  document.body.classList.add("drag-import");
});
window.addEventListener("dragleave", (ev) => {
  if (ev.relatedTarget == null || ev.target === document.body) {
    document.body.classList.remove("drag-import");
  }
});
window.addEventListener("drop", (ev) => {
  document.body.classList.remove("drag-import");
  if (!isFileDrag(ev)) return;
  ev.preventDefault();
  importFiles(ev.dataTransfer.files);
});

els.loadDemoBtn.addEventListener("click", () => {
  loadDemo().catch((err) => setHint(`示例失败: ${err.message || err}`));
});

els.loadUnplacedBtn.addEventListener("click", () => {
  loadUnplaced().catch((err) => setHint(`unplaced失败: ${err.message || err}`));
});

els.repackBtn.addEventListener("click", repack);
els.lockPlaceBtn?.addEventListener("click", toggleLockSelected);

els.exportBtn.addEventListener("click", downloadPackage);

els.exportOpsBtn.addEventListener("click", downloadOps);

els.exportNcBtn.addEventListener("click", () => {
  downloadNc().catch((err) => setHint(`NC失败: ${err.message || err}`));
});

els.exportDxfBtn?.addEventListener("click", downloadDxf);
els.exportJobBtn?.addEventListener("click", downloadJobSheet);
els.saveProjectBtn?.addEventListener("click", downloadProject);
els.clearRecentBtn?.addEventListener("click", () => {
  clearRecent();
  renderRecentList();
  setHint("已清空最近工程");
});

els.machineSelect?.addEventListener("change", () => {
  state.machineId = els.machineSelect.value;
  const profile = effectiveProfile();
  const tool = (library.tools || []).find((t) => t.machineId === state.machineId);
  if (tool) {
    state.selectedToolId = tool.id;
    fillToolForm(tool);
  } else {
    fillToolForm({
      diameterMm: profile.toolDiameterMm,
      spindleRpm: profile.spindleRpm,
      feedXyMmMin: profile.feedXyMmMin,
      feedZMmMin: profile.feedZMmMin,
    });
  }
  applyMachineProfileToToolpathUi(profile);
  renderToolLib();
  renderLibraryLists();
  renderStatusBar();
  setHint(`机型 · ${profile.name} · Ø${profile.toolDiameterMm}`);
  if (state.showToolpath) scheduleToolpathRefresh();
  if (state.viewMode === "out") refreshOutPreview();
});

els.geomMoveBtn.addEventListener("click", () => {
  // Nest: nudge sheet placement (what "移动" usually means on the nest canvas).
  // Geom: nudge holes/grooves only — translating the whole panel is invisible
  // because the Geom camera auto-frames the bbox.
  if (state.viewMode === "nest") {
    const id = state.selectedPanelId;
    const place = findPlacement(id);
    if (!place) return;
    const resolved = commitNestOffset(
      id,
      (Number(place.offsetX) || 0) + 10,
      Number(place.offsetY) || 0,
      place.rotationDeg,
      { offsetX: Number(place.offsetX) || 0, offsetY: Number(place.offsetY) || 0 }
    );
    if (resolved?.blocked) {
      setHint(`还原 ${id} · 目标位置重叠`);
    } else if (resolved) {
      setHint(`move ${id} → (${resolved.offsetX}, ${resolved.offsetY})`);
    }
    renderAll();
    return;
  }
  applyGeomEdit((g) => translateFeatures(g, 10, 0));
});
els.geomRotBtn.addEventListener("click", () => {
  applyGeomEdit((g) => rotatePanel(g, 90));
});
els.geomHoleBtn.addEventListener("click", () => {
  applyGeomEdit((g) => {
    const b = panelBbox(g);
    return addVerticalHole(g, {
      x: b.minX + Math.min(40, b.width / 2),
      y: b.minY + Math.min(40, b.height / 2),
      diameterMm: 8,
      depthMm: Math.min(12, g.thicknessMm),
    });
  });
});
els.geomGrooveBtn.addEventListener("click", () => {
  applyGeomEdit((g) => {
    const b = panelBbox(g);
    const y = b.minY + Math.min(20, b.height / 2);
    return addVerticalGroove(g, {
      path: [
        [b.minX + 5, y],
        [b.minX + b.width - 5, y],
      ],
      widthMm: 6,
      depthMm: 8,
    });
  });
});
els.geomNewBtn.addEventListener("click", () => {
  const g = createRectPanel({ panelId: `N${Date.now() % 10000}`, widthMm: 400, heightMm: 300 });
  const panel = toCutPackagePanel(g, {
    boardType: "carcass",
    material: "oak",
    colorTag: "oak",
  });
  const pkg = {
    schema: "cabinetnc.cut-package",
    schemaVersion: 1,
    source: { app: "CabinetNC Cut", designName: "geom", exportId: `geom-${Date.now()}` },
    units: "mm",
    sheets: [
      {
        sheetId: "S1",
        material: "oak",
        thicknessMm: 18,
        widthMm: 1220,
        lengthMm: 2440,
      },
    ],
    panels: [panel],
    nestSettings: { spacingMm: 12, borderMm: 15, allowRotation: true },
  };
  applyPackage(pkg);
  setHint(`new rect ${g.panelId}`);
});

els.sheetSelect.addEventListener("change", () => {
  state.sheetIndex = Number(els.sheetSelect.value) || 0;
  paint();
});

window.addEventListener("keydown", (ev) => {
  if (ev.target && /^(INPUT|TEXTAREA|SELECT)$/.test(ev.target.tagName)) return;
  const stageMap = { "1": "geom", "2": "nest", "3": "cam", "4": "out" };
  if (stageMap[ev.key]) {
    setViewMode(stageMap[ev.key]);
    ev.preventDefault();
    return;
  }
  if (!state.package) return;
  const opts = sheetOptions(state.package);
  if (opts.length < 2) return;
  if (ev.key === "ArrowLeft") {
    state.sheetIndex = (state.sheetIndex - 1 + opts.length) % opts.length;
    els.sheetSelect.value = String(state.sheetIndex);
    paint();
    ev.preventDefault();
  } else if (ev.key === "ArrowRight") {
    state.sheetIndex = (state.sheetIndex + 1) % opts.length;
    els.sheetSelect.value = String(state.sheetIndex);
    paint();
    ev.preventDefault();
  }
});

window.addEventListener("resize", () => paint());

els.canvas.style.cursor = "grab";
els.canvas.addEventListener("pointerdown", onCanvasPointerDown);
els.canvas.addEventListener("pointermove", onCanvasPointerMove);
els.canvas.addEventListener("pointerup", onCanvasPointerUp);
els.canvas.addEventListener("pointercancel", onCanvasPointerUp);

els.viewGeomBtn?.addEventListener("click", () => setViewMode("geom"));
els.viewNestBtn?.addEventListener("click", () => setViewMode("nest"));
els.viewCamBtn?.addEventListener("click", () => setViewMode("cam"));
els.viewOutBtn?.addEventListener("click", () => setViewMode("out"));

els.applyNestSettingsBtn?.addEventListener("click", applyNestSettingsFromUi);
els.nestVerifyBtn?.addEventListener("click", () => {
  runNestVerify().catch((err) => setHint(`校验失败: ${err.message || err}`));
});
els.applyStockBtn?.addEventListener("click", applyStockFromUi);
els.lockAllBtn?.addEventListener("click", () => setAllPlacementsLocked(true));
els.unlockAllBtn?.addEventListener("click", () => setAllPlacementsLocked(false));
els.matAddBtn?.addEventListener("click", addMaterialFromUi);
els.toolSaveBtn?.addEventListener("click", saveSelectedToolFromUi);
els.toolApplyBtn?.addEventListener("click", applyToolToMachineFromUi);
els.camApplyBtn?.addEventListener("click", applyCamParamsFromUi);
els.camSimPrevBtn?.addEventListener("click", () => {
  stopCamSimPlay();
  camSimMove(-1);
});
els.camSimNextBtn?.addEventListener("click", () => {
  stopCamSimPlay();
  camSimMove(1);
});
els.camSimPlayBtn?.addEventListener("click", toggleCamSimPlay);
els.postApplyBtn?.addEventListener("click", applyPostWizardFromUi);

if (els.allowOverlapChk) {
  els.allowOverlapChk.checked = !!state.allowOverlap;
  els.allowOverlapChk.addEventListener("change", () => {
    state.allowOverlap = !!els.allowOverlapChk.checked;
    if (state.viewMode === "nest") {
      setHint(
        state.allowOverlap ? "Nest: 拖摆位（允许重叠）" : "Nest: 拖动可穿过 · 放下时不可重叠"
      );
    }
  });
}

if (els.toolpathChk) {
  els.toolpathChk.checked = !!state.showToolpath;
  els.toolpathChk.addEventListener("change", () => {
    state.showToolpath = !!els.toolpathChk.checked;
    if (!state.showToolpath && !state.showHoleBool) {
      state.toolpath = null;
      setToolpathMeta("—", true);
      paint();
    } else {
      scheduleToolpathRefresh();
    }
  });
}
if (els.holeBoolChk) {
  els.holeBoolChk.checked = !!state.showHoleBool;
  els.holeBoolChk.addEventListener("change", () => {
    state.showHoleBool = !!els.holeBoolChk.checked;
    if (!state.showToolpath && !state.showHoleBool) {
      state.toolpath = null;
      setToolpathMeta("—", true);
      paint();
    } else {
      scheduleToolpathRefresh();
    }
  });
}
els.toolpathDelta?.addEventListener("change", () => {
  state.toolpathDeltaMm = toolpathDelta();
  if (state.showToolpath || state.showHoleBool) scheduleToolpathRefresh();
});

fillMachineSelect();
applyMachineProfileToToolpathUi(effectiveProfile());

setViewMode("geom");
loadDemo().catch(() => setHint("等待导入 cut package JSON"));
