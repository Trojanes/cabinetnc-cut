namespace CabinetNC.Domain.Manufacturing;

/// <summary>
/// M3 climb milling (shop ArtCAM): keep the material on the climb side.
/// Outer profile travels clockwise (G2). Inner / pocket wall travels CCW (G3).
/// </summary>
public static class ClimbCut
{
    /// <param name="inner">True for windows and pocket walls (tool inside the loop).</param>
    public static IReadOnlyList<(double X, double Y)> OrientClosed(
        IReadOnlyList<(double X, double Y)> path,
        bool inner)
    {
        if (path.Count < 3) return path;
        var ccw = SignedArea(path) > 0;
        var wantCcw = inner;
        if (ccw == wantCcw)
            return StartAtLongestEdge(path);
        var rev = path.ToList();
        rev.Reverse();
        return StartAtLongestEdge(rev);
    }

    /// <summary>
    /// Plunge on a long straight, not mid-corner. Stops the ~0.6 mm G1 stub at close.
    /// </summary>
    public static IReadOnlyList<(double X, double Y)> StartAtLongestEdge(
        IReadOnlyList<(double X, double Y)> path)
    {
        if (path.Count < 3) return path;
        var bestI = 0;
        var bestL = -1d;
        for (var i = 0; i < path.Count; i++)
        {
            var a = path[i];
            var b = path[(i + 1) % path.Count];
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var l = dx * dx + dy * dy;
            if (l > bestL)
            {
                bestL = l;
                bestI = i;
            }
        }
        if (bestI == 0) return path is List<(double X, double Y)> list ? list : path.ToList();
        var rot = new List<(double X, double Y)>(path.Count);
        for (var k = 0; k < path.Count; k++)
            rot.Add(path[(bestI + k) % path.Count]);
        return rot;
    }

    public static double SignedArea(IReadOnlyList<(double X, double Y)> path)
    {
        if (path.Count < 3) return 0;
        double a = 0;
        for (var i = 0; i < path.Count; i++)
        {
            var p = path[i];
            var q = path[(i + 1) % path.Count];
            a += p.X * q.Y - q.X * p.Y;
        }
        return a * 0.5;
    }
}
