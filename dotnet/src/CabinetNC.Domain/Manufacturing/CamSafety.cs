namespace CabinetNC.Domain.Manufacturing;

using CabinetNC.Domain.Parts;

/// <summary>
/// Day 8 shop-safe CAM ordering and depths.
/// Order: Drill → Tongue → Clearance → Inner Profile → Outer Profile.
/// ASSUMED ThroughAllowanceMm=0.5, SpoilboardAllowMm=1.0.
/// </summary>
public static class CamSafety
{
    public const double ThroughAllowanceMm = 0.5;
    public const double SpoilboardAllowMm = 1.0;

    public static int SequenceRank(CutOp op)
    {
        var kind = op.Op.ToLowerInvariant();
        if (kind == "drill") return 0;
        if (kind == "groove" && op.IsTongue) return 1;
        if (kind is "pocket" or "groove") return 2;
        if (kind == "contour" && !string.IsNullOrWhiteSpace(op.FeatureId)) return 3;
        if (kind == "contour") return 4;
        if (kind == "remnant") return 5;
        return 9;
    }

    public static IOrderedEnumerable<CutOp> OrderSafe(IEnumerable<CutOp> ops) =>
        ops.OrderBy(o => o.SheetIndex)
            .ThenBy(SequenceRank)
            .ThenBy(o => o.PanelId, StringComparer.Ordinal)
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
            var stamped = op.ThicknessMm is > 0 ? op : op with { ThicknessMm = th };
            if (stamped.Op == "contour" && string.IsNullOrWhiteSpace(stamped.FeatureId))
                return stamped with { DepthMm = OuterContourDepthMm(th), Through = true };
            if (stamped.Op == "contour" && stamped.DepthMm is null or <= 0)
                return stamped with { DepthMm = th }; // inner without depth → panel thickness
            // Do NOT clamp over-deep grooves here — Preflight DepthIssues must see the raw illegal depth.
            if (stamped.Op == "drill" && stamped.DepthMm is null or <= 0)
                return stamped with { DepthMm = th };
            return stamped;
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
