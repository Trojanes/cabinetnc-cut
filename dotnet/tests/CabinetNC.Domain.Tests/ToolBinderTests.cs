using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Manufacturing;
using CabinetNC.Domain.Machines;
using CabinetNC.Domain.Parts;

namespace CabinetNC.Domain.Tests;

public class ToolBinderTests
{
    static Panel PanelWithHole() => new()
    {
        PanelId = "P1",
        ThicknessMm = 18,
        Outline = new Outline
        {
            Points = [new(0, 0), new(100, 0), new(100, 50), new(0, 50)],
        },
        Features =
        [
            new PanelFeature
            {
                FeatureId = "H1", Kind = "holeVertical",
                X = 20, Y = 20, DiameterMm = 5, DepthMm = 12,
            },
            new PanelFeature
            {
                FeatureId = "G1", Kind = "grooveVertical",
                WidthMm = 6, DepthMm = 8,
                Path = [new(10, 10), new(90, 10)],
            },
        ],
    };

    [Fact]
    public void FeaturesToOps_assigns_tool_ids()
    {
        var ops = OpsPlanner.FeaturesToOps([PanelWithHole()]);
        Assert.Contains(ops, o => o.Op == "contour" && o.ToolId == "T1");
        Assert.Contains(ops, o => o.Op == "drill" && o.ToolId == "T3");
        Assert.Contains(ops, o => o.Op == "groove" && o.ToolId == "T2");
        Assert.Empty(ToolBinder.MissingToolIds(ops));
    }

    [Fact]
    public void Preflight_errors_when_tool_id_stripped()
    {
        var ops = OpsPlanner.FeaturesToOps([PanelWithHole()])
            .Select(o => o with { ToolId = null, Placed = true, SheetX = 10, SheetY = 10, Path = [(10, 10), (20, 10)] })
            .ToList();
        // drills need sheet coords; contours need path — set both
        ops = ops.Select(o => o.Op == "drill"
            ? o with { SheetX = 10, SheetY = 10, Path = null }
            : o with { Path = [(0, 0), (100, 0), (100, 50), (0, 50)] }).ToList();

        var report = NcPreflight.Check(ops, MachineCatalog.Get("nesting_router_6"), 1220, 2440);
        Assert.False(report.Ok);
        Assert.Contains(report.Issues, i => i.Code == "missing_tool_id");
    }

    [Fact]
    public void Default_presets_include_T1_T2_T3()
    {
        Assert.Contains(ToolCatalog.DefaultPresets, t => t.ToolId == "T1" && t.DiameterMm == 6.35);
        Assert.Contains(ToolCatalog.DefaultPresets, t => t.ToolId == "T2");
        Assert.Contains(ToolCatalog.DefaultPresets, t => t.ToolId == "T3" && t.Role == "drill");
    }
}
