namespace CabinetNC.Domain.Geometry;

/// <summary>Shortest distance between two 2D polygons (closest points on the boundaries).</summary>
public static class PolygonDistance
{
    public readonly record struct Pair(Point2 A, Point2 B, double Distance);

    public static Pair Closest(IReadOnlyList<Point2> a, IReadOnlyList<Point2> b)
    {
        if (a is not { Count: > 0 } || b is not { Count: > 0 })
            return new Pair(default, default, double.NaN);

        var best = new Pair(a[0], b[0], double.PositiveInfinity);
        foreach (var (a0, a1) in Edges(a))
        foreach (var (b0, b1) in Edges(b))
        {
            var (pa, pb, d) = ClosestOnSegments(a0, a1, b0, b1);
            if (d < best.Distance)
                best = new Pair(pa, pb, d);
        }

        if (double.IsPositiveInfinity(best.Distance))
        {
            var dx = a[0].X - b[0].X;
            var dy = a[0].Y - b[0].Y;
            return new Pair(a[0], b[0], Math.Sqrt(dx * dx + dy * dy));
        }

        return best;
    }

    /// <summary>Closest point on the polygon boundary to <paramref name="p"/>.</summary>
    public static (Point2 Point, double Distance) ClosestPoint(IReadOnlyList<Point2> poly, Point2 p)
    {
        if (poly is not { Count: > 0 })
            return (default, double.NaN);

        var best = poly[0];
        var bestD = double.PositiveInfinity;
        foreach (var (a, b) in Edges(poly))
        {
            var q = ClosestOnSegment(p, a, b);
            var dx = q.X - p.X;
            var dy = q.Y - p.Y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            if (d >= bestD) continue;
            bestD = d;
            best = q;
        }

        if (double.IsPositiveInfinity(bestD))
        {
            var dx = poly[0].X - p.X;
            var dy = poly[0].Y - p.Y;
            return (poly[0], Math.Sqrt(dx * dx + dy * dy));
        }

        return (best, bestD);
    }

    /// <summary>
    /// Unit outward normal at the boundary point nearest to <paramref name="near"/>.
    /// CCW winding → rotate tangent clockwise; CW → opposite.
    /// </summary>
    public static (double X, double Y) OutwardUnitNormal(IReadOnlyList<Point2> poly, Point2 near)
    {
        if (poly is not { Count: >= 2 })
            return (0, 0);

        Point2 a0 = poly[0], a1 = poly[1];
        var bestD = double.PositiveInfinity;
        foreach (var (a, b) in Edges(poly))
        {
            var q = ClosestOnSegment(near, a, b);
            var dx = q.X - near.X;
            var dy = q.Y - near.Y;
            var d = dx * dx + dy * dy;
            if (d >= bestD) continue;
            bestD = d;
            a0 = a;
            a1 = b;
        }

        var tx = a1.X - a0.X;
        var ty = a1.Y - a0.Y;
        var len = Math.Sqrt(tx * tx + ty * ty);
        if (len < 1e-12)
            return (0, 0);

        var ccw = SignedArea(poly) >= 0;
        var nx = ccw ? ty / len : -ty / len;
        var ny = ccw ? -tx / len : tx / len;
        return (nx, ny);
    }

    public static double SignedArea(IReadOnlyList<Point2> poly)
    {
        if (poly is not { Count: >= 3 }) return 0;
        double a = 0;
        for (var i = 0; i < poly.Count; i++)
        {
            var p = poly[i];
            var q = poly[(i + 1) % poly.Count];
            a += p.X * q.Y - q.X * p.Y;
        }
        return a * 0.5;
    }

    static IEnumerable<(Point2 A, Point2 B)> Edges(IReadOnlyList<Point2> poly)
    {
        if (poly.Count == 1)
        {
            yield return (poly[0], poly[0]);
            yield break;
        }

        for (var i = 0; i < poly.Count; i++)
        {
            var j = (i + 1) % poly.Count;
            var dx = poly[j].X - poly[i].X;
            var dy = poly[j].Y - poly[i].Y;
            if (dx * dx + dy * dy < 1e-18)
                continue;
            yield return (poly[i], poly[j]);
        }
    }

    static (Point2 A, Point2 B, double Dist) ClosestOnSegments(
        Point2 a0, Point2 a1, Point2 b0, Point2 b1)
    {
        if (TryIntersect(a0, a1, b0, b1, out var hit))
            return (hit, hit, 0);

        var bestD = double.PositiveInfinity;
        var pa = a0;
        var pb = b0;

        void Consider(Point2 p, Point2 q)
        {
            var dx = q.X - p.X;
            var dy = q.Y - p.Y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            if (d >= bestD) return;
            bestD = d;
            pa = p;
            pb = q;
        }

        Consider(a0, ClosestOnSegment(a0, b0, b1));
        Consider(a1, ClosestOnSegment(a1, b0, b1));
        Consider(ClosestOnSegment(b0, a0, a1), b0);
        Consider(ClosestOnSegment(b1, a0, a1), b1);
        return (pa, pb, bestD);
    }

    static Point2 ClosestOnSegment(Point2 p, Point2 a, Point2 b)
    {
        var vx = b.X - a.X;
        var vy = b.Y - a.Y;
        var len2 = vx * vx + vy * vy;
        if (len2 < 1e-18) return a;
        var t = ((p.X - a.X) * vx + (p.Y - a.Y) * vy) / len2;
        t = t < 0 ? 0 : t > 1 ? 1 : t;
        return new Point2(a.X + t * vx, a.Y + t * vy);
    }

    static bool TryIntersect(Point2 a0, Point2 a1, Point2 b0, Point2 b1, out Point2 hit)
    {
        hit = default;
        var dax = a1.X - a0.X;
        var day = a1.Y - a0.Y;
        var dbx = b1.X - b0.X;
        var dby = b1.Y - b0.Y;
        var den = dax * dby - day * dbx;
        if (Math.Abs(den) < 1e-12) return false;

        var dx = b0.X - a0.X;
        var dy = b0.Y - a0.Y;
        var t = (dx * dby - dy * dbx) / den;
        var u = (dx * day - dy * dax) / den;
        if (t is < -1e-9 or > 1 + 1e-9 || u is < -1e-9 or > 1 + 1e-9)
            return false;

        hit = new Point2(a0.X + t * dax, a0.Y + t * day);
        return true;
    }
}
