using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Machines;
using CabinetNC.Domain.Manufacturing;
using CabinetNC.Domain.Parts;

namespace CabinetNC.Domain.Tests;

public class CamSafetyGrooveDepthAuditTests
{
    [Fact]
    public void ApplyPanelDepths_does_not_clamp_away_illegal_groove_depth()
    {
        var panel = new Panel
        {
            PanelId = "P1",
            ThicknessMm = 18,
            Outline = new Outline
            {
                Points = [new(0, 0), new(100, 0), new(100, 50), new(0, 50)],
            },
        };
        var ops = new[]
        {
            new CutOp
            {
                Op = "groove", PanelId = "P1", FeatureId = "G1", ToolId = "T2",
                DepthMm = 40, Placed = true,
                Path = [(10, 10), (90, 10)],
            },
        };
        var applied = CamSafety.ApplyPanelDepths(ops, new Dictionary<string, Panel> { ["P1"] = panel });
        Assert.Equal(40, Assert.Single(applied).DepthMm);
        var issues = CamSafety.DepthIssues(applied, new Dictionary<string, Panel> { ["P1"] = panel });
        Assert.Contains(issues, i => i.Code == "groove_too_deep");
    }

    [Fact]
    public void FeaturesToOps_preflight_still_sees_overdeep_groove()
    {
        var panel = new Panel
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
                    FeatureId = "G1", Kind = "grooveVertical",
                    DepthMm = 40, WidthMm = 6,
                    Path = [new(10, 10), new(90, 10)],
                },
            ],
        };
        var ops = OpsPlanner.FeaturesToOps([panel])
            .Select(o => o with
            {
                Placed = true,
                SheetX = o.Op == "drill" ? 10 : null,
                SheetY = o.Op == "drill" ? 10 : null,
                Path = o.Path ?? [(0, 0), (10, 0), (10, 10)],
            })
            .ToList();
        var groove = Assert.Single(ops, o => o.Op == "groove");
        Assert.Equal(40, groove.DepthMm);
        var report = NcPreflight.Check(
            ops,
            MachineCatalog.Get("nesting_router_6"),
            1220, 2440,
            new Dictionary<string, Panel> { ["P1"] = panel });
        Assert.False(report.Ok);
        Assert.Contains(report.Issues, i => i.Code == "groove_too_deep");
    }
}
