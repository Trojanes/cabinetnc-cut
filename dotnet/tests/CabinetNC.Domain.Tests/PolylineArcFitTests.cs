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
    public void Clipper_style_dense_fan_becomes_one_R5()
    {
        // Real S5 corner (Bunk Bed S5 N211–N224): 0.63 mm chords on R5.
        (double X, double Y)[] fan =
        [
            (624.000, 1317.000),
            (624.631, 1317.040),
            (625.252, 1317.159),
            (625.852, 1317.356),
            (626.424, 1317.627),
            (626.956, 1317.967),
            (627.441, 1318.373),
            (627.871, 1318.836),
            (628.240, 1319.350),
            (628.540, 1319.906),
            (628.768, 1320.495),
            (628.920, 1321.109),
            (628.993, 1321.737),
            (629.000, 1322.000),
        ];
        var segs = PolylineArcFit.Fit(fan, closed: false);
        var arc = Assert.Single(segs, s => s.Arc);
        Assert.Equal(5, arc.R, 3);
        Assert.Equal(629, arc.X, 2);
        Assert.Equal(1322, arc.Y, 2);
    }

    [Fact]
    public void Sharp_square_stays_G1()
    {
        var segs = PolylineArcFit.Fit([(0, 0), (200, 0), (200, 100), (0, 100)], closed: true);
        Assert.DoesNotContain(segs, s => s.Arc);
        Assert.True(segs.Count >= 4);
    }

    [Fact]
    public void Shallow_bow_on_long_edge_becomes_one_G1()
    {
        // Circular R256 over ~90 mm — the false G2 OmniCam used to emit on square edges.
        const double r = 256;
        var sweep = 91.5 / r;
        var pts = new List<(double X, double Y)>();
        for (var i = 0; i <= 12; i++)
        {
            var a = -sweep / 2 + sweep * (i / 12d);
            pts.Add((257 + r * Math.Sin(a), 1285 - r * (1 - Math.Cos(a))));
        }
        var segs = PolylineArcFit.Fit(pts, closed: false);
        Assert.DoesNotContain(segs, s => s.Arc);
        var line = Assert.Single(segs);
        Assert.Equal(pts[^1].X, line.X, 3);
        Assert.Equal(pts[^1].Y, line.Y, 3);
    }
}

public class NcEmitterTroyArcTests
{
    static MachineProfile Machine() => MachineCatalog.Get("nesting_router_6");

    [Fact]
    public void Offset_outer_square_uses_R5_tool_centre_corners()
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
        var path = offset[0].Path!;
        var nc = NcEmitter.OpsToNc(offset, Machine(), recipe: PostRecipe.TroyDefault());
        Assert.Contains("G2 ", nc);
        Assert.Contains("R5.0000", nc);
        Assert.True(ClimbCut.SignedArea(path) < 0, "outer climb is CW");
        Assert.True(path.Count > 8, $"round square needs corner fans, got {path.Count}");
    }

    [Fact]
    public void Inner_window_climb_is_ccw_G3()
    {
        var source = new CutOp
        {
            Op = "contour",
            PanelId = "P1",
            FeatureId = "W1",
            ToolId = "T2",
            Placed = true,
            ClosePath = true,
            Through = true,
            ThicknessMm = 18,
            DepthMm = 18.5,
            Path = [(20, 20), (80, 20), (80, 70), (20, 70)],
        };
        var offset = ContourToolOffset.Apply([source], 5);
        var path = offset[0].Path!;
        var nc = NcEmitter.OpsToNc(offset, Machine(), recipe: PostRecipe.TroyDefault());
        // Sharp inner rectangle: Clipper inset stays G1 (no round join). Climb is CCW.
        Assert.True(ClimbCut.SignedArea(path) > 0, "inner climb is CCW");
        Assert.DoesNotContain("G2 ", nc);
    }
}
