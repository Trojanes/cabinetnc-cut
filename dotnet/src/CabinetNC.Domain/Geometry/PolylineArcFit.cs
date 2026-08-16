namespace CabinetNC.Domain.Geometry;

/// <summary>
/// Collapse Clipper round-join fans into OSAI <c>G2/G3 R</c> arcs.
/// Sharp polyline corners stay as <c>G1</c> (a 3-point L is not an arc).
/// </summary>
public static class PolylineArcFit
{
    public const double PointTolMm = 0.05;
    public const double MinRadiusMm = 0.5;
    public const double MaxRadiusMm = 400;
    public const double MinSweepDeg = 8;
    public const double MinSagittaMm = 0.06;

    static readonly double[] SnapRadii =
    [
        3.175, 5, 5.715, 8.255, 14.497, 20.504, 20.584, 21.58,
    ];

    public readonly record struct Seg(bool Arc, bool Cw, double X, double Y, double R);

    public static IReadOnlyList<Seg> Fit(IReadOnlyList<(double X, double Y)> path, bool closed = false)
    {
        var pts = Dedup(path);
        if (closed && pts.Count >= 3)
        {
            var a = pts[0];
            var b = pts[^1];
            if (Math.Abs(a.X - b.X) > 1e-6 || Math.Abs(a.Y - b.Y) > 1e-6)
                pts.Add(a);
        }

        var segs = new List<Seg>();
        if (pts.Count < 2) return segs;
        var i = 0;
        while (i < pts.Count - 1)
        {
            if (TryArc(pts, i, out var end, out var cw, out var r))
            {
                segs.Add(new Seg(true, cw, pts[end].X, pts[end].Y, SnapRadius(r)));
                i = end;
                continue;
            }
            segs.Add(new Seg(false, false, pts[i + 1].X, pts[i + 1].Y, 0));
            i++;
        }
        return segs;
    }

    static bool TryArc(
        IReadOnlyList<(double X, double Y)> pts,
        int i,
        out int end,
        out bool cw,
        out double r)
    {
        end = i;
        cw = false;
        r = 0;
        if (i + 2 >= pts.Count) return false;

        var bestEnd = -1;
        double bestR = 0, bestCx = 0, bestCy = 0;
        var bestCw = false;

        for (var j = i + 2; j < pts.Count; j++)
        {
            var mid = (i + j) / 2;
            if (mid <= i) mid = i + 1;
            if (mid >= j) mid = j - 1;
            if (!CircleThrough(pts[i], pts[mid], pts[j], out var cx, out var cy, out var rr, out var ccw)
                && !CircleThrough(pts[i], pts[i + 1], pts[j], out cx, out cy, out rr, out ccw))
            {
                if (bestEnd >= 0) break;
                continue;
            }
            if (!AcceptArc(pts, i, j, cx, cy, rr, ccw))
            {
                if (bestEnd >= 0) break;
                continue;
            }
            bestEnd = j;
            bestR = rr;
            bestCw = ccw;
            bestCx = cx;
            bestCy = cy;
        }

        if (bestEnd < i + 2) return false;
        end = bestEnd;
        r = bestR;
        cw = bestCw;

        if (end == i + 2)
        {
            var l0 = Dist(pts[i], pts[i + 1]);
            var l1 = Dist(pts[i + 1], pts[i + 2]);
            var lo = Math.Min(l0, l1);
            if (lo < 1e-9) return false;
            if (Math.Max(l0, l1) / lo > 1.8) return false;
        }

        var sweepDeg = SweepDeg(pts[i], pts[end], bestCx, bestCy, cw);
        var sagitta = r * (1 - Math.Cos(sweepDeg * Math.PI / 360));
        if (sweepDeg < MinSweepDeg && sagitta < MinSagittaMm) return false;
        if (sagitta < MinSagittaMm && end < i + 3) return false;
        return true;
    }

    static bool AcceptArc(
        IReadOnlyList<(double X, double Y)> pts,
        int i,
        int j,
        double cx, double cy, double r, bool cw)
    {
        if (r < MinRadiusMm || r > MaxRadiusMm) return false;
        var sweep = SweepDeg(pts[i], pts[j], cx, cy, cw);
        if (sweep > 180.5) return false;
        if (!AllOnCircle(pts, i, j, cx, cy, r)) return false;
        if (!SameTurn(pts, i, j, cw)) return false;
        if (!AllShort(pts, i, j, r)) return false;
        return true;
    }

    static bool AllOnCircle(
        IReadOnlyList<(double X, double Y)> pts, int i, int j, double cx, double cy, double r)
    {
        for (var k = i; k <= j; k++)
            if (!OnCircle(pts[k], cx, cy, r)) return false;
        return true;
    }

    static bool SameTurn(IReadOnlyList<(double X, double Y)> pts, int i, int j, bool cw)
    {
        for (var k = i + 1; k < j; k++)
        {
            var turn = Cross(pts[k - 1], pts[k], pts[k + 1]);
            if (cw ? turn > 1e-9 : turn < -1e-9) return false;
        }
        return true;
    }

    static bool CircleThrough(
        (double X, double Y) a,
        (double X, double Y) b,
        (double X, double Y) c,
        out double cx, out double cy, out double r, out bool cw)
    {
        cx = cy = r = 0;
        cw = false;
        var d = 2 * (a.X * (b.Y - c.Y) + b.X * (c.Y - a.Y) + c.X * (a.Y - b.Y));
        if (Math.Abs(d) < 1e-12) return false;
        var a2 = a.X * a.X + a.Y * a.Y;
        var b2 = b.X * b.X + b.Y * b.Y;
        var c2 = c.X * c.X + c.Y * c.Y;
        cx = (a2 * (b.Y - c.Y) + b2 * (c.Y - a.Y) + c2 * (a.Y - b.Y)) / d;
        cy = (a2 * (c.X - b.X) + b2 * (a.X - c.X) + c2 * (b.X - a.X)) / d;
        r = Dist(a, (cx, cy));
        if (r < 1e-9) return false;
        cw = Cross(a, b, c) < 0;
        return true;
    }

    static bool OnCircle((double X, double Y) p, double cx, double cy, double r) =>
        Math.Abs(Dist(p, (cx, cy)) - r) <= PointTolMm;

    static bool AllShort(IReadOnlyList<(double X, double Y)> pts, int i, int end, double r)
    {
        for (var k = i; k < end; k++)
            if (!ShortChord(pts[k], pts[k + 1], r)) return false;
        return true;
    }

    static bool ShortChord((double X, double Y) a, (double X, double Y) b, double r) =>
        Dist(a, b) <= Math.Max(2.2, 0.45 * r);

    static double SweepDeg(
        (double X, double Y) start,
        (double X, double Y) end,
        double cx, double cy, bool cw)
    {
        var a0 = Math.Atan2(start.Y - cy, start.X - cx);
        var a1 = Math.Atan2(end.Y - cy, end.X - cx);
        var d = a1 - a0;
        if (cw)
        {
            if (d > 0) d -= 2 * Math.PI;
            return -d * 180 / Math.PI;
        }
        if (d < 0) d += 2 * Math.PI;
        return d * 180 / Math.PI;
    }

    static double SnapRadius(double r)
    {
        foreach (var s in SnapRadii)
            if (Math.Abs(r - s) <= 0.05) return s;
        return Math.Round(r, 4, MidpointRounding.AwayFromZero);
    }

    static List<(double X, double Y)> Dedup(IReadOnlyList<(double X, double Y)> path)
    {
        var pts = new List<(double X, double Y)>(path.Count);
        foreach (var p in path)
        {
            if (pts.Count > 0 && Dist(pts[^1], p) < 1e-4) continue;
            pts.Add(p);
        }
        return pts;
    }

    static double Dist((double X, double Y) a, (double X, double Y) b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    static double Cross((double X, double Y) a, (double X, double Y) b, (double X, double Y) c) =>
        (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);
}
