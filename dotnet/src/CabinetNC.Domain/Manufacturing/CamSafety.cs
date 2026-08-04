namespace CabinetNC.Domain.Manufacturing;

using CabinetNC.Domain.Parts;

/// <summary>
/// Day 8 shop-safe CAM ordering and depths.
/// Order: Drill → Pocket → Groove → Inner Contour → Outer Profile.
/// ASSUMED ThroughAllowanceMm=0.5, SpoilboardAllowMm=1.0.
/// </summary>
public static class CamSafety
{
    public const double ThroughAllowanceMm = 0.5;
    public const double SpoilboardAllowMm = 1.0;

    public static int SequenceRank(CutOp op)
    {
        return op.Op.ToLowerInvariant() switch
        {
            "drill" => 0,
            "pocket" => 1,
            "groove" => 2,
            "contour" when !string.IsNullOrWhiteSpace(op.FeatureId) => 3, // inner
            "contour" => 4, // outer
            _ => 9,
        };
    }

    public static IOrderedEnumerable<CutOp> OrderSafe(IEnumerable<CutOp> ops) =>
        ops.OrderBy(o => o.SheetIndex)
            .ThenBy(o => o.PanelId, StringComparer.Ordinal)
            .ThenBy(SequenceRank)
            .ThenBy(o => o.ToolId ?? "", StringComparer.Ordinal)
            .ThenBy(o => o.FeatureId ?? "", StringComparer.Ordinal);

    public static double OuterContourDepthMm(double thicknessMm) =>
        Math.Max(0, thicknessMm) + ThroughAllowanceMm;

    public static IReadOnlyList<CutOp> ApplyPanelDepths(
        IEnumerable<CutOp> ops,
        IReadOnlyDictionary<string, Panel> panelsById)
    {
        return ops.Select(op =>
        {
            if (!panelsById.TryGetValue(op.PanelId, out var panel))
                return op;
            var th = panel.ThicknessMm;
            if (op.Op == "contour" && string.IsNullOrWhiteSpace(op.FeatureId))
                return op with { DepthMm = OuterContourDepthMm(th) };
            if (op.Op == "contour" && op.DepthMm is null or <= 0)
                return op with { DepthMm = th }; // inner without depth → panel thickness
            if (op.Op == "groove" && op.DepthMm is double gd && gd > th)
                return op with { DepthMm = th }; // clamp illegal groove (preflight still flags)
            if (op.Op == "drill" && op.DepthMm is null or <= 0)
                return op with { DepthMm = th };
            return op;
        }).ToList();
    }

    public static IReadOnlyList<PreflightIssue> DepthIssues(
        IEnumerable<CutOp> ops,
        IReadOnlyDictionary<string, Panel> panelsById)
    {
        var issues = new List<PreflightIssue>();
        foreach (var op in ops.Where(o => o.Placed && o.Enabled))
        {
            if (!panelsById.TryGetValue(op.PanelId, out var panel)) continue;
            var th = panel.ThicknessMm;
            var depth = op.DepthMm ?? 0;
            if (depth <= 0) continue;
            var max = th + SpoilboardAllowMm;
            if (op.Op == "contour" && string.IsNullOrWhiteSpace(op.FeatureId))
                max = OuterContourDepthMm(th) + 1e-6; // outer may equal thickness+allowance
            if (depth > th + SpoilboardAllowMm + 1e-6)
            {
                issues.Add(new("error", "depth_spoilboard",
                    $"{op.Op}/{op.PanelId}: 切深 {depth:0.###} > 板厚 {th:0.###}+spoil {SpoilboardAllowMm}"));
            }
            if (op.Op == "groove" && depth > th + 1e-6)
            {
                issues.Add(new("error", "groove_too_deep",
                    $"groove/{op.PanelId}: 槽深 {depth:0.###} > 板厚 {th:0.###}"));
            }
        }
        return issues;
    }
}
