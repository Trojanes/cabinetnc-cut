using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Manufacturing;
using CabinetNC.Domain.Nesting;
using CabinetNC.Domain.Parts;

namespace CabinetNC.Domain.Tests;

public class OpsPlannerTests
{
    [Fact]
    public void Contour_and_drill_from_panel()
    {
        var panel = new Panel
        {
            PanelId = "P1",
            Outline = new Outline
            {
                Points = [new(0, 0), new(100, 0), new(100, 50), new(0, 50)],
            },
            Features =
            [
                new PanelFeature
                {
                    FeatureId = "H1",
                    Kind = "holeVertical",
                    X = 20,
                    Y = 20,
                    DiameterMm = 3,
                    DepthMm = 12,
                },
            ],
        };
        var ops = OpsPlanner.FeaturesToOps([panel]);
        Assert.Equal(2, ops.Count);
        Assert.Contains(ops, o => o.Op == "contour");
        Assert.Contains(ops, o => o.Op == "drill");

        var placed = OpsPlanner.AttachToNest(ops, [
            new NestPlacement { PanelId = "P1", SheetIndex = 0, OffsetX = 10, OffsetY = 5, RotationDeg = 0 },
        ]);
        var drill = placed.First(o => o.Op == "drill");
        Assert.True(drill.Placed);
        Assert.Equal(30, drill.SheetX);
        Assert.Equal(25, drill.SheetY);
    }

    [Fact]
    public void AttachToNest_keeps_four_decimal_sheet_coords()
    {
        var ops = new[]
        {
            new CutOp
            {
                Op = "drill",
                PanelId = "P1",
                FeatureId = "H1",
                Placed = false,
                X = 10.12346,
                Y = 20.56789,
            },
        };
        var placed = OpsPlanner.AttachToNest(ops, [
            new NestPlacement { PanelId = "P1", SheetIndex = 0, OffsetX = 0.11111, OffsetY = 0.22222 },
        ]);
        var drill = Assert.Single(placed);
        Assert.Equal(10.2346, drill.SheetX);
        Assert.Equal(20.7901, drill.SheetY);
    }

    [Fact]
    public void Through_finger_hole_is_one_contour_not_pocket_spiral()
    {
        var panel = new Panel
        {
            PanelId = "LID",
            ThicknessMm = 18,
            Outline = new Outline
            {
                Points = [new(0, 0), new(447, 0), new(447, 277), new(0, 277)],
            },
            Features =
            [
                new PanelFeature
                {
                    FeatureId = "FINGER",
                    Kind = "holeVertical",
                    X = 223.5,
                    Y = 138.5,
                    DiameterMm = 40,
                    DepthMm = 18,
                    Through = true,
                },
            ],
        };
        var ops = OpsPlanner.FeaturesToOps([panel]);
        var hole = Assert.Single(ops, o => o.FeatureId == "FINGER");
        Assert.Equal("contour", hole.Op);
        Assert.True(hole.Through);
        Assert.True(hole.Path is { Count: >= 3 });
        Assert.Null(hole.PathSegments);
        Assert.Null(hole.FinishLoop);
    }

    [Fact]
    public void Rebate_pocket_is_two_closed_walls_without_outer_retrace()
    {
        var panel = new Panel
        {
            PanelId = "LID",
            ThicknessMm = 18,
            Outline = new Outline
            {
                Points = [new(0, 0), new(447, 0), new(447, 277), new(0, 277)],
            },
            Features =
            [
                new PanelFeature
                {
                    FeatureId = "REBATE",
                    Kind = "pocket",
                    DepthMm = 9,
                    Path = [new(0, 0), new(447, 0), new(447, 277), new(0, 277)],
                    Holes =
                    [
                        [new(9, 9), new(438, 9), new(438, 268), new(9, 268)],
                    ],
                },
            ],
        };
        var pocket = Assert.Single(OpsPlanner.FeaturesToOps([panel]), o => o.Op == "pocket");
        Assert.Null(pocket.FinishLoop);
        Assert.Equal(2, pocket.PathSegments!.Count);
        Assert.All(pocket.PathSegments, loop =>
        {
            Assert.True(loop.Count >= 4);
            Assert.Equal(loop[0].X, loop[^1].X, 6);
            Assert.Equal(loop[0].Y, loop[^1].Y, 6);
        });
    }
}
