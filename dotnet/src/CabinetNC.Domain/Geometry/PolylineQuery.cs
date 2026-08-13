namespace CabinetNC.Domain.Geometry;

/// <summary>Nearest-point / arc-length queries on open or closed polylines (mm).</summary>
public static class PolylineQuery
{
    public readonly record struct Hit(
        int SegIndex,
        double T,
        double ArcLengthMm,
        double X,
        double Y,
        double Distance,
        double TangentX,
        double TangentY);

    public static double Length(IReadOnlyList<(double X, double Y)> path, bool closed)
    {
        if (path is not { Count: >= 2 }) return 0;
        double len = 0;
        var n = closed ? path.Count : path.Count - 1;
        for (var i = 0; i < n; i++)
        {
            var a = path[i];
            var b = path[(i + 1) % path.Count];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            len += Math.Sqrt(dx * dx + dy * dy);
        }
        return len;
    }

    public static Hit? Nearest(IReadOnlyList<(double X, double Y)> path, double x, double y, bool closed)
    {
        if (path is not { Count: >= 2 }) return null;

        var best = new Hit(0, 0, 0, path[0].X, path[0].Y, double.PositiveInfinity, 1, 0);
        double arcBefore = 0;
        var n = closed ? path.Count : path.Count - 1;
        for (var i = 0; i < n; i++)
        {
            var a = path[i];
            var b = path[(i + 1) % path.Count];
            var vx = b.X - a.X;
            var vy = b.Y - a.Y;
            var len2 = vx * vx + vy * vy;
            double t;
            double px, py;
            if (len2 < 1e-18)
            {
                t = 0;
                px = a.X;
                py = a.Y;
            }
            else
            {
                t = ((x - a.X) * vx + (y - a.Y) * vy) / len2;
                t = t < 0 ? 0 : t > 1 ? 1 : t;
                px = a.X + t * vx;
                py = a.Y + t * vy;
            }

            var dx = px - x;
            var dy = py - y;
            var d = Math.Sqrt(dx * dx + dy * dy);
            var segLen = Math.Sqrt(len2);
            if (d < best.Distance)
            {
                var tx = segLen > 1e-12 ? vx / segLen : 1;
                var ty = segLen > 1e-12 ? vy / segLen : 0;
                best = new Hit(i, t, arcBefore + t * segLen, px, py, d, tx, ty);
            }

            arcBefore += segLen;
        }

        return double.IsPositiveInfinity(best.Distance) ? null : best;
    }

    public static (double X, double Y)? PointAtArc(
        IReadOnlyList<(double X, double Y)> path, double arcMm, bool closed)
    {
        if (path is not { Count: >= 2 }) return null;
        var total = Length(path, closed);
        if (total < 1e-9) return (path[0].X, path[0].Y);
        var tArc = arcMm % total;
        if (tArc < 0) tArc += total;

        var n = closed ? path.Count : path.Count - 1;
        double walked = 0;
        for (var i = 0; i < n; i++)
        {
            var a = path[i];
            var b = path[(i + 1) % path.Count];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var seg = Math.Sqrt(dx * dx + dy * dy);
            if (walked + seg >= tArc - 1e-9 || i == n - 1)
            {
                var u = seg < 1e-12 ? 0 : (tArc - walked) / seg;
                u = u < 0 ? 0 : u > 1 ? 1 : u;
                return (a.X + u * dx, a.Y + u * dy);
            }
            walked += seg;
        }

        return (path[0].X, path[0].Y);
    }
}
