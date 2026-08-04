namespace CabinetNC.Domain.Manufacturing;

using CabinetNC.Domain.Machines;

/// <summary>Port of src/nc.js opsToNc (G0/G1/F/S — no arcs).</summary>
public static class NcEmitter
{
    static readonly Dictionary<string, int> Rank = new()
    {
        ["contour"] = 0,
        ["drill"] = 1,
        ["groove"] = 2,
    };

    public static string OpsToNc(IEnumerable<CutOp> ops, MachineProfile profile)
    {
        var safeZ = profile.SafeZMm;
        var rpm = profile.SpindleRpm;
        var list = ops.Where(o => o.Placed).ToList();
        if (!profile.EnableContour) list = list.Where(o => o.Op != "contour").ToList();
        if (!profile.EnableDrill) list = list.Where(o => o.Op != "drill").ToList();
        if (!profile.EnableGroove) list = list.Where(o => o.Op != "groove").ToList();

        var contours = list.Where(o => o.Op == "contour" && o.Path is { Count: >= 3 }).ToList();
        var drills = list.Where(o => o.Op == "drill" && o.SheetX is not null).ToList();
        var grooves = list.Where(o => o.Op == "groove" && o.Path is { Count: >= 2 }).ToList();
        var all = SortOps(contours.Concat(drills).Concat(grooves));

        var lines = new List<string>
        {
            $"(cabinetnc-cut nc · {profile.Id} · {profile.Name} · {profile.Dialect})",
            "(wcs: sheet SW origin · X+ right · Y+ back · Z+ up · units mm)",
        };
        if (!string.IsNullOrWhiteSpace(profile.OriginNote))
            lines.Add($"(origin: {profile.OriginNote.Replace("(", "").Replace(")", "")})");
        lines.Add("G21");
        lines.Add("G90");
        if (profile.Dialect == "fanuc_like")
        {
            lines.Add("G17");
            lines.Add("G40");
            lines.Add("G49");
            lines.Add("G80");
        }
        if (rpm > 0) lines.Add($"S{Math.Round(rpm)} M3");
        lines.Add($"G0 Z{Fmt(safeZ)}");

        foreach (var group in all.GroupBy(o => o.SheetIndex).OrderBy(g => g.Key))
        {
            lines.Add($"(sheet {group.Key + 1})");
            foreach (var item in SortOps(group))
            {
                if (item.Op == "contour") EmitContour(lines, item, profile);
                else if (item.Op == "drill") EmitDrill(lines, item, profile);
                else if (item.Op == "groove") EmitGroove(lines, item, profile);
            }
        }

        if (rpm > 0) lines.Add("M5");
        var end = (profile.ProgramEnd ?? "M2").ToUpperInvariant();
        lines.Add(end == "M30" ? "M30" : "M2");
        return string.Join("\n", lines) + "\n";
    }

    public static IReadOnlyList<double> ContourPassDepths(double totalDepthMm, double stepdownMm)
    {
        var total = Math.Abs(totalDepthMm);
        var step = Math.Abs(stepdownMm);
        if (total <= 0) return [];
        if (!(step > 0) || step >= total - 1e-9) return [total];
        var depths = new List<double>();
        for (var d = step; d < total - 1e-9; d += step)
            depths.Add(Math.Round(d, 3));
        depths.Add(total);
        return depths;
    }

    static void EmitContour(List<string> lines, CutOp c, MachineProfile profile)
    {
        var path = c.Path!;
        var safeZ = profile.SafeZMm;
        var total = Math.Abs(c.DepthMm ?? profile.ContourDepthMm);
        var passes = ContourPassDepths(total, profile.ContourStepdownMm);
        var feed = profile.FeedXyMmMin;
        var feedZ = profile.FeedZMmMin;
        lines.Add($"(contour {c.PanelId}{(passes.Count > 1 ? $" passes={passes.Count}" : "")})");
        lines.Add($"G0 X{Fmt(path[0].X)} Y{Fmt(path[0].Y)}");
        for (var p = 0; p < passes.Count; p++)
        {
            var z = -passes[p];
            if (passes.Count > 1) lines.Add($"(pass {p + 1}/{passes.Count} Z{Fmt(z)})");
            lines.Add($"G1 Z{Fmt(z)} F{feedZ}");
            for (var i = 1; i < path.Count; i++)
                lines.Add($"G1 X{Fmt(path[i].X)} Y{Fmt(path[i].Y)} F{feed}");
            lines.Add($"G1 X{Fmt(path[0].X)} Y{Fmt(path[0].Y)} F{feed}");
            if (p < passes.Count - 1) lines.Add($"G0 Z{Fmt(safeZ)}");
        }
        lines.Add($"G0 Z{Fmt(safeZ)}");
    }

    static void EmitDrill(List<string> lines, CutOp d, MachineProfile profile)
    {
        var safeZ = profile.SafeZMm;
        var total = Math.Abs(d.DepthMm ?? 0);
        var peck = Math.Abs(profile.DrillPeckMm);
        var feedZ = profile.FeedZMmMin;
        lines.Add($"(drill {d.PanelId} dia={d.DiameterMm})");
        lines.Add($"G0 X{Fmt(d.SheetX)} Y{Fmt(d.SheetY)}");
        if (!(peck > 0) || peck >= total - 1e-9)
        {
            lines.Add($"G1 Z{Fmt(-total)} F{feedZ}");
            lines.Add($"G0 Z{Fmt(safeZ)}");
            return;
        }
        for (var z = peck; z < total - 1e-9; z += peck)
        {
            lines.Add($"G1 Z{Fmt(-z)} F{feedZ}");
            lines.Add($"G0 Z{Fmt(safeZ)}");
        }
        lines.Add($"G1 Z{Fmt(-total)} F{feedZ}");
        lines.Add($"G0 Z{Fmt(safeZ)}");
    }

    static void EmitGroove(List<string> lines, CutOp g, MachineProfile profile)
    {
        var path = g.Path!;
        var safeZ = profile.SafeZMm;
        var z = -Math.Abs(g.DepthMm ?? 0);
        var feed = profile.FeedXyMmMin;
        lines.Add($"(groove {g.PanelId})");
        lines.Add($"G0 X{Fmt(path[0].X)} Y{Fmt(path[0].Y)}");
        lines.Add($"G1 Z{Fmt(z)} F{profile.FeedZMmMin}");
        for (var i = 1; i < path.Count; i++)
            lines.Add($"G1 X{Fmt(path[i].X)} Y{Fmt(path[i].Y)} F{feed}");
        lines.Add($"G0 Z{Fmt(safeZ)}");
    }

    static IEnumerable<CutOp> SortOps(IEnumerable<CutOp> items) =>
        items.OrderBy(o => o.PanelId, StringComparer.Ordinal)
            .ThenBy(o => Rank.GetValueOrDefault(o.Op, 9));

    static string Fmt(double? n) => (Math.Round(n ?? 0, 3)).ToString("0.###");
}
