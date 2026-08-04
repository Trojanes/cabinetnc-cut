/** Project document: cut-package + UI session fields. */

export function buildProjectDoc(pkg, session = {}) {
  return {
    schema: "cabinetnc.cut-project",
    schemaVersion: 1,
    savedAt: new Date().toISOString(),
    package: pkg,
    session: {
      machineId: session.machineId || null,
      allowOverlap: Boolean(session.allowOverlap),
      showToolpath: Boolean(session.showToolpath),
      toolpathDeltaMm: Number(session.toolpathDeltaMm) || 6,
      viewMode: session.viewMode || "geom",
      selectedPanelId: session.selectedPanelId || null,
      sheetIndex: Number(session.sheetIndex) || 0,
      library: session.library || null,
      machineOverrides:
        session.machineOverrides && typeof session.machineOverrides === "object"
          ? session.machineOverrides
          : {},
    },
  };
}

export function parseProjectDoc(raw) {
  if (!raw || typeof raw !== "object") return { ok: false, error: "invalid json" };
  if (raw.schema === "cabinetnc.cut-project") {
    return {
      ok: true,
      package: raw.package,
      session: raw.session || {},
    };
  }
  // plain cut-package (schema optional if panels[])
  if (raw.schema === "cabinetnc.cut-package" || Array.isArray(raw.panels)) {
    return { ok: true, package: raw, session: {} };
  }
  return { ok: false, error: "unrecognized project schema — need cut-package / cut-project / panels[]" };
}
