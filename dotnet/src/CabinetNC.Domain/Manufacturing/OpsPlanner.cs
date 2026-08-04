namespace CabinetNC.Domain.Manufacturing;

public sealed record CutOp
{
    public required string Op { get; init; } // contour | drill | groove
    public required string PanelId { get; init; }
    public string? FeatureId { get; init; }
    public bool Placed { get; init; }
    public int SheetIndex { get; init; }
    public double OffsetX { get; init; }
    public double OffsetY { get; init; }
    public double RotationDeg { get; init; }
    public double? X { get; init; }
    public double? Y { get; init; }
    public double? SheetX { get; init; }
    public double? SheetY { get; init; }
    public double? DiameterMm { get; init; }
    public double? DepthMm { get; init; }
    public IReadOnlyList<(double X, double Y)>? Path { get; init; }
    public Nesting.LocalBounds? PanelBounds { get; init; }
}

/// <summary>Port of src/ops.js featuresToOps + attachOpsToNest (contour + drill + groove).</summary>
public static class OpsPlanner
{
    static readonly Dictionary<string, int> Rank = new()
    {
        ["contour"] = 0,
        ["drill"] = 1,
        ["groove"] = 2,
    };

    public static IReadOnlyList<CutOp> FeaturesToOps(
        IEnumerable<Parts.Panel> panels,
        bool enableContour = true,
        bool enableDrill = true,
        bool enableGroove = true)
    {
        var ops = new List<CutOp>();
        foreach (var panel in panels)
        {
            var pts = panel.Outline.Points;
            var bounds = Nesting.NestTransform.BoundsOf(panel);
            if (enableContour && pts.Count >= 3)
            {
                ops.Add(new CutOp
                {
                    Op = "contour",
                    PanelId = panel.PanelId,
                    Path = pts.Select(p => (p.X, p.Y)).ToList(),
                    PanelBounds = bounds,
                });
            }
            foreach (var f in panel.Features)
            {
                if (enableDrill && f.Kind.Contains("hole", StringComparison.OrdinalIgnoreCase))
                {
                    ops.Add(new CutOp
                    {
                        Op = "drill",
                        PanelId = panel.PanelId,
                        FeatureId = f.FeatureId,
                        X = f.X,
                        Y = f.Y,
                        DiameterMm = f.DiameterMm,
                        DepthMm = f.DepthMm,
                        PanelBounds = bounds,
                    });
                }
                else if (enableGroove && f.Kind.Contains("groove", StringComparison.OrdinalIgnoreCase)
                         && f.Path is { Count: >= 2 } path)
                {
                    ops.Add(new CutOp
                    {
                        Op = "groove",
                        PanelId = panel.PanelId,
                        FeatureId = f.FeatureId,
                        DepthMm = f.DepthMm,
                        Path = path.Select(p => (p.X, p.Y)).ToList(),
                        PanelBounds = bounds,
                    });
                }
                else if (enableContour && (f.Kind.Contains("cutout", StringComparison.OrdinalIgnoreCase)
                         || f.Kind.Contains("pocket", StringComparison.OrdinalIgnoreCase))
                         && f.Path is { Count: >= 3 } cutPath)
                {
                    // throughCutout / pocket → inner contour pass
                    ops.Add(new CutOp
                    {
                        Op = "contour",
                        PanelId = panel.PanelId,
                        FeatureId = f.FeatureId,
                        DepthMm = f.DepthMm,
                        Path = cutPath.Select(p => (p.X, p.Y)).ToList(),
                        PanelBounds = bounds,
                    });
                }
            }
        }

        return ops
            .OrderBy(o => o.PanelId, StringComparer.Ordinal)
            .ThenBy(o => Rank.GetValueOrDefault(o.Op, 9))
            .ToList();
    }

    public static IReadOnlyList<CutOp> AttachToNest(IEnumerable<CutOp> ops, IEnumerable<Nesting.NestPlacement> placements)
    {
        var byId = placements.ToDictionary(p => p.PanelId, p => p);
        var opList = ops.ToList();
        var boundsByPanel = opList
            .Where(o => o.Op == "contour" && o.FeatureId is null && o.Path is { Count: >= 3 })
            .GroupBy(o => o.PanelId)
            .ToDictionary(
                g => g.Key,
                g => (Nesting.LocalBounds?)Nesting.NestTransform.BoundsOf(g.First().Path!));
        return opList.Select(op =>
        {
            if (!byId.TryGetValue(op.PanelId, out var place))
                return op with { Placed = false };

            var bounds = op.PanelBounds
                ?? boundsByPanel.GetValueOrDefault(op.PanelId)
                ?? (op.Path is { Count: > 0 } sourcePath
                    ? Nesting.NestTransform.BoundsOf(sourcePath)
                    : default);
            double? sheetX = null, sheetY = null;
            IReadOnlyList<(double X, double Y)>? path = op.Path;
            if (op.Op == "drill" && op.X is double x && op.Y is double y)
            {
                var (sx, sy) = Nesting.NestTransform.ToSheet(
                    x, y, bounds, place.OffsetX, place.OffsetY, place.RotationDeg);
                sheetX = Math.Round(sx, 3);
                sheetY = Math.Round(sy, 3);
            }
            else if (op.Path is { Count: > 0 })
            {
                path = op.Path.Select(p =>
                {
                    var (sx, sy) = Nesting.NestTransform.ToSheet(
                        p.X, p.Y, bounds, place.OffsetX, place.OffsetY, place.RotationDeg);
                    return (Math.Round(sx, 3), Math.Round(sy, 3));
                }).ToList();
            }

            return op with
            {
                Placed = true,
                SheetIndex = place.SheetIndex,
                OffsetX = place.OffsetX,
                OffsetY = place.OffsetY,
                RotationDeg = place.RotationDeg,
                SheetX = sheetX,
                SheetY = sheetY,
                Path = path,
            };
        }).ToList();
    }

}
