/** Import helpers checks. */
import { pathToFileURL } from "node:url";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(fileURLToPath(import.meta.url));
const {
  validateCutPackage,
  normalizeCutPackage,
  formatValidationReport,
  mergeCutPackages,
} = await import(pathToFileURL(join(root, "..", "src", "package.js")).href);
const { buildProjectDoc, parseProjectDoc } = await import(
  pathToFileURL(join(root, "..", "src", "project.js")).href
);
const { dxfToCutPackage, expandBulge } = await import(
  pathToFileURL(join(root, "..", "src", "dxf_import.js")).href
);
const { svgToCutPackage, pathDToPolylines, parseTransform, applyMatrix } = await import(
  pathToFileURL(join(root, "..", "src", "svg_import.js")).href
);

const errors = [];
function assert(cond, msg) {
  if (!cond) errors.push(msg);
}

// soft schema
const loose = normalizeCutPackage({
  panels: [
    {
      panelId: "A",
      outline: {
        points: [
          [0, 0],
          [10, 0],
          [10, 10],
          [0, 10],
        ],
      },
      features: [],
    },
  ],
});
const v = validateCutPackage(loose);
assert(v.ok, "loose panels ok");
assert(v.package.schema === "cabinetnc.cut-package", "schema injected");
assert(v.warnings.some((w) => String(w).includes("schema") || String(w).includes("features")), "warns");

const bad = validateCutPackage({
  schema: "cabinetnc.cut-package",
  schemaVersion: 1,
  panels: [{ panelId: "B", outline: { points: [[0, 0]] } }],
});
assert(!bad.ok, "bad outline");
assert(formatValidationReport(bad).includes("outline.points"), "field path");

const m = mergeCutPackages([
  v.package,
  {
    schema: "cabinetnc.cut-package",
    schemaVersion: 1,
    panels: [
      {
        panelId: "A",
        outline: {
          points: [
            [0, 0],
            [5, 0],
            [5, 5],
            [0, 5],
          ],
        },
        features: [{ kind: "holeVertical", id: "h1", x: 1, y: 1, diameterMm: 5, depthMm: 10 }],
      },
    ],
  },
]);
assert(m.panels.length === 2, "merge panels");
assert(m.panels[1].panelId === "A_m1", "clash rename");

const doc = buildProjectDoc(v.package, {
  machineId: "nesting_router_6",
  machineOverrides: { nesting_router_6: { contourStepdownMm: 6 } },
});
assert(doc.session.machineOverrides.nesting_router_6.contourStepdownMm === 6, "ov save");
const round = parseProjectDoc(doc);
assert(round.session.machineOverrides.nesting_router_6.contourStepdownMm === 6, "ov load");

const dxf = `0
SECTION
2
ENTITIES
0
LWPOLYLINE
70
1
10
0
20
0
10
100
20
0
10
100
20
50
10
0
20
50
0
ENDSEC
0
EOF
`;
const d = dxfToCutPackage(dxf);
assert(d.ok && d.package.panels.length === 1, "dxf panel");
assert(d.package.panels[0].outline.points.length >= 3, "dxf pts");

const bulgePts = expandBulge([0, 0], [10, 0], 1, 8);
assert(bulgePts.length >= 2, "bulge samples");

const circDxf = `0
SECTION
2
ENTITIES
0
CIRCLE
10
0
20
0
40
50
0
ENDSEC
0
EOF
`;
const circ = dxfToCutPackage(circDxf);
assert(circ.ok && circ.package.panels[0].outline.points.length >= 8, "circle panel");

const blockDxf = `0
SECTION
2
BLOCKS
0
BLOCK
2
MYBLK
0
LWPOLYLINE
70
1
10
0
20
0
10
20
20
0
10
20
20
10
10
0
20
10
0
ENDBLK
0
ENDSEC
0
SECTION
2
ENTITIES
0
INSERT
2
MYBLK
10
100
20
50
0
ENDSEC
0
EOF
`;
const blk = dxfToCutPackage(blockDxf);
assert(blk.ok && blk.package.panels.length >= 1, "block insert explode");
assert(blk.package.panels[0].outline.points.length >= 3, "insert ring");

const svg = `<svg xmlns="http://www.w3.org/2000/svg">
  <rect x="0" y="0" width="100" height="40"/>
  <polygon points="0,0 30,0 30,20 0,20"/>
  <path d="M0 0 L50 0 L50 25 L0 25 Z"/>
</svg>`;
const s = svgToCutPackage(svg);
assert(s.ok && s.package.panels.length >= 3, "svg panels");
assert(pathDToPolylines("M0 0 L10 0 L10 10 Z").length === 1, "path d");

const tf = parseTransform("translate(10 5) scale(2)");
const moved = applyMatrix(
  [
    [0, 0],
    [1, 0],
    [1, 1],
    [0, 1],
  ],
  tf
);
assert(Math.abs(moved[0][0] - 10) < 1e-6 && Math.abs(moved[1][0] - 12) < 1e-6, "svg transform");

const svgUse = `<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink">
  <rect id="unit" x="0" y="0" width="10" height="10"/>
  <use href="#unit" x="30" y="0"/>
</svg>`;
const su = svgToCutPackage(svgUse);
assert(su.ok && su.package.panels.length >= 2, "svg use");

if (errors.length) {
  console.error("FAIL import", errors);
  process.exit(1);
}
console.log(
  "OK import",
  `merge=${m.panels.length}`,
  `dxf=${d.package.panels[0].panelId}`,
  `svg=${s.package.panels.length}`,
  `block=${blk.package.panels.length}`,
  `use=${su.package.panels.length}`
);
