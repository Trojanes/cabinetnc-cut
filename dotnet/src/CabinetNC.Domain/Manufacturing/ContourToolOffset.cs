namespace CabinetNC.Domain.Manufacturing;

using Clipper2Lib;

/// <summary>Clipper2 contour compensation in sheet-space millimetres.</summary>
public static class ContourToolOffset
{
    const double Scale = 10000;

    public static IReadOnlyList<CutOp> Apply(IEnumerable<CutOp> ops, double offsetMm)
    {
        if (Math.Abs(offsetMm) < 1e-9) return ops.ToList();

        return ops.Select(op =>
        {
            if (op.Op != "contour" || op.Path is not { Count: >= 3 } path)
                return op;

            var source = new Path64(path.Count);
            foreach (var p in path)
                source.Add(new Point64(
                    (long)Math.Round(p.X * Scale),
                    (long)Math.Round(p.Y * Scale)));

            var inflated = Clipper.InflatePaths(
                new Paths64 { source },
                (op.FeatureId is null ? offsetMm : -offsetMm) * Scale,
                JoinType.Round,
                EndType.Polygon);
            var best = inflated
                .OrderByDescending(p => Math.Abs(Clipper.Area(p)))
                .FirstOrDefault();
            if (best is null || best.Count < 3) return op;

            return op with
            {
                Path = best
                    .Select(p => (p.X / Scale, p.Y / Scale))
                    .ToList(),
            };
        }).ToList();
    }
}
