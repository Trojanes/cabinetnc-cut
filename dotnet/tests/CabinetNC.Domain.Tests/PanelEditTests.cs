using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Parts;

namespace CabinetNC.Domain.Tests;

public class PanelEditTests
{
    static Panel Rect() => new()
    {
        PanelId = "P1",
        ThicknessMm = 18,
        Outline = new Outline
        {
            Points = [new(0, 0), new(600, 0), new(600, 400), new(0, 400)],
        },
        Features =
        [
            new PanelFeature { FeatureId = "H1", Kind = "holeVertical", X = 80, Y = 80, DiameterMm = 35 },
            new PanelFeature
            {
                FeatureId = "G1",
                Kind = "grooveVertical",
                X = 0,
                Y = 50,
                WidthMm = 6,
                Path = [new(0, 50), new(600, 50)],
            },
        ],
    };

    [Fact]
    public void MoveHole_updates_xy()
    {
        var next = PanelEdit.MoveHole(Rect(), "H1", 100, 120);
        var h = next.Features.Single(f => f.FeatureId == "H1");
        Assert.Equal(100, h.X);
        Assert.Equal(120, h.Y);
    }

    [Fact]
    public void Resize_scales_features()
    {
        var next = PanelEdit.ResizeFromEdges(Rect(), 0, 0, 300, 200);
        var h = next.Features.Single(f => f.FeatureId == "H1");
        Assert.Equal(40, h.X, 3);
        Assert.Equal(40, h.Y, 3);
    }

    [Fact]
    public void HitTest_finds_hole()
    {
        var p = Rect();
        var view = GeomInteraction.BuildView(p, 800, 600);
        var (sx, sy) = GeomInteraction.ToScreen(view, 80, 80);
        var hit = GeomInteraction.HitTest(p, view, sx, sy);
        Assert.NotNull(hit);
        Assert.Equal("hole", hit!.Value.Type);
        Assert.Equal("H1", hit.Value.FeatureId);
    }
}
