namespace CabinetNC.Domain.Manufacturing;

using CabinetNC.Domain.Machines;

public sealed record PreflightIssue(string Level, string Code, string Message);

public sealed class PreflightReport
{
    public required bool Ok { get; init; }
    public IReadOnlyList<PreflightIssue> Issues { get; init; } = [];
}

/// <summary>Port of src/nc_preflight.js — shop gate before NC/DXF export.</summary>
public static class NcPreflight
{
    public static PreflightReport Check(
        IReadOnlyList<CutOp> ops,
        MachineProfile profile,
        double sheetWidthMm,
        double sheetLengthMm)
    {
        var issues = new List<PreflightIssue>();
        var placed = ops.Where(o => o.Placed).ToList();
        if (placed.Count == 0)
        {
            issues.Add(new("error", "no_ops", "无已排工序 — 先密排并启用轮廓/钻孔/开槽"));
            return new PreflightReport { Ok = false, Issues = issues };
        }

        if (sheetWidthMm > 0 && sheetLengthMm > 0)
        {
            var oob = 0;
            foreach (var op in placed)
            {
                foreach (var (x, y) in PointsOf(op))
                {
                    if (x < -0.5 || y < -0.5 || x > sheetWidthMm + 0.5 || y > sheetLengthMm + 0.5)
                        oob++;
                }
            }
            if (oob > 0)
                issues.Add(new("error", "out_of_sheet", $"{oob} 个刀位点超出板材 {sheetWidthMm:0.###}×{sheetLengthMm:0.###}"));
        }

        if (profile.FeedXyMmMin <= 0)
            issues.Add(new("error", "bad_feed", "XY 进给无效"));
        if (profile.SpindleRpm <= 0)
            issues.Add(new("warn", "no_spindle", "主轴转速未设置"));
        if (profile.ToolDiameterMm <= 0)
            issues.Add(new("warn", "no_tool", "刀径为 0"));

        var missingTools = ToolBinder.MissingToolIds(placed);
        if (missingTools.Count > 0)
        {
            issues.Add(new("error", "missing_tool_id",
                $"缺少刀具绑定 ToolId ×{missingTools.Count}: " + string.Join(", ", missingTools.Take(8))));
        }

        var ok = issues.All(i => i.Level != "error");
        return new PreflightReport { Ok = ok, Issues = issues };
    }

    public static string Format(PreflightReport report)
    {
        if (report.Issues.Count == 0) return "预检通过";
        return string.Join("\n", report.Issues.Select(i => (i.Level == "error" ? "✗ " : "! ") + i.Message));
    }

    static IEnumerable<(double X, double Y)> PointsOf(CutOp op)
    {
        if (op.Op == "drill" && op.SheetX is double sx && op.SheetY is double sy)
        {
            yield return (sx, sy);
            yield break;
        }
        if (op.Path is { Count: > 0 } path)
        {
            foreach (var p in path) yield return p;
        }
    }
}
