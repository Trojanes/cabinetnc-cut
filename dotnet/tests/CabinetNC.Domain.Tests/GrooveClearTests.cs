using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Machines;
using CabinetNC.Domain.Manufacturing;
using CabinetNC.Domain.Nesting;
using CabinetNC.Domain.Parts;

namespace CabinetNC.Domain.Tests;

public class GrooveClearTests
{
    static Panel PanelWithGroove(double widthMm, string? purpose, string id = "G1") => new()
    {
        PanelId = "P",
        ThicknessMm = 16,
        Outline = new Outline { Points = [new(0, 0), new(200, 0), new(200, 120), new(0, 120)] },
        Features =
        [
            new PanelFeature
            {
                FeatureId = id,
                Kind = "grooveVertical",
                Purpose = purpose,
                WidthMm = widthMm,
                DepthMm = 8,
                Path = [new(50, 10), new(50, 110)],
            },
        ],
    };

    [Fact]
    public void Tool_width_tongue_stays_centreline()
    {
        Assert.False(CamStrategy.NeedsGrooveClear(6.35, TroyRecipe.TongueDiameterMm));
        var ops = OpsPlanner.FeaturesToOps([PanelWithGroove(6, "tongue")]);
        var g = Assert.Single(ops, o => o.Op == "groove");
        Assert.True(g.IsTongue);
        Assert.Equal("T1", g.ToolId);
        Assert.True(g.PathSegments is null or { Count: 0 });
        Assert.True(g.FinishLoop is null or { Count: < 3 });
        Assert.Equal(2, g.Path!.Count);
    }

    [Fact]
    public void Sixteen_mm_tongue_clears_on_T1_not_centreline()
    {
        Assert.True(CamStrategy.NeedsGrooveClear(16, TroyRecipe.TongueDiameterMm));
        var ops = OpsPlanner.FeaturesToOps([PanelWithGroove(16, "tongue")]);
        var g = Assert.Single(ops, o => o.Op == "groove");
        Assert.True(g.IsTongue);
        Assert.Equal("T1", g.ToolId);
        Assert.Equal(16, g.WidthMm);
        Assert.False(g.PocketTooSmallForTool);
        Assert.True(g.FinishLoop is { Count: >= 3 });
        var spiral = Assert.Single(g.PathSegments!);
        Assert.Equal(spiral[^1].X, g.FinishLoop![0].X, 6);
        Assert.Equal(spiral[^1].Y, g.FinishLoop[0].Y, 6);
        var xs = g.FinishLoop!.Select(p => p.X).ToList();
        Assert.InRange(xs.Max() - xs.Min(), 8.5, 11);
    }

    [Fact]
    public void Sixteen_mm_tongue_nc_is_loop_at_F9000()
    {
        var ops = OpsPlanner.FeaturesToOps([PanelWithGroove(16, "tongue")]);
        var placed = OpsPlanner.AttachToNest(ops, [
            new NestPlacement { PanelId = "P", SheetIndex = 0, OffsetX = 20, OffsetY = 30 },
        ]);
        var groove = placed.Where(o => o.Op == "groove").ToArray();
        var nc = NcEmitter.OpsToNc(groove, MachineCatalog.Get("nesting_router_6"),
            recipe: PostRecipe.TroyDefault());
        Assert.Contains("M6 T1", nc);
        Assert.Contains("F9000.0", nc);
        Assert.DoesNotContain("F12000.0", nc);
        var workMoves = nc.Split('\n').Count(l => l.Contains("F9000.0", StringComparison.Ordinal));
        Assert.True(workMoves >= 1, nc);
        Assert.DoesNotContain("G1 Y110.0000 F9000.0", nc);
        Assert.Equal(2, nc.Split('\n').Count(l =>
            l.Contains("G0 Z30.0000", StringComparison.Ordinal)));
        Assert.Equal(1, nc.Split('\n').Count(l =>
            l.Contains("G1 Z8.0000", StringComparison.Ordinal)));
    }

    [Fact]
    public void Preflight_rejects_wide_groove_without_clear()
    {
        var op = new CutOp
        {
            Op = "groove",
            PanelId = "P",
            FeatureId = "G1",
            ToolId = "T1",
            Placed = true,
            IsTongue = true,
            WidthMm = 16,
            DepthMm = 8,
            Path = [(50, 10), (50, 110)],
        };
        var issues = NcPreflight.GrooveClearIssues([op]);
        Assert.Contains(issues, i => i.Code == "groove_width_not_cleared");
    }
}
