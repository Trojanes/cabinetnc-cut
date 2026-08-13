using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Machines;
using CabinetNC.Domain.Manufacturing;

namespace CabinetNC.Domain.Tests;

public class PolylineArcFitTests
{
    static List<(double X, double Y)> Quarter(bool cw, int steps = 12)
    {
        var pts = new List<(double X, double Y)>(steps + 1);
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (double)steps * Math.PI / 2;
            pts.Add(cw
                ? (5 * Math.Cos(t), -5 * Math.Sin(t))
                : (5 * Math.Cos(t), 5 * Math.Sin(t)));
        }
        return pts;
    }

    [Fact]
    public void Tessellated_quarter_becomes_one_arc_R5()
    {
        var segs = PolylineArcFit.Fit(Quarter(cw: false), closed: false);
        var arc = Assert.Single(segs, s => s.Arc);
        Assert.False(arc.Cw);
        Assert.Equal(5, arc.R, 3);
        Assert.Equal(0, arc.X, 3);
        Assert.Equal(5, arc.Y, 3);
    }

    [Fact]
    public void Clockwise_quarter_is_G2()
    {
        var segs = PolylineArcFit.Fit(Quarter(cw: true), closed: false);
        var arc = Assert.Single(segs, s => s.Arc);
        Assert.True(arc.Cw);
        Assert.Equal(5, arc.R, 3);
    }

    [Fact]
    public void Sharp_square_stays_G1()
    {
        var segs = PolylineArcFit.Fit([(0, 0), (200, 0), (200, 100), (0, 100)], closed: true);
        Assert.DoesNotContain(segs, s => s.Arc);
        Assert.True(segs.Count >= 4);
    }
}

public class NcEmitterTroyArcTests
{
    static MachineProfile Machine() => MachineCatalog.Get("nesting_router_6");

    [Fact]
    public void Offset_square_emits_R5_arcs()
    {
        var source = new CutOp
        {
            Op = "contour",
            PanelId = "P1",
            ToolId = "T2",
            Placed = true,
            ClosePath = true,
            Through = true,
            ThicknessMm = 18,
            DepthMm = 18.5,
            Path = [(0, 0), (100, 0), (100, 80), (0, 80)],
        };
        var offset = ContourToolOffset.Apply([source], 5);
        var nc = NcEmitter.OpsToNc(offset, Machine(), recipe: PostRecipe.TroyDefault());
        Assert.True(nc.Contains("G2 ") || nc.Contains("G3 "), nc);
        Assert.Contains("R5.0000", nc);
    }
}
