/** Pack unit checks: multi-sheet, outline fallback, rotation, spacing collision. */
import { readFileSync } from "node:fs";
import { pathToFileURL } from "node:url";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const root = dirname(fileURLToPath(import.meta.url));
const {
  shelfPack,
  blfPack,
  packPanels,
  findNestCollisions,
  aabbsConflict,
  placementAabb,
  nestStats,
} = await import(pathToFileURL(join(root, "..", "src", "pack.js")).href);

const errors = [];

function assert(cond, msg) {
  if (!cond) errors.push(msg);
}

// outline-only size (bbox 0)
const outlinePanels = [
  {
    panelId: "A",
    bbox: { widthMm: 0, heightMm: 0 },
    outline: { points: [[0, 0], [100, 0], [100, 50], [0, 50]] },
  },
  {
    panelId: "B",
    bbox: { widthMm: 0, heightMm: 0 },
    outline: { points: [[0, 0], [80, 0], [80, 40], [0, 40]] },
  },
];
const fromOutline = shelfPack(outlinePanels, { widthMm: 1220, lengthMm: 2440 }, 10, 10);
assert(fromOutline.placements.length === 2, "outline fallback should place 2");
assert(fromOutline.unplacedCount === 0, "outline fallback unplaced");

// multi-sheet overflow
const tall = [
  { panelId: "T1", bbox: { widthMm: 400, heightMm: 300 } },
  { panelId: "T2", bbox: { widthMm: 400, heightMm: 300 } },
  { panelId: "T3", bbox: { widthMm: 400, heightMm: 300 } },
];
const multi = shelfPack(tall, { widthMm: 500, lengthMm: 350 }, 10, 10);
assert(multi.sheetCount >= 2, `expected >=2 sheets got ${multi.sheetCount}`);
assert(multi.placements.length === 3, "all tall panels placed");
assert(multi.unplacedCount === 0, "no unplaced on multi");
assert(
  Math.max(...multi.placements.map((p) => p.sheetIndex)) + 1 === multi.sheetCount,
  "sheetCount vs max index"
);

// too big → unplaced
const huge = shelfPack(
  [{ panelId: "X", bbox: { widthMm: 2000, heightMm: 2000 } }],
  { widthMm: 1220, lengthMm: 2440 },
  12,
  15
);
assert(huge.placements.length === 0, "huge should not place");
assert(huge.unplacedCount === 1, "huge unplaced");

// fixture: demo_unplaced.json
const fixture = JSON.parse(
  readFileSync(join(root, "..", "public", "samples", "demo_unplaced.json"), "utf8")
);
const fromFixture = shelfPack(
  fixture.panels,
  fixture.sheets[0],
  fixture.nestSettings?.spacingMm,
  fixture.nestSettings?.borderMm
);
assert(fromFixture.placements.some((p) => p.panelId === "OK1"), "OK1 placed");
assert(fromFixture.unplaced.includes("HUGE1"), "HUGE1 unplaced from fixture");
assert(fromFixture.unplacedCount === 1, "fixture unplacedCount");

// rotation: tall strip only fits after 90° on a wide-short sheet
const strip = [{ panelId: "S", bbox: { widthMm: 100, heightMm: 800 } }];
const noRot = shelfPack(strip, { widthMm: 900, lengthMm: 200 }, 10, 10, {
  allowRotation: false,
});
assert(noRot.unplaced.includes("S"), "strip unplaced without rotation");
const withRot = shelfPack(strip, { widthMm: 900, lengthMm: 200 }, 10, 10, {
  allowRotation: true,
});
assert(withRot.placements.length === 1, "strip placed with rotation");
assert(withRot.placements[0].rotationDeg === 90, "strip rotationDeg 90");
assert(withRot.engine === "browser_shelf_v1", "engine v1 when rotation on");

// grainLocked / rotatable:false must not rotate even if allowRotation
const locked = shelfPack(
  [{ panelId: "L", bbox: { widthMm: 100, heightMm: 800 }, grainLocked: true }],
  { widthMm: 900, lengthMm: 200 },
  10,
  10,
  { allowRotation: true }
);
assert(locked.unplaced.includes("L"), "grainLocked strip stays unplaced");
const noFlag = shelfPack(
  [{ panelId: "N", bbox: { widthMm: 100, heightMm: 800 }, rotatable: false }],
  { widthMm: 900, lengthMm: 200 },
  10,
  10,
  { allowRotation: true }
);
assert(noFlag.unplaced.includes("N"), "rotatable:false strip stays unplaced");

// shelf result must respect spacing (no collisions)
const spaced = shelfPack(
  [
    { panelId: "P1", bbox: { widthMm: 200, heightMm: 100 } },
    { panelId: "P2", bbox: { widthMm: 200, heightMm: 100 } },
  ],
  { widthMm: 1220, lengthMm: 2440 },
  12,
  15
);
assert(
  findNestCollisions(
    [
      { panelId: "P1", bbox: { widthMm: 200, heightMm: 100 }, outline: { points: [[0, 0], [200, 0], [200, 100], [0, 100]] } },
      { panelId: "P2", bbox: { widthMm: 200, heightMm: 100 }, outline: { points: [[0, 0], [200, 0], [200, 100], [0, 100]] } },
    ],
    spaced.placements,
    12
  ).length === 0,
  "shelf pack should have zero spacing collisions"
);

// intentional overlap detection
const panelsOL = [
  {
    panelId: "A",
    outline: { points: [[0, 0], [100, 0], [100, 50], [0, 50]] },
  },
  {
    panelId: "B",
    outline: { points: [[0, 0], [100, 0], [100, 50], [0, 50]] },
  },
];
const overlapHits = findNestCollisions(
  panelsOL,
  [
    { panelId: "A", sheetIndex: 0, offsetX: 0, offsetY: 0, rotationDeg: 0 },
    { panelId: "B", sheetIndex: 0, offsetX: 50, offsetY: 0, rotationDeg: 0 },
  ],
  12
);
assert(overlapHits.length === 1, "detect AABB overlap");

const boxA = placementAabb(panelsOL[0], { offsetX: 0, offsetY: 0, rotationDeg: 0 });
const boxB = placementAabb(panelsOL[1], { offsetX: 112, offsetY: 0, rotationDeg: 0 });
assert(!aabbsConflict(boxA, boxB, 12), "112mm gap clears 12mm spacing");
assert(aabbsConflict(boxA, boxB, 13), "112mm gap fails 13mm spacing");

// resolveNestPlacement (render) — clamp + block on conflict
const { resolveNestPlacement, clampPlacementOnSheet } = await import(
  pathToFileURL(join(root, "..", "src", "render.js")).href
);
const map = new Map(panelsOL.map((p) => [p.panelId, p]));
const others = [
  { panelId: "A", sheetIndex: 0, offsetX: 15, offsetY: 15, rotationDeg: 0 },
];
const okMove = resolveNestPlacement({
  panel: panelsOL[1],
  place: { panelId: "B", sheetIndex: 0, offsetX: 200, offsetY: 15, rotationDeg: 0 },
  panelId: "B",
  otherPlacements: others,
  panelsById: map,
  sheetW: 1220,
  sheetH: 2440,
  spacingMm: 12,
  borderMm: 15,
  fallback: { offsetX: 15, offsetY: 200 },
});
assert(!okMove.blocked && okMove.offsetX === 200, "free move allowed");

const blocked = resolveNestPlacement({
  panel: panelsOL[1],
  place: { panelId: "B", sheetIndex: 0, offsetX: 50, offsetY: 15, rotationDeg: 0 },
  panelId: "B",
  otherPlacements: others,
  panelsById: map,
  sheetW: 1220,
  sheetH: 2440,
  spacingMm: 12,
  borderMm: 15,
  fallback: { offsetX: 15, offsetY: 200 },
});
assert(blocked.blocked && blocked.offsetY === 200, "overlap falls back");

const edge = clampPlacementOnSheet(
  panelsOL[0],
  { offsetX: -50, offsetY: -50, rotationDeg: 0 },
  1220,
  2440,
  15
);
assert(edge.offsetX === 15 && edge.offsetY === 15, "border clamp to 15");

// BLF + packPanels utilization
const mix = [
  { panelId: "A", bbox: { widthMm: 400, heightMm: 300 } },
  { panelId: "B", bbox: { widthMm: 400, heightMm: 300 } },
  { panelId: "C", bbox: { widthMm: 200, heightMm: 200 } },
];
const blf = blfPack(mix, { widthMm: 1220, lengthMm: 2440 }, 12, 15);
assert(blf.engine === "browser_blf_v0", "blf engine");
assert(blf.placements.length === 3 && blf.unplacedCount === 0, "blf places all");
const packed = packPanels(mix, { widthMm: 1220, lengthMm: 2440 }, 12, 15);
assert(packed.stats && packed.stats.utilizationPct > 0, "packPanels stats");
assert(packed.stats.collisionCount === 0, "packPanels no collisions");
const stats = nestStats(mix, blf, 12);
assert(stats.utilizationPct > 5, "util >5%");

// locked placements survive repack
const lockedSeed = [
  {
    panelId: "A",
    sheetIndex: 0,
    offsetX: 100,
    offsetY: 100,
    rotationDeg: 0,
    locked: true,
  },
];
const withLock = packPanels(mix, { widthMm: 1220, lengthMm: 2440 }, 12, 15, {
  lockedPlacements: lockedSeed,
});
const kept = withLock.placements.find((p) => p.panelId === "A");
assert(kept && kept.locked, "locked flag kept");
assert(Math.abs(kept.offsetX - 100) < 1e-6 && Math.abs(kept.offsetY - 100) < 1e-6, "locked coords kept");
assert(withLock.placements.length === 3, "locked pack still places others");

const forceShelf = packPanels(mix, { widthMm: 1220, lengthMm: 2440 }, 12, 15, {
  engine: "shelf",
});
assert(String(forceShelf.engine).includes("shelf"), "force shelf engine");
const forceBlf = packPanels(mix, { widthMm: 1220, lengthMm: 2440 }, 12, 15, {
  engine: "blf",
});
assert(forceBlf.engine === "browser_blf_v0", "force blf engine");

// Notch nest: rect sits in L empty corner — AABB hits, poly does not.
const L1 = {
  panelId: "L1",
  bbox: { widthMm: 100, heightMm: 100 },
  outline: {
    points: [
      [0, 0],
      [100, 0],
      [100, 40],
      [40, 40],
      [40, 100],
      [0, 100],
    ],
  },
};
const L2 = {
  panelId: "L2",
  bbox: { widthMm: 40, heightMm: 40 },
  outline: {
    points: [
      [0, 0],
      [40, 0],
      [40, 40],
      [0, 40],
    ],
  },
};
const nestL = {
  placements: [
    { panelId: "L1", sheetIndex: 0, offsetX: 0, offsetY: 0, rotationDeg: 0 },
    { panelId: "L2", sheetIndex: 0, offsetX: 50, offsetY: 50, rotationDeg: 0 },
  ],
};
const aabbHits = findNestCollisions([L1, L2], nestL.placements, 0);
assert(aabbHits.length >= 1, "AABB sees L/notch overlap");
const polyHits = findNestCollisions([L1, L2], nestL.placements, 0, { poly: true });
assert(polyHits.length === 0, "poly allows notch nest");
const forcePoly = packPanels([L1, L2], { widthMm: 300, lengthMm: 300 }, 0, 10, {
  engine: "poly",
});
assert(String(forcePoly.engine).includes("poly"), "force poly engine");
assert(forcePoly.placements.length === 2, "poly packs L+rect");

if (errors.length) {
  console.error("FAIL", errors);
  process.exit(1);
}
console.log(
  "OK pack",
  `outline=${fromOutline.placements.length}`,
  `sheets=${multi.sheetCount}`,
  `fixtureUnplaced=${fromFixture.unplacedCount}`,
  `rot=${withRot.placements[0]?.rotationDeg}`,
  `blf=${blf.placements.length}`,
  `util=${packed.stats.utilizationPct.toFixed(1)}%`,
  `resolve=ok`
);
