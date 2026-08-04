/** Built-in machine profiles for NC export (mm). */

export const MACHINE_PROFILES = [
  {
    id: "generic_cnc_mm",
    name: "Generic CNC (mm)",
    dialect: "generic",
    programEnd: "M2",
    safeZMm: 5,
    feedXyMmMin: 3000,
    feedZMmMin: 500,
    spindleRpm: 18000,
    toolDiameterMm: 12,
    offsetContours: true,
    contourDepthMm: 18,
    contourStepdownMm: 0,
    drillPeckMm: 0,
    enableContour: true,
    enableDrill: true,
    enableGroove: true,
  },
  {
    id: "nesting_router_6",
    name: "Nesting router Ø6",
    dialect: "generic",
    programEnd: "M2",
    safeZMm: 8,
    feedXyMmMin: 4000,
    feedZMmMin: 800,
    spindleRpm: 18000,
    toolDiameterMm: 6,
    offsetContours: true,
    contourDepthMm: 18,
    contourStepdownMm: 0,
    drillPeckMm: 0,
    enableContour: true,
    enableDrill: true,
    enableGroove: true,
  },
  {
    id: "fanuc_like_m30",
    name: "Fanuc-like (M30 end)",
    dialect: "fanuc_like",
    programEnd: "M30",
    safeZMm: 10,
    feedXyMmMin: 2500,
    feedZMmMin: 400,
    spindleRpm: 16000,
    toolDiameterMm: 10,
    offsetContours: true,
    contourDepthMm: 18,
    contourStepdownMm: 0,
    drillPeckMm: 0,
    enableContour: true,
    enableDrill: true,
    enableGroove: true,
  },
  {
    id: "drill_only_stub",
    name: "Drill-focused (no contour offset)",
    dialect: "generic",
    programEnd: "M2",
    safeZMm: 5,
    feedXyMmMin: 2000,
    feedZMmMin: 400,
    spindleRpm: 12000,
    toolDiameterMm: 8,
    offsetContours: false,
    enableContour: false,
    enableDrill: true,
    enableGroove: false,
  },
];

export function getMachineProfile(id) {
  return MACHINE_PROFILES.find((p) => p.id === id) || MACHINE_PROFILES[0];
}

export function toolRadiusMm(profile) {
  return Math.max(0, Number(profile?.toolDiameterMm) || 0) / 2;
}

/** Merge runtime overrides onto a built-in profile (MakerHub-depth tool edits). */
export function withProfileOverrides(profile, overrides) {
  if (!profile) return profile;
  if (!overrides || typeof overrides !== "object") return profile;
  const next = { ...profile };
  for (const key of [
    "toolDiameterMm",
    "feedXyMmMin",
    "feedZMmMin",
    "spindleRpm",
    "safeZMm",
    "contourDepthMm",
    "contourStepdownMm",
    "drillPeckMm",
    "offsetContours",
    "programEnd",
    "dialect",
    "originNote",
    "enableContour",
    "enableDrill",
    "enableGroove",
  ]) {
    if (overrides[key] != null && overrides[key] !== "") next[key] = overrides[key];
  }
  return next;
}
