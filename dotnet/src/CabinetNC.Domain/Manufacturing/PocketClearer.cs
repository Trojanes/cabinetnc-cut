namespace CabinetNC.Domain.Manufacturing;

using Clipper2Lib;

/// <summary>
/// Pocket area clear — Clipper inset + inward offset rings stitched into a spiral
/// (inside-out), then a separate finish loop. Not a horizontal zigzag raster.
/// ASSUMPTION: stepover = 40% tool Ø; finish/onion allowance = 0.5 mm on walls.
/// </summary>
public static class PocketClearer
{
    public const double DefaultOnionSkinMm = 0.5;
    public const double DefaultStepoverRatio = 0.4;
    const double Scale = 10000;

    public sealed class PocketClearRequest
    {
        public required IReadOnlyList<(double X, double Y)> Outline { get; init; }
        public double ToolDiameterMm { get; init; } = 6.35;
        public double? StepoverMm { get; init; }
        public double OnionSkinMm { get; init; } = DefaultOnionSkinMm;
    }

    public sealed class PocketClearResult
    {
        public required IReadOnlyList<(double X, double Y)> Path { get; init; }
        /// <summary>Spiral fill as one (or few) polylines. Finish is <see cref="FinishLoop"/>.</summary>
        public IReadOnlyList<IReadOnlyList<(double X, double Y)>> Segments { get; init; } = [];
        public IReadOnlyList<(double X, double Y)>? FinishLoop { get; init; }
        public int PassCount { get; init; }
        public double StepoverMm { get; init; }
        public double InsetMm { get; init; }
        /// <summary>True when inset region cannot fit the tool (no silent skip).</summary>
        public bool TooSmallForTool { get; init; }
    }

    public static PocketClearResult Clear(PocketClearRequest req)
    {
        if (req.Outline.Count < 3)
            return new PocketClearResult { Path = req.Outline, PassCount = 0, StepoverMm = 0, InsetMm = 0 };

        var toolR = Math.Max(0.1, req.ToolDiameterMm / 2);
        var onion = Math.Max(0, req.OnionSkinMm);
        var inset = toolR + onion;
        var step = req.StepoverMm ?? Math.Max(0.5, req.ToolDiameterMm * DefaultStepoverRatio);

        var outer = ToPath64(req.Outline);
        var insetPaths = Clipper.InflatePaths(
            new Paths64 { outer },
            -inset * Scale,
            JoinType.Round,
            EndType.Polygon);
        if (insetPaths.Count == 0 || insetPaths[0].Count < 3)
        {
            var cx = req.Outline.Average(p => p.X);
            var cy = req.Outline.Average(p => p.Y);
            return new PocketClearResult
            {
                Path = [(cx, cy)],
                Segments = [],
                PassCount = 0,
                StepoverMm = step,
                InsetMm = inset,
                TooSmallForTool = true,
            };
        }

        var region = Largest(insetPaths);
        EnsureCcw(region);

        var rings = OffsetRings(region, step);
        var spiral = StitchSpiralInsideOut(rings);
        IReadOnlyList<(double X, double Y)>? finish = ClosedLoop(region);

        var flat = new List<(double X, double Y)>();
        if (spiral.Count >= 2)
            flat.AddRange(spiral);
        if (finish is not null)
            flat.AddRange(finish);

        IReadOnlyList<IReadOnlyList<(double X, double Y)>> segments =
            spiral.Count >= 2 ? [spiral] : [];

        return new PocketClearResult
        {
            Path = flat,
            Segments = segments,
            FinishLoop = finish,
            PassCount = Math.Max(1, rings.Count),
            StepoverMm = step,
            InsetMm = inset,
        };
    }

    static List<Path64> OffsetRings(Path64 outer, double stepMm)
    {
        var rings = new List<Path64> { outer };
        var current = new Paths64 { outer };
        for (var i = 0; i < 80; i++)
        {
            var next = Clipper.InflatePaths(
                current, -stepMm * Scale, JoinType.Round, EndType.Polygon);
            if (next.Count == 0) break;
            var ring = Largest(next);
            if (ring.Count < 3) break;
            EnsureCcw(ring);
            if (Math.Abs(Clipper.Area(ring)) < Scale * Scale * 0.5)
                break;
            rings.Add(ring);
            current = [ring];
        }
        return rings;
    }

    static List<(double X, double Y)> StitchSpiralInsideOut(IReadOnlyList<Path64> outerToInner)
    {
        var spiral = new List<(double X, double Y)>();
        if (outerToInner.Count == 0) return spiral;

        (double X, double Y)? last = null;
        for (var r = outerToInner.Count - 1; r >= 0; r--)
        {
            var pts = ToPoints(outerToInner[r]);
            if (pts.Count < 3) continue;
            var start = last is { } p ? NearestIndex(pts, p) : MinXIndex(pts);
            RotateInPlace(pts, start);
            if (last is not null)
                spiral.Add(pts[0]);
            for (var i = 0; i < pts.Count; i++)
                spiral.Add(pts[i]);
            last = pts[^1];
        }
        return spiral;
    }

    static Path64 Largest(Paths64 paths) =>
        paths.OrderByDescending(p => Math.Abs(Clipper.Area(p))).First();

    static void EnsureCcw(Path64 path)
    {
        if (Clipper.Area(path) < 0)
            path.Reverse();
    }

    static List<(double X, double Y)> ToPoints(Path64 path)
    {
        var pts = new List<(double X, double Y)>(path.Count);
        foreach (var p in path)
            pts.Add((p.X / Scale, p.Y / Scale));
        if (pts.Count >= 2)
        {
            var a = pts[0];
            var b = pts[^1];
            if (Math.Abs(a.X - b.X) < 1e-6 && Math.Abs(a.Y - b.Y) < 1e-6)
                pts.RemoveAt(pts.Count - 1);
        }
        return pts;
    }

    static IReadOnlyList<(double X, double Y)> ClosedLoop(Path64 region)
    {
        var loop = new List<(double X, double Y)>(region.Count + 1);
        foreach (var p in region)
            loop.Add((p.X / Scale, p.Y / Scale));
        loop.Add((region[0].X / Scale, region[0].Y / Scale));
        return loop;
    }

    static int NearestIndex(IReadOnlyList<(double X, double Y)> pts, (double X, double Y) p)
    {
        var best = 0;
        var bestD = double.PositiveInfinity;
        for (var i = 0; i < pts.Count; i++)
        {
            var dx = pts[i].X - p.X;
            var dy = pts[i].Y - p.Y;
            var d = dx * dx + dy * dy;
            if (d >= bestD) continue;
            bestD = d;
            best = i;
        }
        return best;
    }

    static int MinXIndex(IReadOnlyList<(double X, double Y)> pts)
    {
        var best = 0;
        for (var i = 1; i < pts.Count; i++)
            if (pts[i].X < pts[best].X) best = i;
        return best;
    }

    static void RotateInPlace(List<(double X, double Y)> pts, int start)
    {
        if (start <= 0 || start >= pts.Count) return;
        var head = pts.GetRange(0, start);
        pts.RemoveRange(0, start);
        pts.AddRange(head);
    }

    static Path64 ToPath64(IReadOnlyList<(double X, double Y)> pts)
    {
        var path = new Path64(pts.Count);
        foreach (var p in pts)
            path.Add(new Point64((long)Math.Round(p.X * Scale), (long)Math.Round(p.Y * Scale)));
        return path;
    }
}
