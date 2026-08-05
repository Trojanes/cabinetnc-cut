namespace CabinetNC.Domain.Manufacturing;

using Clipper2Lib;

/// <summary>
/// Pocket area clear v1 — Clipper inset + zigzag fill (not boundary-only).
/// ASSUMPTION: stepover = 40% tool Ø; finish/onion allowance = 0.5 mm on walls.
/// Scan strokes are disjoint segments; finish inset is a separate closed loop.
/// </summary>
public static class PocketClearer
{
    public const double DefaultOnionSkinMm = 0.5;
    public const double DefaultStepoverRatio = 0.4;
    const double Scale = 1000;

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
        /// <summary>Disjoint motion segments (each scan stroke).</summary>
        public IReadOnlyList<IReadOnlyList<(double X, double Y)>> Segments { get; init; } = [];
        public IReadOnlyList<(double X, double Y)>? FinishLoop { get; init; }
        public int PassCount { get; init; }
        public double StepoverMm { get; init; }
        public double InsetMm { get; init; }
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
            // Too small for tool+onion — fall back to center point only (still not a fake loop claim)
            var cx = req.Outline.Average(p => p.X);
            var cy = req.Outline.Average(p => p.Y);
            return new PocketClearResult
            {
                Path = [(cx, cy)],
                PassCount = 0,
                StepoverMm = step,
                InsetMm = inset,
            };
        }

        var region = insetPaths[0];
        var minX = region.Min(p => p.X) / Scale;
        var maxX = region.Max(p => p.X) / Scale;
        var minY = region.Min(p => p.Y) / Scale;
        var maxY = region.Max(p => p.Y) / Scale;

        var segments = new List<IReadOnlyList<(double X, double Y)>>();
        var flat = new List<(double X, double Y)>();
        var pass = 0;
        var leftToRight = true;
        for (var y = minY; y <= maxY + 1e-9; y += step)
        {
            var yScaled = (long)Math.Round(y * Scale);
            // Horizontal scan line clipped to polygon via intersection with a thin band
            var band = new Path64
            {
                new Point64((long)Math.Round(minX * Scale) - 10, yScaled - 1),
                new Point64((long)Math.Round(maxX * Scale) + 10, yScaled - 1),
                new Point64((long)Math.Round(maxX * Scale) + 10, yScaled + 1),
                new Point64((long)Math.Round(minX * Scale) - 10, yScaled + 1),
            };
            var hits = Clipper.Intersect(new Paths64 { region }, new Paths64 { band }, FillRule.NonZero);
            var segs = new List<(double x0, double x1)>();
            foreach (var hit in hits)
            {
                if (hit.Count == 0) continue;
                var xs = hit.Select(p => p.X / Scale).OrderBy(v => v).ToList();
                segs.Add((xs.First(), xs.Last()));
            }
            segs = segs.OrderBy(s => s.x0).ToList();
            if (segs.Count == 0) continue;
            pass++;
            if (!leftToRight) segs.Reverse();
            foreach (var (x0, x1) in segs)
            {
                (double X, double Y) a, b;
                if (leftToRight)
                {
                    a = (x0, y);
                    b = (x1, y);
                }
                else
                {
                    a = (x1, y);
                    b = (x0, y);
                }
                segments.Add([a, b]);
                flat.Add(a);
                flat.Add(b);
            }
            leftToRight = !leftToRight;
        }

        // Finish: one boundary pass on inset (onion skin leave stock on outer wall) — separate loop
        IReadOnlyList<(double X, double Y)>? finish = null;
        if (region.Count >= 3)
        {
            var loop = new List<(double X, double Y)>(region.Count + 1);
            foreach (var p in region)
                loop.Add((p.X / Scale, p.Y / Scale));
            loop.Add((region[0].X / Scale, region[0].Y / Scale));
            finish = loop;
            flat.AddRange(loop);
        }

        return new PocketClearResult
        {
            Path = flat,
            Segments = segments,
            FinishLoop = finish,
            PassCount = pass,
            StepoverMm = step,
            InsetMm = inset,
        };
    }

    static Path64 ToPath64(IReadOnlyList<(double X, double Y)> pts)
    {
        var path = new Path64(pts.Count);
        foreach (var p in pts)
            path.Add(new Point64((long)Math.Round(p.X * Scale), (long)Math.Round(p.Y * Scale)));
        return path;
    }
}
