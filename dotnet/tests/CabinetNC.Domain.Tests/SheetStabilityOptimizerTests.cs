using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Nesting;
using CabinetNC.Domain.Parts;

namespace CabinetNC.Domain.Tests;

public class SheetStabilityOptimizerTests
{
    const double SheetW = 1220;
    const double SheetH = 2440;
    const double Border = 15;
    const double Gap = 12;

    static Panel Rect(string id, double w, double h) => new()
    {
        PanelId = id,
        ThicknessMm = 18,
        Outline = new Outline
        {
            Points = [new(0, 0), new(w, 0), new(w, h), new(0, h)],
        },
    };

    static NestPlacement Place(string id, double x, double y, int sheet = 0) => new()
    {
        PanelId = id,
        SheetIndex = sheet,
        OffsetX = x,
        OffsetY = y,
        RotationDeg = 0,
    };

    [Fact]
    public void Classify_strip_vs_large()
    {
        Assert.Equal(SheetStabilityOptimizer.Kind.Strip, SheetStabilityOptimizer.Classify(60, 800, medianArea: 200_000));
        Assert.Equal(SheetStabilityOptimizer.Kind.Large, SheetStabilityOptimizer.Classify(800, 600, medianArea: 200_000));
        Assert.Equal(SheetStabilityOptimizer.Kind.Small, SheetStabilityOptimizer.Classify(200, 180, medianArea: 200_000));
        // 150mm-class verticals are OK on the edge — not treated as dangerous strips.
        Assert.NotEqual(SheetStabilityOptimizer.Kind.Strip, SheetStabilityOptimizer.Classify(150, 2000, medianArea: 300_000));
        Assert.Equal(SheetStabilityOptimizer.Kind.Strip, SheetStabilityOptimizer.Classify(110, 1800, medianArea: 300_000, medianPortraitShort: 175));
        Assert.NotEqual(SheetStabilityOptimizer.Kind.Strip, SheetStabilityOptimizer.Classify(150, 2000, medianArea: 300_000, medianPortraitShort: 175));
        // Horizontal rails are not 竖条 — they must not be dragged toward the width centre.
        Assert.NotEqual(SheetStabilityOptimizer.Kind.Strip, SheetStabilityOptimizer.Classify(589, 65, medianArea: 50_000));
        Assert.NotEqual(SheetStabilityOptimizer.Kind.Strip, SheetStabilityOptimizer.Classify(800, 60, medianArea: 200_000));
    }

    [Fact]
    public void Horizontal_rails_on_edge_are_not_moved()
    {
        var rail = Rect("Rail", 589, 65);
        var block = Rect("Block", 400, 400);
        var strip = Rect("S", 40, 800);
        var placements = new[]
        {
            Place("Block", Border, Border),
            Place("S", SheetW - Border - 40, Border),
            Place("Rail", SheetW - Border - 589, Border + 800 + Gap),
        };
        var railX = SheetW - Border - 589;
        var result = SheetStabilityOptimizer.Optimize(
            [rail, block, strip], placements, 0, SheetW, SheetH, Border, Gap);
        Assert.Equal(railX, result.Placements.Single(p => p.PanelId == "Rail").OffsetX, 6);
        Assert.Empty(NestValidator.FindPolygonCollisions(
            [rail, block, strip], result.Placements, 0));
    }

    [Fact]
    public void Swaps_narrow_right_column_into_middle()
    {
        var panels = new List<Panel>();
        var placements = new List<NestPlacement>();
        var skinnyX = SheetW - Border - 40;
        var x = skinnyX;
        for (var i = 3; i >= 0; i--)
        {
            x -= Gap + 150;
            var id = $"C{i}";
            panels.Add(Rect(id, 150, 1800));
            placements.Add(Place(id, x, Border));
        }
        panels.Add(Rect("S1", 40, 890));
        panels.Add(Rect("S2", 40, 890));
        placements.Add(Place("S1", skinnyX, Border));
        placements.Add(Place("S2", skinnyX, Border + 890 + Gap));

        var before = SheetStabilityOptimizer.CenterNorm(placements[^2], 40, SheetW);
        var result = SheetStabilityOptimizer.Optimize(
            panels, placements, 0, SheetW, SheetH, Border, Gap);

        Assert.True(result.Improved, result.Message);
        var s1 = result.Placements.Single(p => p.PanelId == "S1");
        var after = SheetStabilityOptimizer.CenterNorm(s1, 40, SheetW);
        Assert.True(after > before + 0.15, $"narrow column {before:0.00} → {after:0.00}");
        var rightmost = result.Placements
            .Where(p => p.SheetIndex == 0)
            .Select(p =>
            {
                var panel = panels.Single(x => x.PanelId == p.PanelId);
                var box = NestDrag.Aabb(panel, p.OffsetX, p.OffsetY, p.RotationDeg);
                return (p.PanelId, box.MaxX, W: box.MaxX - box.MinX);
            })
            .OrderByDescending(t => t.MaxX)
            .First();
        Assert.True(rightmost.W >= 140, $"right edge should be a ~150mm board, got {rightmost.PanelId} w={rightmost.W}");
        Assert.Empty(NestValidator.FindAabbCollisions(
            panels.Select(p => new NestPart
            {
                PanelId = p.PanelId,
                WidthMm = NestDrag.SizeRotated(p, 0).W,
                HeightMm = NestDrag.SizeRotated(p, 0).H,
            }).ToList(),
            result.Placements, Gap));
    }

    [Fact]
    public void Remnant_small_spacing_does_not_block_strip_or_hug_top()
    {
        var panels = new List<Panel>();
        var placements = new List<NestPlacement>();
        var skinnyX = SheetW - Border - 40;
        var x = skinnyX;
        for (var i = 3; i >= 0; i--)
        {
            x -= Gap + 150;
            var id = $"C{i}";
            panels.Add(Rect(id, 150, 1800));
            placements.Add(Place(id, x, Border));
        }
        panels.Add(Rect("S1", 40, 890));
        panels.Add(Rect("S2", 40, 890));
        placements.Add(Place("S1", skinnyX, Border));
        placements.Add(Place("S2", skinnyX, Border + 890 + Gap));
        panels.Add(Rect("SM", 180, 80));
        var smY = Border + 1800 + 5;
        var smX = Border;
        placements.Add(Place("SM", smX, smY));

        var result = SheetStabilityOptimizer.Optimize(
            panels, placements, 0, SheetW, SheetH, Border, Gap);

        Assert.True(result.Improved, string.Join("\n", result.Reasons));
        Assert.Contains(result.Reasons, r => r.Contains("rule:strips-off-edge-first", StringComparison.Ordinal));
        var sm = result.Placements.Single(p => p.PanelId == "SM");
        Assert.Equal(smY, sm.OffsetY, 6);
        Assert.True(
            sm.OffsetY < SheetH - Border - 80 - 40,
            $"small remnant part must not hug the top, y={sm.OffsetY:0}");
        var s1 = result.Placements.Single(p => p.PanelId == "S1");
        Assert.True(s1.OffsetX < skinnyX - 40, $"strip should leave the right edge, x={s1.OffsetX:0}");
    }

    [Fact]
    public void Swap_seven_wide_columns_leaves_left_column()
    {
        var panels = new List<Panel>();
        var placements = new List<NestPlacement>();
        var skinnyW = 55.0;
        var skinnyX = SheetW - Border - skinnyW;
        var x = skinnyX;
        for (var i = 6; i >= 0; i--)
        {
            x -= Gap + 150;
            var id = $"C{i}";
            panels.Add(Rect(id, 150, 1965));
            placements.Add(Place(id, x, Border));
        }
        panels.Add(Rect("S1", skinnyW, 1475));
        panels.Add(Rect("S2", 53, 609));
        placements.Add(Place("S1", skinnyX, Border));
        placements.Add(Place("S2", skinnyX, Border + 1475 + Gap));
        panels.Add(Rect("TOP", 344, 100));
        var top = Place("TOP", Border, SheetH - Border - 100);
        placements.Add(top);
        panels.Add(Rect("CAP", 50, 80));
        var capY = Border + 1475 + Gap + 609 + Gap;
        placements.Add(Place("CAP", skinnyX, capY));
        panels.Add(Rect("SPAN", 400, 80));
        var spanY = 1990.0;
        placements.Add(Place("SPAN", 500, spanY));

        var leftBefore = placements.Single(p => p.PanelId == "C0").OffsetX;
        var capBefore = skinnyX;
        var result = SheetStabilityOptimizer.Optimize(
            panels, placements, 0, SheetW, SheetH, Border, Gap);

        Assert.True(result.Improved, string.Join("\n", result.Reasons));
        Assert.Equal(leftBefore, result.Placements.Single(p => p.PanelId == "C0").OffsetX, 6);
        Assert.Equal(top.OffsetX, result.Placements.Single(p => p.PanelId == "TOP").OffsetX, 6);
        Assert.Equal(top.OffsetY, result.Placements.Single(p => p.PanelId == "TOP").OffsetY, 6);
        var s1x = result.Placements.Single(p => p.PanelId == "S1").OffsetX;
        var c3x = result.Placements.Single(p => p.PanelId == "C3").OffsetX;
        var c4x = result.Placements.Single(p => p.PanelId == "C4").OffsetX;
        Assert.True(c3x < s1x && s1x < c4x,
            $"skinny should sit between the left and right 150mm groups, C3={c3x:0} S={s1x:0} C4={c4x:0}\n{string.Join("\n", result.Reasons)}");
        var cap = result.Placements.Single(p => p.PanelId == "CAP");
        Assert.True(Math.Abs(cap.OffsetX - capBefore) > 40, $"top cap should ride the skinny column, {capBefore:0} → {cap.OffsetX:0}\n{string.Join("\n", result.Reasons)}");
        Assert.Equal(capY, cap.OffsetY, 6);
        var span = result.Placements.Single(p => p.PanelId == "SPAN");
        Assert.True(
            Math.Abs(span.OffsetX - 500) > 10 || span.OffsetY > spanY + 10,
            $"spanning remnant part should unstick off the skinny spine, ({span.OffsetX:0},{span.OffsetY:0})\n{string.Join("\n", result.Reasons)}");
        Assert.True(
            span.OffsetY < SheetH - Border - 80 - 40,
            $"remnant part should not hug the top edge, y={span.OffsetY:0}\n{string.Join("\n", result.Reasons)}");
        Assert.Empty(NestValidator.FindPolygonCollisions(panels, result.Placements, 0));
        var rightmost = result.Placements
            .Where(p => p.PanelId is not "TOP")
            .Select(p =>
            {
                var panel = panels.Single(x => x.PanelId == p.PanelId);
                var box = NestDrag.Aabb(panel, p.OffsetX, p.OffsetY, p.RotationDeg);
                return (p.PanelId, box.MaxX, W: box.MaxX - box.MinX);
            })
            .OrderByDescending(t => t.MaxX)
            .First();
        Assert.True(rightmost.W >= 140, $"right edge should be a ~150mm board, got {rightmost.PanelId} w={rightmost.W}\n{string.Join("\n", result.Reasons)}");
    }

    [Fact]
    public void Slides_edge_strip_toward_width_centre()
    {
        var largeA = Rect("A", 500, 400);
        var largeB = Rect("B", 500, 400);
        var strip = Rect("S", 40, 800);
        var panels = new[] { largeA, largeB, strip };
        var placements = new[]
        {
            Place("A", Border, Border),
            Place("B", SheetW - Border - 500, Border),
            Place("S", SheetW - Border - 40, 430),
        };

        var before = SheetStabilityOptimizer.CenterNorm(placements[2], 40, SheetW);
        var result = SheetStabilityOptimizer.Optimize(
            panels, placements, sheetIndex: 0,
            SheetW, SheetH, Border, Gap);

        Assert.True(result.Improved);
        var afterS = result.Placements.Single(p => p.PanelId == "S");
        var afterNorm = SheetStabilityOptimizer.CenterNorm(afterS, 40, SheetW);
        Assert.True(afterNorm > before + 0.05, $"strip centre-norm {before:0.00} → {afterNorm:0.00}");
        Assert.Empty(NestValidator.FindAabbCollisions(
            panels.Select(p => new NestPart { PanelId = p.PanelId, WidthMm = NestDrag.SizeRotated(p, 0).W, HeightMm = NestDrag.SizeRotated(p, 0).H }).ToList(),
            result.Placements, Gap));
    }

    [Fact]
    public void Locked_strip_does_not_move()
    {
        var large = Rect("A", 500, 400);
        var strip = Rect("S", 40, 800);
        var placements = new[]
        {
            Place("A", Border, Border),
            Place("S", SheetW - Border - 40, 430),
        };
        var result = SheetStabilityOptimizer.Optimize(
            [large, strip], placements, 0, SheetW, SheetH, Border, Gap,
            locked: new HashSet<string>(StringComparer.Ordinal) { "S" });
        var s = result.Placements.Single(p => p.PanelId == "S");
        Assert.Equal(SheetW - Border - 40, s.OffsetX, 6);
        Assert.False(result.Improved);
    }

    [Fact]
    public void Other_sheet_unchanged()
    {
        var a = Rect("A", 500, 400);
        var s = Rect("S", 40, 800);
        var other = Rect("Z", 300, 300);
        var placements = new[]
        {
            Place("A", Border, Border, 0),
            Place("S", SheetW - Border - 40, 430, 0),
            Place("Z", 40, 40, 1),
        };
        var result = SheetStabilityOptimizer.Optimize(
            [a, s, other], placements, sheetIndex: 0,
            SheetW, SheetH, Border, Gap);
        var z = result.Placements.Single(p => p.PanelId == "Z");
        Assert.Equal(1, z.SheetIndex);
        Assert.Equal(40, z.OffsetX, 6);
        Assert.Equal(40, z.OffsetY, 6);
    }

    [Fact]
    public void Swaps_when_notched_aabbs_overlap()
    {
        var panels = new List<Panel>();
        var placements = new List<NestPlacement>();
        const double tabW = 28;
        var skinnyW = 40.0;
        var skinnyX = SheetW - Border - skinnyW;
        var x = skinnyX;
        for (var i = 3; i >= 0; i--)
        {
            x -= Gap + 150;
            var id = $"C{i}";
            panels.Add(TabbedPortrait(id, 150, 1800, tabW));
            placements.Add(Place(id, x, Border));
        }
        panels.Add(Rect("S1", skinnyW, 890));
        panels.Add(Rect("S2", skinnyW, 890));
        placements.Add(Place("S1", skinnyX, Border));
        placements.Add(Place("S2", skinnyX, Border + 890 + Gap));

        var c0 = panels.Single(p => p.PanelId == "C0");
        var c1 = panels.Single(p => p.PanelId == "C1");
        var p0 = placements.Single(p => p.PanelId == "C0");
        var p1 = placements.Single(p => p.PanelId == "C1");
        var a0 = NestDrag.Aabb(c0, p0.OffsetX, p0.OffsetY, 0);
        var a1 = NestDrag.Aabb(c1, p1.OffsetX, p1.OffsetY, 0);
        Assert.True(a0.MaxX > a1.MinX, "fixture must overlap AABBs the way notched boards do");

        var result = SheetStabilityOptimizer.Optimize(
            panels, placements, 0, SheetW, SheetH, Border, Gap);

        Assert.Contains(result.Reasons, r => r.Contains("cols=5", StringComparison.Ordinal));
        Assert.Contains(result.Reasons, r => r.Contains("strips=2", StringComparison.Ordinal));
    }

    [Fact]
    public void Records_why_locked_strip_does_not_move()
    {
        var result = SheetStabilityOptimizer.Optimize(
            [Rect("A", 500, 400), Rect("S", 40, 800)],
            [Place("A", Border, Border), Place("S", SheetW - Border - 40, 430)],
            0, SheetW, SheetH, Border, Gap,
            locked: new HashSet<string>(StringComparer.Ordinal) { "S" });
        Assert.False(result.Improved);
        Assert.NotEmpty(result.Reasons);
        Assert.Contains(result.Reasons, r => r.Contains("strips=1", StringComparison.Ordinal));
        Assert.Contains(result.Reasons, r => r.Contains("result:", StringComparison.Ordinal));
    }

    [Fact]
    public void Mixed_side_panel_moves_three_right_strips_inward()
    {
        var side = Rect("Side", 600, 2000);
        var fp = Rect("FP", 340, 900);
        var cap = Rect("Cap", 600, 180);
        var s1 = Rect("S1", 45, 1400);
        var s2 = Rect("S2", 45, 1400);
        var s3 = Rect("S3", 45, 900);
        var skinnyX = SheetW - Border - 45;
        var panels = new[] { side, fp, cap, s1, s2, s3 };
        var placements = new[]
        {
            Place("Side", Border, Border),
            Place("FP", Border + 600 + Gap, Border),
            Place("Cap", Border, Border + 2000 + Gap),
            Place("S3", skinnyX, Border),
            Place("S2", skinnyX - Gap - 45, Border),
            Place("S1", skinnyX - 2 * (Gap + 45), Border),
        };
        var sideX = Border;
        var capX = Border;
        var capY = Border + 2000 + Gap;

        var result = SheetStabilityOptimizer.Optimize(
            panels, placements, 0, SheetW, SheetH, Border, Gap);

        Assert.True(result.Improved, string.Join("\n", result.Reasons));
        Assert.Equal(sideX, result.Placements.Single(p => p.PanelId == "Side").OffsetX, 6);
        Assert.Equal(capX, result.Placements.Single(p => p.PanelId == "Cap").OffsetX, 6);
        Assert.Equal(capY, result.Placements.Single(p => p.PanelId == "Cap").OffsetY, 6);
        foreach (var id in new[] { "S1", "S2", "S3" })
        {
            var s = result.Placements.Single(p => p.PanelId == id);
            Assert.True(s.OffsetX < skinnyX - 80, $"{id} should leave the right edge, x={s.OffsetX:0}\n{string.Join("\n", result.Reasons)}");
        }
        var rightmost = result.Placements
            .Select(p =>
            {
                var panel = panels.Single(x => x.PanelId == p.PanelId);
                var box = NestDrag.Aabb(panel, p.OffsetX, p.OffsetY, p.RotationDeg);
                return (p.PanelId, box.MaxX, W: box.MaxX - box.MinX);
            })
            .OrderByDescending(t => t.MaxX)
            .First();
        Assert.True(rightmost.W >= 140, $"right edge should be the FP/side board, got {rightmost.PanelId} w={rightmost.W}");
        Assert.Empty(NestValidator.FindPolygonCollisions(panels, result.Placements, 0));
    }

    [Fact]
    public void Mixed_body_columns_insert_right_strips_between_them()
    {
        var left = Rect("Left", 520, 1800);
        var right = Rect("RightB", 240, 1600);
        var s1 = Rect("S1", 40, 1475);
        var s2 = Rect("S2", 40, 900);
        var skinnyX = SheetW - Border - 40;
        var rightX = skinnyX - Gap - 240;
        var panels = new[] { left, right, s1, s2 };
        var placements = new[]
        {
            Place("Left", Border, Border),
            Place("RightB", rightX, Border),
            Place("S1", skinnyX, Border),
            Place("S2", skinnyX, Border + 1475 + Gap),
        };

        var result = SheetStabilityOptimizer.Optimize(
            panels, placements, 0, SheetW, SheetH, Border, Gap);

        Assert.True(result.Improved, string.Join("\n", result.Reasons));
        Assert.Equal(Border, result.Placements.Single(p => p.PanelId == "Left").OffsetX, 6);
        var sx = result.Placements.Single(p => p.PanelId == "S1").OffsetX;
        var rx = result.Placements.Single(p => p.PanelId == "RightB").OffsetX;
        Assert.True(sx > Border + 100 && sx < rx,
            $"strips should sit between Left and RightB, S={sx:0} RightB={rx:0}\n{string.Join("\n", result.Reasons)}");
        Assert.True(sx < skinnyX - 40, $"strip should leave the right edge, x={sx:0}");
        Assert.Empty(NestValidator.FindPolygonCollisions(panels, result.Placements, 0));
    }

    [Fact]
    public void Swaps_strips_with_wider_board_not_only_150()
    {
        var big = Rect("Big", 400, 1600);
        var s1 = Rect("S1", 40, 1400);
        var skinnyX = SheetW - Border - 40;
        var bigX = 120.0;
        var panels = new[] { big, s1 };
        var placements = new[]
        {
            Place("Big", bigX, Border),
            Place("S1", skinnyX, Border),
        };

        var result = SheetStabilityOptimizer.Optimize(
            panels, placements, 0, SheetW, SheetH, Border, Gap);

        Assert.True(result.Improved, string.Join("\n", result.Reasons));
        Assert.Contains(result.Reasons, r => r.StartsWith("pair-ok:", StringComparison.Ordinal) && r.Contains("Big", StringComparison.Ordinal));
        var s = result.Placements.Single(p => p.PanelId == "S1");
        Assert.True(s.OffsetX < skinnyX - 40, $"strip should leave the right edge, x={s.OffsetX:0}");
        Assert.True(s.OffsetX > Border + SheetStabilityOptimizer.EdgeSlackMm,
            $"strip must not land on the left edge, x={s.OffsetX:0}");
        var rightmost = result.Placements
            .Select(p =>
            {
                var panel = panels.Single(x => x.PanelId == p.PanelId);
                var box = NestDrag.Aabb(panel, p.OffsetX, p.OffsetY, p.RotationDeg);
                return (p.PanelId, box.MaxX, W: box.MaxX - box.MinX);
            })
            .OrderByDescending(t => t.MaxX)
            .First();
        Assert.Equal("Big", rightmost.PanelId);
        Assert.Empty(NestValidator.FindPolygonCollisions(panels, result.Placements, 0));
    }

    [Fact]
    public void Gapped_skinny_columns_are_not_one_edge_run()
    {
        var left = Rect("L", 40, 1400);
        var mid = Rect("M", 100, 900);
        var fat = Rect("F", 150, 1800);
        var right = Rect("R", 40, 1400);
        var panels = new[] { left, mid, fat, right };
        var rightX = SheetW - Border - 40;
        var fatX = rightX - Gap - 150;
        var midX = fatX - 180 - 100;
        var placements = new[]
        {
            Place("L", Border, Border),
            Place("M", midX, Border),
            Place("F", fatX, Border),
            Place("R", rightX, Border),
        };

        var result = SheetStabilityOptimizer.Optimize(
            panels, placements, 0, SheetW, SheetH, Border, Gap);

        Assert.True(result.Improved, string.Join("\n", result.Reasons));
        Assert.DoesNotContain(result.Reasons, r => r.Contains("run:left 0..", StringComparison.Ordinal)
            && r.Contains("+M", StringComparison.Ordinal));
        var rx = result.Placements.Single(p => p.PanelId == "R").OffsetX;
        Assert.True(rx < rightX - 40, $"right strip should leave the edge, x={rx:0}\n{string.Join("\n", result.Reasons)}");
        var rightmost = result.Placements
            .Select(p =>
            {
                var panel = panels.Single(x => x.PanelId == p.PanelId);
                var box = NestDrag.Aabb(panel, p.OffsetX, p.OffsetY, p.RotationDeg);
                return (p.PanelId, box.MaxX, W: box.MaxX - box.MinX);
            })
            .OrderByDescending(t => t.MaxX)
            .First();
        Assert.Equal("F", rightmost.PanelId);
        Assert.Empty(NestValidator.FindPolygonCollisions(panels, result.Placements, 0));
    }

    [Fact]
    public void Mixed_sheet_like_board11_moves_right_strips_inward()
    {
        // Matches 大板 11: T5 AABB overlaps the 150mm B3 in X (stacked in Y),
        // so T5 must not be swallowed into the right-edge run (that shoved B3 off-sheet).
        var lStrip = Rect("LStrip", 40, 1400);
        var h1 = Rect("H1", 100, 900);
        var h2 = Rect("H2", 100, 400);
        var b3 = Rect("B3", 150, 1000);
        var t5 = Rect("T5", 100, 500);
        var t4 = Rect("T4", 50, 994);
        var t2 = Rect("T2", 40, 1400);
        var body = Rect("Body1", 85, 589);
        var rails = Enumerable.Range(0, 4)
            .Select(i => Rect($"Rail{i}", 500, 100))
            .ToArray();
        var panels = new List<Panel> { lStrip, h1, h2, b3, t5, t4, t2, body };
        panels.AddRange(rails);

        var bodyX = SheetW - Border - 85;
        var t2x = bodyX - Gap - 40;
        var b3x = 888.0;
        var t5x = 956.0;
        var h2x = 800.0;
        var h1x = 688.0;
        var placements = new List<NestPlacement>
        {
            Place("LStrip", Border, Border),
            Place("H1", h1x, Border),
            Place("H2", h2x, Border),
            Place("B3", b3x, Border + 400 + Gap),
            Place("T5", t5x, 1700),
            Place("T2", t2x, Border),
            Place("T4", bodyX, Border),
            Place("Body1", bodyX, Border + 994 + Gap),
        };
        for (var i = 0; i < rails.Length; i++)
            placements.Add(Place($"Rail{i}", Border + 40 + Gap, Border + i * (100 + Gap)));

        var result = SheetStabilityOptimizer.Optimize(
            panels, placements, 0, SheetW, SheetH, Border, Gap);

        Assert.True(result.Improved, string.Join("\n", result.Reasons));
        Assert.Contains(result.Reasons, r => r.StartsWith("run:right ", StringComparison.Ordinal)
            && !r.Contains("T5", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Reasons, r => r.StartsWith("insert-reject:bounds", StringComparison.Ordinal));
        foreach (var id in new[] { "T4", "T2", "Body1" })
        {
            var s = result.Placements.Single(p => p.PanelId == id);
            var panel = panels.Single(p => p.PanelId == id);
            var box = NestDrag.Aabb(panel, s.OffsetX, s.OffsetY, s.RotationDeg);
            Assert.True(
                box.MaxX < SheetW - Border - SheetStabilityOptimizer.EdgeSlackMm,
                $"{id} should leave the right edge, maxX={box.MaxX:0}\n{string.Join("\n", result.Reasons)}");
        }
        var rightmost = result.Placements
            .Where(p => !p.PanelId.StartsWith("Rail", StringComparison.Ordinal))
            .Select(p =>
            {
                var panel = panels.Single(x => x.PanelId == p.PanelId);
                var box = NestDrag.Aabb(panel, p.OffsetX, p.OffsetY, p.RotationDeg);
                return (p.PanelId, box.MaxX, W: box.MaxX - box.MinX);
            })
            .OrderByDescending(t => t.MaxX)
            .First();
        Assert.True(rightmost.W >= 140, $"right edge should be B3, got {rightmost.PanelId} w={rightmost.W}\n{string.Join("\n", result.Reasons)}");
        Assert.Empty(NestValidator.FindPolygonCollisions(panels, result.Placements, 0));
        var lAfter = result.Placements.Single(p => p.PanelId == "LStrip");
        Assert.Equal(Border, lAfter.OffsetX, 6);
        var b3After = result.Placements.Single(p => p.PanelId == "B3");
        var b3Box = NestDrag.Aabb(b3, b3After.OffsetX, b3After.OffsetY, 0);
        Assert.True(b3Box.MaxX <= SheetW - Border + 0.05, $"B3 must stay on sheet, maxX={b3Box.MaxX:0}");
    }

    [Fact]
    public void SlideToward_does_not_overlap_horizontal_pack()
    {
        var strip = Rect("S", 40, 1200);
        var pack = Enumerable.Range(0, 6)
            .Select(i => Rect($"H{i}", 380, 80))
            .ToArray();
        var filler = Rect("F", 400, 400);
        var panels = new List<Panel> { strip, filler };
        panels.AddRange(pack);
        var placements = new List<NestPlacement>
        {
            Place("S", Border, Border),
            Place("F", SheetW - Border - 400, Border),
        };
        for (var i = 0; i < pack.Length; i++)
            placements.Add(Place($"H{i}", Border + 40 + Gap, Border + i * (80 + Gap)));

        var result = SheetStabilityOptimizer.Optimize(
            panels, placements, 0, SheetW, SheetH, Border, Gap);

        Assert.Empty(NestValidator.FindPolygonCollisions(panels, result.Placements, 0));
        var s = result.Placements.Single(p => p.PanelId == "S");
        var firstH = result.Placements.Single(p => p.PanelId == "H0");
        Assert.True(s.OffsetX + 40 <= firstH.OffsetX + 0.05,
            $"strip must not slide into the horizontal pack, S={s.OffsetX:0} H0={firstH.OffsetX:0}\n{string.Join("\n", result.Reasons)}");
    }

    static Panel HostWithCutout(string id, double w, double h, double cutMinX, double cutMinY, double cutMaxX, double cutMaxY) =>
        new()
        {
            PanelId = id,
            ThicknessMm = 18,
            Outline = new Outline
            {
                Points = [new(0, 0), new(w, 0), new(w, h), new(0, h)],
            },
            Features =
            [
                new PanelFeature
                {
                    FeatureId = "CUT1",
                    Kind = "throughCutout",
                    Through = true,
                    Purpose = "innerProfile",
                    Path =
                    [
                        new(cutMinX, cutMinY),
                        new(cutMaxX, cutMinY),
                        new(cutMaxX, cutMaxY),
                        new(cutMinX, cutMaxY),
                    ],
                },
            ],
        };

    [Fact]
    public void Centers_pip_child_when_sheet_has_no_strips()
    {
        var host = HostWithCutout("HOST", 400, 300, 90, 60, 310, 240);
        var child = Rect("CHILD", 80, 60);
        var hostX = Border;
        var hostY = Border;
        Assert.True(PartsInPartPacker.TryUsableVoid(
            host, hostX, hostY, 0, "CUT1", Gap, out var vx, out var vy, out var vw, out var vh));
        var placements = new[]
        {
            Place("HOST", hostX, hostY),
            Place("CHILD", vx, vy),
        };
        var slots = new[]
        {
            new PartInPartSlot
            {
                HostPanelId = "HOST",
                ChildPanelId = "CHILD",
                FeatureId = "CUT1",
                SheetIndex = 0,
            },
        };

        var result = SheetStabilityOptimizer.Optimize(
            [host, child], placements, 0, SheetW, SheetH, Border, Gap,
            partInPartSlots: slots);

        Assert.True(result.Improved, result.Message);
        Assert.Equal(1, result.PipMoved);
        Assert.Contains("套裁居中", result.Message);
        var after = result.Placements.Single(p => p.PanelId == "CHILD");
        Assert.Equal(vx + (vw - 80) / 2, after.OffsetX, 6);
        Assert.Equal(vy + (vh - 60) / 2, after.OffsetY, 6);
        Assert.Equal(hostX, result.Placements.Single(p => p.PanelId == "HOST").OffsetX, 6);
    }

    [Fact]
    public void Centers_pip_children_even_when_frozen_for_strip_pass()
    {
        var host = HostWithCutout("HOST", 400, 300, 90, 60, 310, 240);
        var child = Rect("CHILD", 80, 60);
        var hostX = Border;
        var hostY = Border;
        Assert.True(PartsInPartPacker.TryUsableVoid(
            host, hostX, hostY, 0, "CUT1", Gap, out var vx, out var vy, out var vw, out var vh));
        var placements = new[]
        {
            Place("HOST", hostX, hostY),
            Place("CHILD", vx, vy),
        };
        var slots = new[]
        {
            new PartInPartSlot
            {
                HostPanelId = "HOST",
                ChildPanelId = "CHILD",
                FeatureId = "CUT1",
                SheetIndex = 0,
            },
        };
        var frozen = new HashSet<string>(StringComparer.Ordinal) { "HOST", "CHILD" };

        var result = SheetStabilityOptimizer.Optimize(
            [host, child], placements, 0, SheetW, SheetH, Border, Gap,
            frozen: frozen,
            partInPartSlots: slots);

        Assert.True(result.Improved, result.Message);
        var after = result.Placements.Single(p => p.PanelId == "CHILD");
        Assert.Equal(vx + (vw - 80) / 2, after.OffsetX, 6);
        Assert.Equal(vy + (vh - 60) / 2, after.OffsetY, 6);
    }

    [Fact]
    public void Does_not_center_locked_pip_child()
    {
        var host = HostWithCutout("HOST", 400, 300, 90, 60, 310, 240);
        var child = Rect("CHILD", 80, 60);
        var hostX = Border;
        var hostY = Border;
        Assert.True(PartsInPartPacker.TryUsableVoid(
            host, hostX, hostY, 0, "CUT1", Gap, out var vx, out var vy, out _, out _));
        var placements = new[]
        {
            Place("HOST", hostX, hostY),
            Place("CHILD", vx, vy),
        };
        var slots = new[]
        {
            new PartInPartSlot
            {
                HostPanelId = "HOST",
                ChildPanelId = "CHILD",
                FeatureId = "CUT1",
                SheetIndex = 0,
            },
        };
        var locked = new HashSet<string>(StringComparer.Ordinal) { "CHILD" };

        var result = SheetStabilityOptimizer.Optimize(
            [host, child], placements, 0, SheetW, SheetH, Border, Gap,
            locked: locked,
            partInPartSlots: slots);

        Assert.False(result.Improved);
        Assert.Equal(0, result.PipMoved);
        Assert.Equal(vx, result.Placements.Single(p => p.PanelId == "CHILD").OffsetX, 6);
    }

    static Panel TabbedPortrait(string id, double w, double h, double tabW)
    {
        const double tabH = 18;
        return new()
        {
            PanelId = id,
            ThicknessMm = 18,
            Outline = new Outline
            {
                Points =
                [
                    new(0, 0),
                    new(w, 0),
                    new(w, h + tabH),
                    new(w + tabW, h + tabH),
                    new(w + tabW, h + 2),
                    new(w, h + 2),
                    new(w, h),
                    new(0, h),
                ],
            },
        };
    }
}
