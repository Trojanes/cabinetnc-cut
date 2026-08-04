namespace CabinetNC.Domain.Manufacturing;

using CabinetNC.Domain.Parts;

/// <summary>
/// Dual-face registration gate (Day 11).
/// Does NOT invent a shop flip axis — without an explicit strategy, B-side export is blocked.
/// </summary>
public sealed class FaceRegistration
{
    /// <summary>none | pins | fence | vacuum_fixture | manual_mark</summary>
    public string Strategy { get; init; } = "none";
    /// <summary>Flip about X or Y in panel local — only used when Strategy != none.</summary>
    public string? FlipAxis { get; init; }
    public string? OriginNote { get; init; }
    public IReadOnlyList<(double X, double Y)> RegistrationHoles { get; init; } = [];
}

public static class DoubleSideGate
{
    public static bool AllowsBackSide(FaceRegistration? reg) =>
        reg is not null
        && !string.IsNullOrWhiteSpace(reg.Strategy)
        && !reg.Strategy.Equals("none", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<PreflightIssue> CheckBackSideOps(
        IEnumerable<CutOp> ops,
        FaceRegistration? registration)
    {
        var issues = new List<PreflightIssue>();
        var back = ops.Where(o =>
            o.Placed && o.Enabled &&
            string.Equals(o.Side, "B", StringComparison.OrdinalIgnoreCase)).ToList();
        if (back.Count == 0) return issues;
        if (!AllowsBackSide(registration))
        {
            issues.Add(new("error", "no_registration",
                $"存在 B 面工序 ×{back.Count}，但未配置定位策略（FaceRegistration.Strategy）— 禁止导出反面程序"));
        }
        return issues;
    }

    /// <summary>
    /// Conservative mirror for B-side local XY when FlipAxis is known.
    /// ASSUMPTION only for math test — production WCS must be confirmed by Troy.
    /// </summary>
    public static (double X, double Y) MirrorLocal(double x, double y, double widthMm, double heightMm, string flipAxis)
    {
        return flipAxis.Trim().ToUpperInvariant() switch
        {
            "X" => (widthMm - x, y),
            "Y" => (x, heightMm - y),
            _ => (x, y),
        };
    }
}
