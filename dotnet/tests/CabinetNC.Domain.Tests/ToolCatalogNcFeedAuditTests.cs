using CabinetNC.Domain.Machines;
using CabinetNC.Domain.Manufacturing;

namespace CabinetNC.Domain.Tests;

public class ToolCatalogNcFeedAuditTests
{
    [Fact]
    public void OpsToNc_emits_tool_spindle_and_feed_not_only_tool_comment()
    {
        var tools = ToolCatalog.DefaultMap();
        var t3 = tools["T3"];
        var profile = MachineCatalog.Get("nesting_router_6");
        // Profile defaults differ from T3 so a global-only emitter would fail this test
        Assert.NotEqual(t3.SpindleRpm, profile.SpindleRpm);
        Assert.NotEqual(t3.FeedZMmMin, profile.FeedZMmMin);

        var drill = new CutOp
        {
            Op = "drill",
            PanelId = "P1",
            FeatureId = "H1",
            ToolId = "T3",
            Placed = true,
            SheetX = 100,
            SheetY = 50,
            DiameterMm = 3,
            DepthMm = 12,
        };
        var groove = new CutOp
        {
            Op = "groove",
            PanelId = "P1",
            FeatureId = "G1",
            ToolId = "T2",
            Placed = true,
            DepthMm = 6,
            Path = [(10, 10), (80, 10)],
        };

        var nc = NcEmitter.OpsToNc([drill, groove], profile).Replace("\r\n", "\n");
        var t2 = tools["T2"];

        // Must not rely on a single program-level S from the machine profile alone
        Assert.Contains($"(tool T3)", nc);
        Assert.Contains($"S{Math.Round(t3.SpindleRpm)}", nc);
        Assert.Contains($"F{Fmt(t3.FeedZMmMin)}", nc);

        Assert.Contains($"(tool T2)", nc);
        Assert.Contains($"S{Math.Round(t2.SpindleRpm)}", nc);
        Assert.Contains($"F{Fmt(t2.FeedXyMmMin)}", nc);

        // Profile-global feed must not be the only XY feed after T2 change
        Assert.DoesNotContain($"G1 X80 Y10 F{Fmt(profile.FeedXyMmMin)}", nc);
    }

    static string Fmt(double n) => Math.Round(n, 3).ToString("0.###");
}
