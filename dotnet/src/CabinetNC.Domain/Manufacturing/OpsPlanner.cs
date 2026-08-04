namespace CabinetNC.Domain.Manufacturing;

public sealed record CutOp
{
    public required string Op { get; init; } // contour | drill | groove | pocket
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
    public double? StepdownMm { get; init; }
    public IReadOnlyList<(double X, double Y)>? Path { get; init; }
    public Nesting.LocalBounds? PanelBounds { get; init; }
    /// <summary>Bound tool — required for export (Day 7).</summary>
    public string? ToolId { get; init; }
    /// <summary>A | B face.</summary>
    public string? Side { get; init; }
    public int SequenceGroup { get; init; }
    public bool Enabled { get; init; } = true;
}

/// <summary>Port of src/ops.js featuresToOps + attachOpsToNest (contour + drill + groove).</summary>
public static class OpsPlanner
{
    public static IReadOnlyList<CutOp> FeaturesToOps(
        IEnumerable<Parts.Panel> panels,
        bool enableContour = true,
        bool enableDrill = true,
        bool enableGroove = true)
    {
        var ops = new List<CutOp>();
        var panelList = panels.ToList();
        foreach (var panel in panelList)
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
                    DepthMm = CamSafety.OuterContourDepthMm(panel.ThicknessMm),
                    Side = panel.Side ?? panel.Orientation?.MillingFace,
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
                        DepthMm = f.DepthMm ?? panel.ThicknessMm,
                        PanelBounds = bounds,
                        Side = panel.Side ?? panel.Orientation?.MillingFace,
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
                        Side = panel.Side ?? panel.Orientation?.MillingFace,
                    });
                }
                else if (enableContour && f.Kind.Contains("pocket", StringComparison.OrdinalIgnoreCase)
                         && f.Path is { Count: >= 3 } pocketPath)
                {
                    ops.Add(new CutOp
                    {
                        Op = "pocket",
                        PanelId = panel.PanelId,
                        FeatureId = f.FeatureId,
                        DepthMm = f.DepthMm ?? panel.ThicknessMm,
                        Path = pocketPath.Select(p => (p.X, p.Y)).ToList(),
                        PanelBounds = bounds,
                        Side = panel.Side ?? panel.Orientation?.MillingFace,
                    });
                }
                else if (enableContour && f.Kind.Contains("cutout", StringComparison.OrdinalIgnoreCase)
                         && f.Path is { Count: >= 3 } cutPath)
                {
                    ops.Add(new CutOp
                    {
                        Op = "contour",
                        PanelId = panel.PanelId,
                        FeatureId = f.FeatureId,
                        DepthMm = f.DepthMm ?? CamSafety.OuterContourDepthMm(panel.ThicknessMm),
                        Path = cutPath.Select(p => (p.X, p.Y)).ToList(),
                        PanelBounds = bounds,
                        Side = panel.Side ?? panel.Orientation?.MillingFace,
                    });
                }
            }
        }

        var byId = panelList.ToDictionary(p => p.PanelId, p => p);
        var bound = ToolBinder.BindAll(ops);
        var depthApplied = CamSafety.ApplyPanelDepths(bound, byId);
        return CamSafety.OrderSafe(depthApplied).ToList();
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
