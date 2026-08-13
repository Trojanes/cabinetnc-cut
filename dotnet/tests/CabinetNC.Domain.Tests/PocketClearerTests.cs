using CabinetNC.Domain.Manufacturing;
using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Parts;

namespace CabinetNC.Domain.Tests;

public class PocketClearerTests
{
    static IReadOnlyList<(double X, double Y)> Rect(double w, double h) =>
        [(0, 0), (w, 0), (w, h), (0, h)];

    [Fact]
    public void Clear_uses_spiral_not_horizontal_raster()
    {
        var result = PocketClearer.Clear(new PocketClearer.PocketClearRequest
        {
            Outline = Rect(120, 80),
            ToolDiameterMm = 6.35,
            StepoverMm = 4,
        });
        Assert.True(result.Segments.Count >= 1);
        var fill = result.Segments[0];
        var turn = 0;
        for (var i = 2; i < fill.Count; i++)
        {
            var ax = fill[i - 1].X - fill[i - 2].X;
            var ay = fill[i - 1].Y - fill[i - 2].Y;
            var bx = fill[i].X - fill[i - 1].X;
            var by = fill[i].Y - fill[i - 1].Y;
            if (ax * by - ay * bx is > 0.05 or < -0.05)
                turn++;
        }
        Assert.True(turn >= 8, $"spiral turns={turn} pts={fill.Count}");
    }

    [Fact]
    public void Clear_path_has_many_more_points_than_boundary()
    {
        var boundary = Rect(120, 80);
        var result = PocketClearer.Clear(new PocketClearer.PocketClearRequest
        {
            Outline = boundary,
            ToolDiameterMm = 6.35,
            StepoverMm = 3,
        });
        Assert.True(result.PassCount >= 3, $"passCount={result.PassCount}");
        Assert.True(result.Path.Count >= boundary.Count * 3,
            $"pathPts={result.Path.Count} boundary={boundary.Count}");
    }

    [Fact]
    public void Smaller_stepover_makes_longer_path()
    {
        var outline = Rect(100, 60);
        var coarse = PocketClearer.Clear(new PocketClearer.PocketClearRequest
        {
            Outline = outline, ToolDiameterMm = 6, StepoverMm = 5,
        });
        var fine = PocketClearer.Clear(new PocketClearer.PocketClearRequest
        {
            Outline = outline, ToolDiameterMm = 6, StepoverMm = 2,
        });
        Assert.True(fine.Path.Count > coarse.Path.Count);
        Assert.True(fine.PassCount > coarse.PassCount);
    }

    [Fact]
    public void FeaturesToOps_pocket_is_not_boundary_only()
    {
        var panel = new Panel
        {
            PanelId = "P",
            ThicknessMm = 18,
            Outline = new Outline
            {
                Points = [new(0, 0), new(200, 0), new(200, 150), new(0, 150)],
            },
            Features =
            [
                new PanelFeature
                {
                    FeatureId = "PK1",
                    Kind = "pocket",
                    DepthMm = 6,
                    Path = [new(20, 20), new(120, 20), new(120, 90), new(20, 90)],
                },
            ],
        };
        var ops = OpsPlanner.FeaturesToOps([panel]);
        var pocket = Assert.Single(ops, o => o.Op == "pocket");
        Assert.True(pocket.Path!.Count > 8, $"pocket path pts={pocket.Path.Count}");
    }

    [Fact]
    public void Small_panel_warns_in_preflight()
    {
        var panel = new Panel
        {
            PanelId = "S",
            ThicknessMm = 18,
            Outline = new Outline
            {
                Points = [new(0, 0), new(50, 0), new(50, 40), new(0, 40)],
            },
        };
        var ops = OpsPlanner.FeaturesToOps([panel])
            .Select(o => o with { Placed = true, Path = [(1, 1), (2, 1), (2, 2)] })
            .ToList();
        var report = NcPreflight.Check(
            ops,
            Machines.MachineCatalog.Get("nesting_router_6"),
            1220, 2440,
            new Dictionary<string, Panel> { ["S"] = panel });
        Assert.Contains(report.Issues, i => i.Code == "small_panel" && i.Level == "warn");
    }
}
