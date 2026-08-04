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

    [Fact]
    public void MirrorX_flips_coords_and_edge_banding()
    {
        var p = Rect();
        p = new Panel
        {
            PanelId = p.PanelId,
            ThicknessMm = p.ThicknessMm,
            Outline = p.Outline,
            Features = p.Features,
            Side = "A",
            EdgeBanding = new EdgeBanding { Left = "L", Right = "R", Front = "F", Back = "B" },
            Orientation = new WorkpieceOrientation { MillingFace = "A", AllowMirror = true },
        };
        var next = PanelEdit.Mirror(p, "X");
        var h = next.Features.Single(f => f.FeatureId == "H1");
        Assert.Equal(520, h.X, 3); // 2*300 - 80
        Assert.Equal(80, h.Y, 3);
        Assert.Equal("R", next.EdgeBanding!.Left);
        Assert.Equal("L", next.EdgeBanding.Right);
        Assert.Equal("B", next.Side);
        Assert.Equal("B", next.Orientation!.MillingFace);
        Assert.Equal("x", next.Orientation.FlipStrategy);
    }

    [Fact]
    public void Duplicate_assigns_new_ids()
    {
        var next = PanelEdit.Duplicate(Rect(), "P1_copy");
        Assert.Equal("P1_copy", next.PanelId);
        Assert.DoesNotContain(next.Features, f => f.FeatureId == "H1");
        Assert.Contains(next.Features, f => f.FeatureId.StartsWith("H1"));
        Assert.Equal(80, next.Features.First(f => f.FeatureId.StartsWith("H1")).X);
    }

    [Fact]
    public void IsSmallPanel_by_short_edge()
    {
        var p = new Panel
        {
            PanelId = "S",
            ThicknessMm = 18,
            Outline = new Outline { Points = [new(0, 0), new(70, 0), new(70, 200), new(0, 200)] },
        };
        Assert.True(PanelEdit.IsSmallPanel(p, out var reason));
        Assert.Contains("80", reason);
    }
}
