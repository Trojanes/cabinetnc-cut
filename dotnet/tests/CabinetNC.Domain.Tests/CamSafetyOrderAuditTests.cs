using CabinetNC.Domain.Manufacturing;

namespace CabinetNC.Domain.Tests;

public class CamSafetyOrderAuditTests
{
    [Fact]
    public void OrderSafe_runs_all_drills_before_any_outer_across_panels()
    {
        // Regression: PanelId sorted before SequenceRank made panel A's outer
        // run before panel B's drill/groove.
        var ops = new[]
        {
            new CutOp { Op = "contour", PanelId = "A", FeatureId = null, ToolId = "T1", Placed = true },
            new CutOp { Op = "drill", PanelId = "A", FeatureId = "H1", ToolId = "T3", Placed = true },
            new CutOp { Op = "groove", PanelId = "B", FeatureId = "G1", ToolId = "T2", Placed = true },
            new CutOp { Op = "contour", PanelId = "B", FeatureId = null, ToolId = "T1", Placed = true },
            new CutOp { Op = "drill", PanelId = "B", FeatureId = "H2", ToolId = "T3", Placed = true },
        };

        var ordered = CamSafety.OrderSafe(ops).ToList();
        var ranks = ordered.Select(CamSafety.SequenceRank).ToList();

        var lastDrill = ranks.LastIndexOf(0);
        var lastGroove = ranks.LastIndexOf(2);
        var firstOuter = ranks.IndexOf(4);

        Assert.True(firstOuter >= 0, "expected outer contours");
        Assert.True(lastDrill < firstOuter, $"drill@{lastDrill} must precede outer@{firstOuter}; order={string.Join(",", ordered.Select(o => $"{o.PanelId}:{o.Op}"))}");
        Assert.True(lastGroove < firstOuter, $"groove@{lastGroove} must precede outer@{firstOuter}");
        // Same-rank ops may still group by PanelId after SequenceRank
        Assert.Equal(0, ranks[0]); // first is a drill
    }
}
