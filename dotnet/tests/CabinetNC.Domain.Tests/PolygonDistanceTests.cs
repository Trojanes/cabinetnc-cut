using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Nesting;
using CabinetNC.Domain.Parts;

namespace CabinetNC.Domain.Tests;

public class PolygonDistanceTests
{
    static IReadOnlyList<Point2> Rect(double x, double y, double w, double h) =>
        [new(x, y), new(x + w, y), new(x + w, y + h), new(x, y + h)];

    [Fact]
    public void Closest_two_rects_gap_is_between_facing_edges()
    {
        var a = Rect(0, 0, 10, 10);
        var b = Rect(20, 0, 10, 10);
        var pair = PolygonDistance.Closest(a, b);
        Assert.Equal(10, pair.Distance, 6);
        Assert.Equal(10, pair.A.X, 6);
        Assert.Equal(20, pair.B.X, 6);
        Assert.InRange(pair.A.Y, 0, 10);
        Assert.InRange(pair.B.Y, 0, 10);
        Assert.Equal(pair.A.Y, pair.B.Y, 6);
    }

    [Fact]
    public void Closest_vertex_to_edge()
    {
        var a = Rect(0, 0, 10, 10);
        var b = Rect(15, 25, 4, 4);
        var pair = PolygonDistance.Closest(a, b);
        // Closest is (10,10) on A to (15,25) wait — actually (10,10) to bottom of B?
        // Bottom of B is y=25, x in [15,19]. From A's top-right (10,10) to (15,25)
        // From A's top edge (x,10) x in [0,10] to (15,25): dist sqrt((15-10)^2+(25-10)^2)=sqrt(25+225)=sqrt(250)≈15.81
        // From (10,10) to (15,25) same.
        // From A's right (10,y) to B bottom-left (15,25): min at y=10 → same.
        Assert.Equal(Math.Sqrt(5 * 5 + 15 * 15), pair.Distance, 6);
    }

    [Fact]
    public void Closest_crossing_segments_is_zero()
    {
        Point2[] a = [new(0, 0), new(10, 0), new(10, 10), new(0, 10)];
        Point2[] b = [new(4, -2), new(6, -2), new(6, 12), new(4, 12)];
        var pair = PolygonDistance.Closest(a, b);
        Assert.Equal(0, pair.Distance, 6);
    }

    [Fact]
    public void Closest_parallel_offset_uses_perpendicular()
    {
        var a = Rect(0, 0, 30, 8);
        var b = Rect(5, 18, 10, 8);
        var pair = PolygonDistance.Closest(a, b);
        Assert.Equal(10, pair.Distance, 6);
        Assert.Equal(8, pair.A.Y, 6);
        Assert.Equal(18, pair.B.Y, 6);
        Assert.InRange(pair.A.X, 5, 15);
        Assert.InRange(pair.B.X, 5, 15);
    }

    [Fact]
    public void SheetOutline_then_closest_matches_placed_gap()
    {
        var panelA = new Panel
        {
            PanelId = "A",
            ThicknessMm = 18,
            Outline = new Outline { Points = Rect(0, 0, 100, 40) },
        };
        var panelB = new Panel
        {
            PanelId = "B",
            ThicknessMm = 18,
            Outline = new Outline { Points = Rect(0, 0, 50, 40) },
        };
        var ringA = NestTransform.SheetOutline(panelA, 0, 0, 0);
        var ringB = NestTransform.SheetOutline(panelB, 120, 0, 0);
        var pair = PolygonDistance.Closest(ringA, ringB);
        Assert.Equal(20, pair.Distance, 6);
    }
}
