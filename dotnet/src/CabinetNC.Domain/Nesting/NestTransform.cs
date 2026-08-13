namespace CabinetNC.Domain.Nesting;

using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Parts;

public readonly record struct LocalBounds(double MinX, double MinY, double MaxX, double MaxY);

/// <summary>
/// Maps panel-local coordinates into sheet space where placement offset is the
/// rotated polygon's lower-left AABB corner (the contract used by BLF/NestDrag).
/// </summary>
public static class NestTransform
{
    public static LocalBounds BoundsOf(Panel panel) => BoundsOf(
        panel.Outline.Points.Select(p => (p.X, p.Y)));

    public static LocalBounds BoundsOf(IEnumerable<(double X, double Y)> points)
    {
        var list = points.ToList();
        if (list.Count == 0) return default;
        return new(
            list.Min(p => p.X),
            list.Min(p => p.Y),
            list.Max(p => p.X),
            list.Max(p => p.Y));
    }

    public static (double X, double Y) ToSheet(
        double x,
        double y,
        LocalBounds bounds,
        double offsetX,
        double offsetY,
        double rotationDeg)
    {
        var r = rotationDeg * Math.PI / 180.0;
        var c = Math.Cos(r);
        var s = Math.Sin(r);
        var rx = x * c - y * s;
        var ry = x * s + y * c;

        var corners = new[]
        {
            Rotate(bounds.MinX, bounds.MinY, c, s),
            Rotate(bounds.MaxX, bounds.MinY, c, s),
            Rotate(bounds.MaxX, bounds.MaxY, c, s),
            Rotate(bounds.MinX, bounds.MaxY, c, s),
        };
        var minX = corners.Min(p => p.X);
        var minY = corners.Min(p => p.Y);
        return (rx - minX + offsetX, ry - minY + offsetY);
    }

    /// <summary>Panel outline mapped into sheet space (same transform as nest painting).</summary>
    public static IReadOnlyList<Point2> SheetOutline(
        Panel panel, double offsetX, double offsetY, double rotationDeg)
    {
        var pts = panel.Outline.Points;
        if (pts.Count == 0) return [];
        var bounds = BoundsOf(panel);
        var list = new List<Point2>(pts.Count);
        foreach (var pt in pts)
        {
            var (x, y) = ToSheet(pt.X, pt.Y, bounds, offsetX, offsetY, rotationDeg);
            list.Add(new Point2(x, y));
        }
        return list;
    }

    /// <summary>Panel-local outline rotated the same way as sheet placement (AABB rebased to origin).</summary>
    public static IReadOnlyList<(double X, double Y)> RotatedOutline(
        IReadOnlyList<(double X, double Y)> points,
        double rotationDeg)
    {
        var list = points.ToList();
        if (list.Count < 2) return list;
        var r = ((int)Math.Round(rotationDeg) % 360 + 360) % 360;
        if (r == 0) return list;
        var bounds = BoundsOf(list);
        return list.Select(p => ToSheet(p.X, p.Y, bounds, 0, 0, r)).ToList();
    }

    static (double X, double Y) Rotate(double x, double y, double c, double s) =>
        (x * c - y * s, x * s + y * c);
}
