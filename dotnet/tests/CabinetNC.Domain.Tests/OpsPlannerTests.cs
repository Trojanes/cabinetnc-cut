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
                    DiameterMm = 8,
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
}
