namespace CabinetNC.Domain.Geometry;

/// <summary>
/// Collapse real corner/cup fans into OSAI <c>G2/G3 R</c> arcs.
/// Shallow bows (R larger than shop fillets) become one <c>G1</c> — they are
/// tessellated straight edges, not part radii.
/// Sharp polyline corners stay as <c>G1</c> (a 3-point L is not an arc).
/// </summary>
public static class PolylineArcFit
{
    public const double PointTolMm = 0.05;
    public const double MinRadiusMm = 0.5;
    /// <summary>Above shop snap radii (≤21.58). R256-class “straight” edges stay G1.</summary>
    public const double MaxRadiusMm = 22;
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
            var straight = GrowStraight(pts, i);
            segs.Add(new Seg(false, false, pts[straight].X, pts[straight].Y, 0));
            i = straight;
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

    /// <summary>
    /// Collapse a tessellated shallow bow (R &gt; <see cref="MaxRadiusMm"/>) or
    /// a colinear run into one chord. Stops before a real corner fan.
    /// </summary>
    static int GrowStraight(IReadOnlyList<(double X, double Y)> pts, int i)
    {
        var end = i + 1;
        for (var j = i + 2; j < pts.Count; j++)
        {
            if (!IsStraightish(pts, i, j))
                break;
            end = j;
        }
        return end;
    }

    static bool IsStraightish(IReadOnlyList<(double X, double Y)> pts, int i, int j)
    {
        if (j <= i + 1) return true;
        if (HasSharpCorner(pts, i, j))
            return false;
        if (AllOnChord(pts, i, j))
            return true;
        var mid = (i + j) / 2;
        if (mid <= i) mid = i + 1;
        if (mid >= j) mid = j - 1;
        if (!CircleThrough(pts[i], pts[mid], pts[j], out var cx, out var cy, out var r, out var cw)
            && !CircleThrough(pts[i], pts[i + 1], pts[j], out cx, out cy, out r, out cw))
            return false;
        if (r <= MaxRadiusMm)
            return false;
        return AllOnCircle(pts, i, j, cx, cy, r) && SameTurn(pts, i, j, cw);
    }

    static bool HasSharpCorner(IReadOnlyList<(double X, double Y)> pts, int i, int j)
    {
        const double maxTurnDeg = 18;
        for (var k = i + 1; k < j; k++)
        {
            var ax = pts[k].X - pts[k - 1].X;
            var ay = pts[k].Y - pts[k - 1].Y;
            var bx = pts[k + 1].X - pts[k].X;
            var by = pts[k + 1].Y - pts[k].Y;
            var d0 = Math.Sqrt(ax * ax + ay * ay);
            var d1 = Math.Sqrt(bx * bx + by * by);
            if (d0 < 1e-9 || d1 < 1e-9) continue;
            var cross = ax * by - ay * bx;
            var dot = ax * bx + ay * by;
            var deg = Math.Abs(Math.Atan2(cross, dot)) * 180 / Math.PI;
            if (deg > maxTurnDeg) return true;
        }
        return false;
    }

    static bool AllOnChord(IReadOnlyList<(double X, double Y)> pts, int i, int j)
    {
        var a = pts[i];
        var b = pts[j];
        var chord = Dist(a, b);
        if (chord < 1e-9) return false;
        for (var k = i + 1; k < j; k++)
        {
            if (DistToSegment(pts[k], a, b) > PointTolMm)
                return false;
        }
        return true;
    }

    static double DistToSegment((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var len2 = dx * dx + dy * dy;
        if (len2 < 1e-18) return Dist(p, a);
        var t = ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2;
        t = Math.Clamp(t, 0, 1);
        return Dist(p, (a.X + t * dx, a.Y + t * dy));
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
        Dist(a, b) <= Math.Max(2.5, 0.45 * r);

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
