import { pathToFileURL } from "node:url";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(fileURLToPath(import.meta.url));
const geom = await import(pathToFileURL(join(root, "..", "src", "geom", "index.js")).href);

const errors = [];
function assert(cond, msg) {
  if (!cond) errors.push(msg);
}

let p = geom.createRectPanel({ panelId: "G1", widthMm: 100, heightMm: 50 });
assert(Math.abs(geom.area(p.outline.points) - 5000) < 1e-6, "rect area");
assert(geom.panelBbox(p).width === 100, "bbox w");

p = geom.translatePanel(p, 10, 20);
assert(geom.panelBbox(p).minX === 10 && geom.panelBbox(p).minY === 20, "translate");

p = geom.rotatePanel(p, 90);
const box = geom.panelBbox(p);
assert(Math.abs(box.width - 50) < 1e-6 && Math.abs(box.height - 100) < 1e-6, "rotate90 size");

p = geom.addVerticalHole(p, { x: box.minX + 10, y: box.minY + 10, diameterMm: 8, depthMm: 12 });
assert(p.features.some((f) => f.kind === "holeVertical"), "hole feature");
assert(p.holes.length === 1, "hole ring");

p = geom.addVerticalGroove(p, {
  path: [
    [box.minX, box.minY + 5],
    [box.minX + box.width, box.minY + 5],
  ],
  widthMm: 6,
  depthMm: 8,
});
assert(p.features.some((f) => f.kind === "grooveVertical"), "groove");

const pkgPanel = geom.toCutPackagePanel(p);
assert(pkgPanel.outline.points.length >= 3, "export outline");
assert(pkgPanel.features.length === 2, "export features");

const roundTrip = geom.fromCutPackagePanel(pkgPanel);
assert(roundTrip.features.length === 2, "import features");
assert(roundTrip.holes.length === 1, "import hole rings");

const moved = geom.translatePanel(roundTrip, -5, 0);
const pkg = {
  schema: "cabinetnc.cut-package",
  schemaVersion: 1,
  panels: [pkgPanel],
  nestResult: { placements: [{ panelId: "G1", sheetIndex: 0 }] },
};
const written = geom.writeBackToPackage(pkg, moved);
assert(!written.nestResult, "writeBack clears nest");
assert(written.panels[0].panelId === "G1", "writeBack id");

const off = geom.offsetRect(geom.rectOutline(100, 50), 5);
assert(Math.abs(geom.bbox(off).width - 110) < 1e-6, "offset rect");

// resize keeping features
let r = geom.createRectPanel({ panelId: "R1", widthMm: 200, heightMm: 100 });
r = geom.addVerticalHole(r, { x: 50, y: 25, diameterMm: 10, depthMm: 8, id: "H9" });
r = geom.resizeRectKeepingFeatures(r, 400, 200);
assert(Math.abs(geom.panelBbox(r).width - 400) < 1e-6, "resize w");
assert(Math.abs(geom.panelBbox(r).height - 200) < 1e-6, "resize h");
const hole = r.features.find((f) => f.id === "H9");
assert(Math.abs(hole.x - 100) < 1e-6 && Math.abs(hole.y - 50) < 1e-6, "hole scaled");

r = geom.moveHole(r, "H9", 30, 40);
assert(r.features.find((f) => f.id === "H9").x === 30, "moveHole x");

{
  const nudged = geom.translateFeatures(r, 10, 0);
  assert(nudged.features.find((f) => f.id === "H9").x === 40, "translateFeatures hole");
  assert(Math.abs(geom.panelBbox(nudged).minX - geom.panelBbox(r).minX) < 1e-9, "outline unmoved");
}

r = geom.addVerticalGroove(r, {
  id: "G9",
  path: [
    [0, 10],
    [100, 10],
  ],
});
r = geom.moveGroovePoint(r, "G9", 1, 80, 12);
assert(r.features.find((f) => f.id === "G9").path[1][0] === 80, "groove pt");

const resized = geom.resizeFromEdges(r, { minX: 0, minY: 0, maxX: 300, maxY: 150 });
assert(Math.abs(geom.panelBbox(resized).width - 300) < 1e-6, "resizeFromEdges");
assert(geom.isAxisAlignedRect(resized), "still rect");

// --- P1 edges / MakerHub ---
const mh = [
  {
    StartPoint: { X: 0, Y: 0, Z: 0 },
    EndPoint: { X: 100, Y: 0, Z: 0 },
    Angle: 0,
  },
  {
    StartPoint: { X: 100, Y: 0, Z: 0 },
    EndPoint: { X: 100, Y: 50, Z: 0 },
    Angle: 0,
  },
  {
    StartPoint: { X: 100, Y: 50, Z: 0 },
    EndPoint: { X: 0, Y: 50, Z: 0 },
    Angle: 0,
  },
  {
    StartPoint: { X: 0, Y: 50, Z: 0 },
    EndPoint: { X: 0, Y: 0, Z: 0 },
    Angle: 0,
  },
];
const mhPanel = geom.panelFromMakerHubOutline(mh, { panelId: "MH", thicknessMm: 18 });
assert(mhPanel.outline.edges.length === 4, "mh edges");
assert(Math.abs(geom.area(mhPanel.outline.points) - 5000) < 1.0, "mh area");

// quarter-circle bulge: start (1,0) end (0,1) angle +90° → near unit quarter
const arcPts = geom.tessellateArc(1, 0, 0, 1, 90, 12);
assert(arcPts.length >= 2, "arc tess");
const last = arcPts[arcPts.length - 1];
assert(Math.abs(last[0]) < 1e-6 && Math.abs(last[1] - 1) < 1e-6, "arc end");
// midpoint of tessellation should be near (cos45, sin45) if center at origin
const mid = arcPts[Math.floor(arcPts.length / 2) - 1] || arcPts[0];
assert(mid[0] > 0.2 && mid[1] > 0.2, "arc bows into Q1");

let edged = geom.createRectPanel({ widthMm: 100, heightMm: 50 });
assert(edged.outline.edges.length === 4, "rect edges");
edged = geom.setEdgeAngle(edged.outline.edges, 0, 45);
// setEdgeAngle returns edges array — apply via sync
let pArc = geom.createRectPanel({ panelId: "A1", widthMm: 100, heightMm: 50 });
pArc.outline.edges = geom.setEdgeAngle(pArc.outline.edges, 0, 60);
pArc = geom.syncOutlineFromEdges(pArc);
assert(pArc.outline.edges[0].type === "arc", "edge promoted to arc");
assert(pArc.outline.points.length > 5, "arc adds points");

const exported = geom.toCutPackagePanel(pArc);
assert(Array.isArray(exported.outline.edges), "export edges optional");
assert(exported.outline.points.length >= 3, "export points");

const roundEdges = geom.fromCutPackagePanel(exported);
assert(roundEdges.outline.edges[0].type === "arc", "import edges");

assert(
  !geom.polygonsOverlap(
    [
      [0, 0],
      [10, 0],
      [10, 10],
      [0, 10],
    ],
    [
      [20, 20],
      [30, 20],
      [30, 30],
      [20, 30],
    ]
  ),
  "disjoint polys"
);
assert(
  geom.polygonsOverlap(
    [
      [0, 0],
      [10, 0],
      [10, 10],
      [0, 10],
    ],
    [
      [5, 5],
      [15, 5],
      [15, 15],
      [5, 15],
    ]
  ),
  "overlap polys"
);

if (errors.length) {
  console.error("FAIL geom", errors);
  process.exit(1);
}
console.log(
  "OK geom",
  `features=${moved.features.length}`,
  `area=${geom.panelMetrics(moved).areaMm2.toFixed(0)}`,
  `resizeHole=(${hole.x},${hole.y})`,
  `mhEdges=${mhPanel.outline.edges.length}`,
  `arcPts=${pArc.outline.points.length}`
);
