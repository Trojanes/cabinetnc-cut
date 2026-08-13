using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Manufacturing;
using CabinetNC.Domain.Parts;

namespace CabinetNC.Domain.Tests;

public class ClearanceToolPickTests
{
    static PanelFeature Pocket(string id, double w, double h, string? purpose = null, string kind = "pocket") =>
        new()
        {
            FeatureId = id,
            Kind = kind,
            Purpose = purpose,
            DepthMm = 6,
            Path = [new(0, 0), new(w, 0), new(w, h), new(0, h)],
        };

    static PanelFeature Groove(string id, double widthMm, string? purpose = null) =>
        new()
        {
            FeatureId = id,
            Kind = "grooveVertical",
            Purpose = purpose,
            WidthMm = widthMm,
            DepthMm = 8,
            Path = [new(10, 10), new(90, 10)],
        };

    [Fact]
    public void Wide_pocket_picks_T2()
    {
        Assert.Equal("T2", ClearanceToolPick.Pick(Pocket("PK", 80, 40)));
        Assert.Equal(40, ClearanceToolPick.ShortSideMm(Pocket("PK", 80, 40)));
    }

    [Fact]
    public void Narrow_pocket_that_fits_T1_picks_T1()
    {
        Assert.Equal("T1", ClearanceToolPick.Pick(Pocket("PK", 80, 12)));
    }

    [Fact]
    public void Hinge_cup_always_T2_even_when_short_side_is_narrow()
    {
        Assert.Equal("T2", ClearanceToolPick.Pick(Pocket("H1", 80, 12, purpose: "hinge")));
        Assert.Equal("T2", ClearanceToolPick.Pick(Pocket("C1", 80, 12, purpose: "铰杯")));
        Assert.Equal("T2", ClearanceToolPick.Pick(Pocket("铰链1", 80, 12)));
    }

    [Fact]
    public void Groove_uses_width_not_length()
    {
        Assert.Equal("T2", ClearanceToolPick.Pick(Groove("G", 20)));
        Assert.Equal("T1", ClearanceToolPick.Pick(Groove("G", 10)));
    }

    [Fact]
    public void FeaturesToOps_binds_picked_tool_and_clears_with_that_diameter()
    {
        var panel = new Panel
        {
            PanelId = "P",
            ThicknessMm = 18,
            Outline = new Outline
            {
                Points = [new(0, 0), new(400, 0), new(400, 300), new(0, 300)],
            },
            Features =
            [
                Pocket("WIDE", 100, 80),
                Pocket("NARROW", 80, 12),
                Pocket("HINGE", 80, 12, purpose: "hinge"),
                Groove("G20", 20),
            ],
        };
        var ops = OpsPlanner.FeaturesToOps([panel]);
        Assert.Equal("T2", Assert.Single(ops, o => o.FeatureId == "WIDE").ToolId);
        Assert.Equal("T1", Assert.Single(ops, o => o.FeatureId == "NARROW").ToolId);
        Assert.Equal("T2", Assert.Single(ops, o => o.FeatureId == "HINGE").ToolId);
        Assert.Equal("T2", Assert.Single(ops, o => o.FeatureId == "G20").ToolId);

        var wide = Assert.Single(ops, o => o.FeatureId == "WIDE");
        var narrow = Assert.Single(ops, o => o.FeatureId == "NARROW");
        Assert.Equal(5, wide.StepdownMm);
        Assert.Equal(6.35 * 0.5, narrow.StepdownMm);
    }

    [Fact]
    public void Untagged_hinge_cup_bore_is_clearance_not_drill()
    {
        var cup = new PanelFeature
        {
            FeatureId = "H-001",
            Kind = "holeVertical",
            X = 80,
            Y = 60,
            DiameterMm = 35,
            DepthMm = 13,
        };
        Assert.True(ClearanceToolPick.IsHingeFeature(cup));
        var panel = new Panel
        {
            PanelId = "P",
            ThicknessMm = 18,
            Outline = new Outline
            {
                Points = [new(0, 0), new(400, 0), new(400, 300), new(0, 300)],
            },
            Features =
            [
                cup,
                new PanelFeature
                {
                    FeatureId = "PIN",
                    Kind = "holeVertical",
                    X = 20,
                    Y = 20,
                    DiameterMm = 5,
                    DepthMm = 12,
                },
                new PanelFeature
                {
                    FeatureId = "D3",
                    Kind = "holeVertical",
                    X = 30,
                    Y = 30,
                    DiameterMm = 3,
                    DepthMm = 12,
                },
            ],
        };
        var ops = OpsPlanner.FeaturesToOps([panel]);
        var milled = Assert.Single(ops, o => o.FeatureId == "H-001");
        Assert.Equal("pocket", milled.Op);
        Assert.Equal("T2", milled.ToolId);
        Assert.Equal(CamStrategyKind.AreaClearance, CamStrategy.Classify(milled));
        Assert.False(milled.PocketTooSmallForTool);
        Assert.True(milled.PathSegments is { Count: >= 1 });
        Assert.Equal("pocket", Assert.Single(ops, o => o.FeatureId == "PIN").Op);
        Assert.Equal("drill", Assert.Single(ops, o => o.FeatureId == "D3").Op);
    }

    [Fact]
    public void Only_holes_under_5mm_are_drill_cycles()
    {
        var under = new PanelFeature
        {
            FeatureId = "D3", Kind = "holeVertical",
            X = 10, Y = 10, DiameterMm = 3, DepthMm = 12,
        };
        var five = new PanelFeature
        {
            FeatureId = "P5", Kind = "holeVertical",
            X = 10, Y = 10, DiameterMm = 5, DepthMm = 12,
        };
        Assert.True(ClearanceToolPick.IsDrillHole(under));
        Assert.False(ClearanceToolPick.IsDrillHole(five));
        Assert.False(ClearanceToolPick.IsDrillHole(new PanelFeature
        {
            FeatureId = "H35", Kind = "holeVertical",
            X = 10, Y = 10, DiameterMm = 35, DepthMm = 13,
        }));
    }

    [Fact]
    public void FeaturesToOps_respects_drill_max_exclusive()
    {
        var panel = new Panel
        {
            PanelId = "P",
            ThicknessMm = 18,
            Outline = new Outline
            {
                Points = [new(0, 0), new(400, 0), new(400, 300), new(0, 300)],
            },
            Features =
            [
                new PanelFeature
                {
                    FeatureId = "D3", Kind = "holeVertical",
                    X = 20, Y = 20, DiameterMm = 3, DepthMm = 12,
                },
                new PanelFeature
                {
                    FeatureId = "D45", Kind = "holeVertical",
                    X = 40, Y = 40, DiameterMm = 4.5, DepthMm = 12,
                },
            ],
        };
        var ops = OpsPlanner.FeaturesToOps([panel], drillMaxExclusiveMm: 4);
        Assert.Equal("drill", Assert.Single(ops, o => o.FeatureId == "D3").Op);
        Assert.Equal("pocket", Assert.Single(ops, o => o.FeatureId == "D45").Op);
    }

    [Fact]
    public void Compact_26mm_cup_is_hinge_five_mm_is_not()
    {
        var compact = new PanelFeature
        {
            FeatureId = "C26", Kind = "holeVertical",
            X = 40, Y = 40, DiameterMm = 26, DepthMm = 12,
        };
        var pin = new PanelFeature
        {
            FeatureId = "P5", Kind = "holeVertical",
            X = 10, Y = 10, DiameterMm = 5, DepthMm = 12,
        };
        Assert.True(ClearanceToolPick.IsHingeFeature(compact));
        Assert.False(ClearanceToolPick.IsHingeFeature(pin));
    }

    [Fact]
    public void FeaturesToOps_respects_large_min_short_threshold()
    {
        var panel = new Panel
        {
            PanelId = "P",
            ThicknessMm = 18,
            Outline = new Outline
            {
                Points = [new(0, 0), new(400, 0), new(400, 300), new(0, 300)],
            },
            Features = [Pocket("PK", 80, 20)],
        };
        var atDefault = Assert.Single(OpsPlanner.FeaturesToOps([panel]), o => o.Op == "pocket");
        Assert.Equal("T2", atDefault.ToolId);
        var raised = Assert.Single(
            OpsPlanner.FeaturesToOps([panel], clearanceLargeMinShortMm: 30),
            o => o.Op == "pocket");
        Assert.Equal("T1", raised.ToolId);
    }
}
