/**
 * Workshop libraries (MakerHub-depth M2): materials + tools.
 * Persists to localStorage; can merge into project session.
 */
import { MACHINE_PROFILES } from "./machine.js";

const STORAGE_KEY = "cabinetnc.library.v1";

export const DEFAULT_MATERIALS = [
  { id: "mat_oak", name: "oak", thicknessMm: 18, densityHint: "板式" },
  { id: "mat_mdf", name: "mdf", thicknessMm: 18, densityHint: "板式" },
  { id: "mat_ply", name: "plywood", thicknessMm: 15, densityHint: "多层" },
];

export function toolsFromMachines(profiles = MACHINE_PROFILES) {
  return (profiles || []).map((p) => ({
    id: `tool_${p.id}`,
    name: p.name,
    machineId: p.id,
    diameterMm: Number(p.toolDiameterMm) || 0,
    feedXyMmMin: Number(p.feedXyMmMin) || 0,
    feedZMmMin: Number(p.feedZMmMin) || 0,
    spindleRpm: Number(p.spindleRpm) || 0,
    offsetContours: Boolean(p.offsetContours),
  }));
}

export function defaultLibrary() {
  return {
    schema: "cabinetnc.library",
    schemaVersion: 1,
    materials: DEFAULT_MATERIALS.map((m) => ({ ...m })),
    tools: toolsFromMachines(),
  };
}

export function loadLibrary() {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return defaultLibrary();
    const j = JSON.parse(raw);
    if (!j || j.schema !== "cabinetnc.library") return defaultLibrary();
    return {
      ...defaultLibrary(),
      materials: Array.isArray(j.materials) ? j.materials : defaultLibrary().materials,
      tools: Array.isArray(j.tools) ? j.tools : defaultLibrary().tools,
    };
  } catch {
    return defaultLibrary();
  }
}

export function saveLibrary(lib) {
  const doc = {
    schema: "cabinetnc.library",
    schemaVersion: 1,
    materials: lib?.materials || [],
    tools: lib?.tools || [],
    savedAt: new Date().toISOString(),
  };
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(doc));
  } catch {
    /* private mode */
  }
  return doc;
}

export function upsertMaterial(lib, mat) {
  const next = {
    ...lib,
    materials: [...(lib.materials || [])],
  };
  const id = mat.id || `mat_${Date.now()}`;
  const row = {
    id,
    name: String(mat.name || "material").trim() || "material",
    thicknessMm: Number(mat.thicknessMm) || 18,
    densityHint: mat.densityHint || "",
  };
  const i = next.materials.findIndex((m) => m.id === id);
  if (i >= 0) next.materials[i] = row;
  else next.materials.push(row);
  return next;
}

export function upsertTool(lib, tool) {
  const next = { ...lib, tools: [...(lib.tools || [])] };
  const id = tool.id || `tool_${Date.now()}`;
  const row = {
    id,
    name: String(tool.name || "tool").trim() || "tool",
    machineId: tool.machineId || null,
    diameterMm: Number(tool.diameterMm) || 0,
    feedXyMmMin: Number(tool.feedXyMmMin) || 0,
    feedZMmMin: Number(tool.feedZMmMin) || 0,
    spindleRpm: Number(tool.spindleRpm) || 0,
    offsetContours: Boolean(tool.offsetContours),
  };
  const i = next.tools.findIndex((t) => t.id === id);
  if (i >= 0) next.tools[i] = row;
  else next.tools.push(row);
  return next;
}

/** Node-safe summary (no localStorage) for checks. */
export function librarySummary(lib) {
  return {
    materialCount: (lib?.materials || []).length,
    toolCount: (lib?.tools || []).length,
    materialNames: (lib?.materials || []).map((m) => m.name),
  };
}
