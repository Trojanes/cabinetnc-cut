using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Machines;
using CabinetNC.Domain.Manufacturing;

namespace CabinetNC.Domain.Tests;

public class CadPathTests
{
    static IReadOnlyList<CadSegment> Rect(double w, double h) =>
    [
        CadSegment.MakeLine(new(0, 0), new(w, 0)),
        CadSegment.MakeLine(new(w, 0), new(w, h)),
        CadSegment.MakeLine(new(w, h), new(0, h)),
        CadSegment.MakeLine(new(0, h), new(0, 0)),
    ];

    static IReadOnlyList<CadSegment> NotchedStrip() =>
    [
        CadSegment.MakeLine(new(0, 0), new(400, 0)),
        CadSegment.MakeLine(new(400, 0), new(400, 200)),
        CadSegment.MakeLine(new(400, 200), new(250, 200)),
        CadSegment.MakeLine(new(250, 200), new(250, 180)),
        CadSegment.MakeLine(new(250, 180), new(170, 180)),
        CadSegment.MakeLine(new(170, 180), new(170, 200)),
        CadSegment.MakeLine(new(170, 200), new(0, 200)),
        CadSegment.MakeLine(new(0, 200), new(0, 0)),
    ];

    [Fact]
    public void Outer_square_offset_adds_tool_radius_arcs_only()
    {
        Assert.True(CadPath.TryOffset(Rect(100, 80), 5, roundConvex: true, out var off));
        var arcs = off.Where(s => s.IsArc).ToList();
        Assert.Equal(4, arcs.Count);
        Assert.All(arcs, a => Assert.InRange(a.RadiusMm, 4.99, 5.01));
    }

    [Fact]
    public void Notch_offset_has_no_r20_invented_arc()
    {
        Assert.True(CadPath.TryOffset(NotchedStrip(), 5, roundConvex: true, out var off));
        Assert.DoesNotContain(off, s => s.IsArc && s.RadiusMm > 8);
        var notchLines = off.Count(s => s.IsLine);
        Assert.True(notchLines >= 8);
    }

    [Fact]
    public void Cad_notch_emits_r5_corners_not_r20()
    {
        var source = new CutOp
        {
            Op = "contour",
            PanelId = "FRAME",
            ToolId = "T2",
            Placed = true,
            ClosePath = true,
            Through = true,
            ThicknessMm = 18,
            DepthMm = 18.5,
            Path = CadPath.ToPolyline(NotchedStrip()),
            CadPath = NotchedStrip(),
        };
        var offset = ContourToolOffset.Apply([source], 5);
        Assert.NotNull(offset[0].CadPath);
        Assert.DoesNotContain(offset[0].CadPath!, s => s.IsArc && s.RadiusMm > 8 && s.RadiusMm < 40);
        var nc = NcEmitter.OpsToNc(offset, MachineCatalog.Get("nesting_router_6"),
            recipe: PostRecipe.TroyDefault());
        Assert.DoesNotContain("R20.", nc);
        Assert.Contains("R5.0000", nc);
        Assert.Contains("G1 ", nc);
    }
}
