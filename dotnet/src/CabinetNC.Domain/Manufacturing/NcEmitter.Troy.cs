namespace CabinetNC.Domain.Manufacturing;

using System.Globalization;
using System.Text;
using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Machines;

public static partial class NcEmitter
{
    /// <summary>OSAI-Troy.con: N-words, (UAO,1), M6 T, G79, 4-dec XYZ, F 1-dec, M30.</summary>
    static string EmitTroy(
        List<CutOp> list,
        MachineProfile profile,
        PostRecipe recipe,
        IReadOnlyDictionary<string, ToolDefinition> catalog)
    {
        _ = catalog;
        _ = profile;
        var contours = list.Where(o => o.Op == "contour" && o.Path is { Count: >= 3 }).ToList();
        var pockets = list.Where(o => o.Op == "pocket" && (
            o.PathSegments is { Count: > 0 } || o.Path is { Count: >= 2 })).ToList();
        var drills = list.Where(o => o.Op == "drill" && o.SheetX is not null).ToList();
        var grooves = list.Where(o => o.Op == "groove" && o.Path is { Count: >= 2 }).ToList();
        var all = CamSafety.OrderSafe(contours.Concat(pockets).Concat(drills).Concat(grooves)).ToList();

        var w = new OsaiTroyWriter(recipe.HomeXyAtEnd);
        var rpm = recipe.ProfileFirstRpm > 0 ? recipe.ProfileFirstRpm : TroyRecipe.SpindleRpm;
        var firstTool = FirstToolNum(all);
        w.StartProgram(firstTool, rpm);

        foreach (var group in all.GroupBy(o => o.SheetIndex).OrderBy(g => g.Key))
        {
            var ordered = CamSafety.OrderSafe(group).ToList();

            foreach (var d in ordered.Where(o => o.Op == "drill"))
            {
                w.ToolChange(ToolNum(d.ToolId), recipe.DrillRpm > 0 ? recipe.DrillRpm : rpm);
                EmitTroyDrill(w, d, recipe);
            }

            foreach (var g in ordered.Where(o => o.Op == "groove" && o.IsTongue))
            {
                w.ToolChange(ToolNum(g.ToolId), recipe.TongueRpm > 0 ? recipe.TongueRpm : rpm);
                EmitTroyGroove(w, g, recipe, recipe.TongueFeed, recipe.TonguePlunge);
            }

            foreach (var c in ordered.Where(o => o.Op == "pocket"
                         || (o.Op == "groove" && !o.IsTongue)))
            {
                w.ToolChange(ToolNum(c.ToolId), recipe.ClearanceRpm > 0 ? recipe.ClearanceRpm : rpm);
                if (c.Op == "pocket")
                    EmitTroyPocket(w, c, recipe);
                else
                    EmitTroyGroove(w, c, recipe, recipe.ClearanceFeed, recipe.ClearancePlunge);
            }

            var profileOps = ordered.Where(o => o.Op == "contour").ToList();
            foreach (var c in profileOps)
            {
                w.ToolChange(ToolNum(c.ToolId), recipe.ProfileFirstRpm > 0 ? recipe.ProfileFirstRpm : rpm);
                EmitTroyProfile(w, c, recipe, lastPass: false);
            }
            foreach (var c in profileOps)
            {
                w.ToolChange(ToolNum(c.ToolId), recipe.ProfileLastRpm > 0 ? recipe.ProfileLastRpm : rpm);
                EmitTroyProfile(w, c, recipe, lastPass: true);
            }
        }

        w.EndProgram();
        return w.Text();
    }

    static int FirstToolNum(IReadOnlyList<CutOp> ops)
    {
        foreach (var o in ops.Where(o => o.Op == "drill"))
            return ToolNum(o.ToolId);
        foreach (var o in ops.Where(o => o.Op == "groove" && o.IsTongue))
            return ToolNum(o.ToolId);
        foreach (var o in ops.Where(o => o.Op == "pocket" || (o.Op == "groove" && !o.IsTongue)))
            return ToolNum(o.ToolId);
        foreach (var o in ops.Where(o => o.Op == "contour"))
            return ToolNum(o.ToolId);
        return 2;
    }

    static int ToolNum(string? toolId)
    {
        if (string.IsNullOrWhiteSpace(toolId)) return 2;
        var s = toolId.Trim();
        if (s.Length >= 2 && (s[0] is 'T' or 't') && int.TryParse(s.AsSpan(1), out var n) && n > 0)
            return n;
        return int.TryParse(s, out var raw) && raw > 0 ? raw : 2;
    }

    static void EmitTroyDrill(OsaiTroyWriter w, CutOp d, PostRecipe recipe)
    {
        var z = DrillWorkZ(d, recipe);
        w.Rapid(d.SheetX, d.SheetY, recipe.SafeZMm);
        w.Feed(null, null, z, recipe.DrillPlunge);
        w.Rapid(null, null, recipe.SafeZMm);
    }

    static void EmitTroyGroove(
        OsaiTroyWriter w, CutOp g, PostRecipe recipe, double feed, double feedZ)
    {
        var path = g.Path!;
        var z = FeatureWorkZ(g, recipe);
        w.Rapid(path[0].X, path[0].Y, recipe.SafeZMm);
        w.Feed(null, null, z, feedZ);
        EmitFittedXy(w, path, feed, closed: false);
        w.Rapid(null, null, recipe.SafeZMm);
    }

    static void EmitTroyPocket(OsaiTroyWriter w, CutOp c, PostRecipe recipe)
    {
        if (c.DepthMm is null or <= 0 || c.PocketTooSmallForTool)
            return;
        var z = FeatureWorkZ(c, recipe);
        var feed = recipe.ClearanceFeed;
        var feedZ = recipe.ClearancePlunge;
        var segments = c.PathSegments;
        if (segments is null || segments.Count == 0)
        {
            if (c.Path is not { Count: >= 2 }) return;
            w.Rapid(c.Path[0].X, c.Path[0].Y, recipe.SafeZMm);
            w.Feed(null, null, z, feedZ);
            EmitFittedXy(w, c.Path, feed, c.ClosePath);
            w.Rapid(null, null, recipe.SafeZMm);
            return;
        }

        foreach (var seg in segments)
        {
            if (seg.Count < 2) continue;
            w.Rapid(null, null, recipe.SafeZMm);
            w.Rapid(seg[0].X, seg[0].Y, recipe.SafeZMm);
            w.Feed(null, null, z, feedZ);
            EmitFittedXy(w, seg, feed, closed: false);
        }
        if (c.FinishLoop is { Count: >= 3 } finish)
        {
            w.Rapid(null, null, recipe.SafeZMm);
            w.Rapid(finish[0].X, finish[0].Y, recipe.SafeZMm);
            w.Feed(null, null, z, feedZ);
            EmitFittedXy(w, finish, feed, closed: true);
        }
        w.Rapid(null, null, recipe.SafeZMm);
    }

    static void EmitTroyProfile(OsaiTroyWriter w, CutOp c, PostRecipe recipe, bool lastPass)
    {
        var path = c.Path!;
        var safeZ = recipe.SafeZMm;
        var cutZ = lastPass ? recipe.ProfileThroughZMm : recipe.ProfileFirstLeaveMm;
        var feed = lastPass ? recipe.ProfileLastFeed : recipe.ProfileFirstFeed;
        var feedZ = lastPass ? recipe.ProfileLastPlunge : recipe.ProfileFirstPlunge;
        w.Rapid(path[0].X, path[0].Y, safeZ);

        var startArc = 0d;
        if (!lastPass && recipe.ProfileFirstRamp45)
            startArc = EmitRamp45(w, path, c.ClosePath, safeZ, cutZ, feedZ);
        else
            w.Feed(null, null, cutZ, feedZ);

        IEnumerable<ProfileBridge> bridges = [];
        if (lastPass)
        {
            bridges = recipe.Bridges.Where(b =>
                b.SheetIndex == c.SheetIndex
                && string.Equals(b.PanelId, c.PanelId, StringComparison.Ordinal)
                && string.Equals(b.FeatureId ?? "", c.FeatureId ?? "", StringComparison.Ordinal));
        }

        EmitPathFromArc(w, path, c.ClosePath, startArc, cutZ, safeZ, feed, feedZ, bridges);
        w.Rapid(null, null, safeZ);
    }

    static double EmitRamp45(
        OsaiTroyWriter w,
        IReadOnlyList<(double X, double Y)> path,
        bool closed,
        double safeZ,
        double cutZ,
        double feedZ)
    {
        var dz = Math.Abs(safeZ - cutZ);
        var total = PolylineQuery.Length(path, closed);
        if (dz < 1e-6 || total < 1e-6)
        {
            w.Feed(null, null, cutZ, feedZ);
            return 0;
        }
        var along = Math.Min(dz, total);
        var pt = PolylineQuery.PointAtArc(path, along, closed);
        if (pt is null)
        {
            w.Feed(null, null, cutZ, feedZ);
            return 0;
        }
        w.Feed(pt.Value.X, pt.Value.Y, cutZ, feedZ);
        return along;
    }

    static void EmitPathFromArc(
        OsaiTroyWriter w,
        IReadOnlyList<(double X, double Y)> path,
        bool closed,
        double startArc,
        double cutZ,
        double safeZ,
        double feed,
        double feedZ,
        IEnumerable<ProfileBridge> bridges)
    {
        var total = PolylineQuery.Length(path, closed);
        if (total < 1e-9) return;
        var gaps = MergeGaps(BridgeGaps(bridges, total, closed), total);
        var arcs = WalkSampleArcs(path, closed, startArc, total, gaps);
        if (arcs.Count == 0) return;

        var run = new List<(double X, double Y)>();
        void FlushCut()
        {
            if (run.Count < 2)
            {
                run.Clear();
                return;
            }
            var loop = run.Count >= 4
                && Math.Sqrt(
                    (run[0].X - run[^1].X) * (run[0].X - run[^1].X)
                    + (run[0].Y - run[^1].Y) * (run[0].Y - run[^1].Y)) < 0.05;
            EmitFittedXy(w, run, feed, closed: loop);
            run.Clear();
        }

        (double X, double Y)? PtAt(double a)
        {
            var queryArc = a >= total - 1e-9 && a <= total + 1e-9 ? 0 : a;
            return PolylineQuery.PointAtArc(path, queryArc, closed);
        }

        var startPt = PtAt(arcs[0]);
        if (startPt is not null)
            run.Add(startPt.Value);

        var cutting = true;
        for (var i = 1; i < arcs.Count; i++)
        {
            var a0 = arcs[i - 1];
            var a1 = arcs[i];
            var span = a1 >= a0 - 1e-9 ? a1 - a0 : (total - a0) + a1;
            if (span < 1e-6) continue;
            var mid = MidArc(a0, a1, total);
            var inGap = gaps.Any(g => mid >= g.A - 1e-9 && mid <= g.B + 1e-9);
            var pt = PtAt(a1);
            if (pt is null) continue;
            if (inGap)
            {
                if (cutting)
                {
                    FlushCut();
                    w.Rapid(null, null, safeZ);
                }
                cutting = false;
                w.Rapid(pt.Value.X, pt.Value.Y, null);
            }
            else
            {
                if (!cutting)
                {
                    w.Feed(null, null, cutZ, feedZ);
                    cutting = true;
                    run.Add(pt.Value);
                }
                else
                {
                    run.Add(pt.Value);
                }
            }
        }
        FlushCut();
    }

    static void EmitFittedXy(
        OsaiTroyWriter w,
        IReadOnlyList<(double X, double Y)> path,
        double feed,
        bool closed)
    {
        foreach (var seg in PolylineArcFit.Fit(path, closed))
        {
            if (seg.Arc)
                w.Arc(seg.Cw, seg.X, seg.Y, seg.R, feed);
            else
                w.Feed(seg.X, seg.Y, null, feed);
        }
    }

    static List<double> WalkSampleArcs(
        IReadOnlyList<(double X, double Y)> path,
        bool closed,
        double startArc,
        double total,
        IReadOnlyList<(double A, double B)> gaps)
    {
        var marks = new List<double>();
        void Mark(double a)
        {
            if (a < -1e-9 || a > total + 1e-9)
                a = WrapArc(a, total, closed);
            else
                a = Math.Clamp(a, 0, total);
            if (marks.All(m => Math.Abs(m - a) > 1e-6))
                marks.Add(a);
        }
        foreach (var v in VertexArcs(path, closed))
            Mark(v);
        foreach (var g in gaps)
        {
            Mark(g.A);
            Mark(g.B);
        }
        Mark(startArc);
        if (marks.All(m => Math.Abs(m - total) > 1e-6))
            marks.Add(total);
        marks.Sort();

        var walk = new List<double>();
        void Add(double a)
        {
            if (walk.Count == 0 || Math.Abs(walk[^1] - a) > 1e-9)
                walk.Add(a);
        }

        foreach (var a in marks.Where(a => a >= startArc - 1e-9))
            Add(a);
        if (closed && startArc > 1e-6)
        {
            foreach (var a in marks.Where(a => a <= startArc + 1e-9))
                Add(a);
        }
        return walk;
    }

    static double MidArc(double a0, double a1, double total)
    {
        if (a1 >= a0 - 1e-9)
            return (a0 + a1) / 2;
        var len = (total - a0) + a1;
        var mid = a0 + len / 2;
        return mid >= total ? mid - total : mid;
    }

    static List<double> VertexArcs(IReadOnlyList<(double X, double Y)> path, bool closed)
    {
        var arcs = new List<double> { 0 };
        double walked = 0;
        var n = closed ? path.Count : path.Count - 1;
        for (var i = 0; i < n; i++)
        {
            var a = path[i];
            var b = path[(i + 1) % path.Count];
            walked += Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));
            arcs.Add(walked);
        }
        return arcs;
    }

    static List<(double A, double B)> BridgeGaps(
        IEnumerable<ProfileBridge> bridges, double total, bool closed)
    {
        var gaps = new List<(double A, double B)>();
        foreach (var b in bridges)
        {
            var half = Math.Max(0.2, b.WidthMm / 2);
            var a = b.ArcLengthMm - half;
            var c = b.ArcLengthMm + half;
            if (!closed)
            {
                gaps.Add((Math.Max(0, a), Math.Min(total, c)));
                continue;
            }
            a = WrapArc(a, total, true);
            c = WrapArc(c, total, true);
            if (c >= a)
                gaps.Add((a, c));
            else
            {
                gaps.Add((a, total));
                gaps.Add((0, c));
            }
        }
        return gaps;
    }

    static List<(double A, double B)> MergeGaps(List<(double A, double B)> gaps, double total)
    {
        var list = gaps.Where(g => g.B - g.A > 1e-4).OrderBy(g => g.A).ToList();
        if (list.Count == 0) return list;
        var merged = new List<(double A, double B)> { list[0] };
        for (var i = 1; i < list.Count; i++)
        {
            var last = merged[^1];
            var next = list[i];
            if (next.A <= last.B + 1e-4)
                merged[^1] = (last.A, Math.Max(last.B, next.B));
            else
                merged.Add(next);
        }
        return merged.Select(g => (Math.Max(0, g.A), Math.Min(total, g.B))).ToList();
    }

    static double WrapArc(double arc, double total, bool closed)
    {
        if (!closed || total < 1e-9) return Math.Clamp(arc, 0, total);
        var t = arc % total;
        if (t < 0) t += total;
        return t;
    }

    static double FeatureWorkZ(CutOp op, PostRecipe recipe)
    {
        var depth = Math.Abs(op.DepthMm ?? 0);
        if (op.Through)
            return recipe.ProfileThroughZMm;
        if (recipe.Z0IsBoardBottom && op.ThicknessMm is > 0)
            return op.ThicknessMm.Value - depth;
        return -depth;
    }

    static double DrillWorkZ(CutOp d, PostRecipe recipe)
    {
        var depth = Math.Abs(d.DepthMm ?? 0);
        var th = d.ThicknessMm ?? 0;
        var through = d.Through || (th > 0 && depth >= th - 0.05);
        if (through)
            return recipe.DrillThroughZMm;
        if (recipe.Z0IsBoardBottom && th > 0)
            return th - depth;
        return -depth;
    }

    sealed class OsaiTroyWriter
    {
        readonly List<string> _lines = [];
        readonly bool _homeXyAtEnd;
        int _n = 1;
        double? _x, _y, _z, _f;
        public int? Tool { get; private set; }

        const double Eps = 5e-5;

        public OsaiTroyWriter(bool homeXyAtEnd = true) => _homeXyAtEnd = homeXyAtEnd;

        public string Text() => string.Join("\r\n", _lines) + "\r\n";

        void Line(string body)
        {
            _lines.Add("N" + _n.ToString(CultureInfo.InvariantCulture) + " " + body);
            _n++;
        }

        public void StartProgram(int tool, double rpm)
        {
            Line("G90 ");
            Line("G40 ");
            Line("G80 ");
            Line("(UAO,1)");
            Line("G79 Z0");
            Line("M05");
            Line("M52");
            Line("M6 T" + tool.ToString(CultureInfo.InvariantCulture));
            Line("M3 S" + Math.Round(rpm).ToString("0", CultureInfo.InvariantCulture));
            Line("(DLY,3)");
            Line("M49");
            Line("G27");
            Line("G17");
            Tool = tool;
            Rapid(0, 0, null);
        }

        public void ToolChange(int tool, double rpm)
        {
            if (Tool == tool) return;
            Line("M5");
            Line("M52");
            Line("M6 T" + tool.ToString(CultureInfo.InvariantCulture));
            Line("M3 S" + Math.Round(rpm).ToString("0", CultureInfo.InvariantCulture));
            Line("(DLY,3)");
            Line("M49");
            Line("G27");
            Line("G17");
            _x = _y = _z = _f = null;
            Tool = tool;
            Rapid(0, 0, null);
        }

        public void EndProgram()
        {
            if (_homeXyAtEnd)
            {
                Line("G0 X" + Xyz(0) + " Y" + Xyz(0));
                _x = 0;
                _y = 0;
            }
            Line("G80");
            Line("M5");
            Line("G79 Z0");
            Line("M30");
        }

        static string Xyz(double v) => v.ToString("0.0000", CultureInfo.InvariantCulture);
        static string FeedFmt(double v) => v.ToString("0.0", CultureInfo.InvariantCulture);

        static bool Same(double? last, double next) =>
            last is double l && Math.Abs(l - next) < Eps;

        public void Rapid(double? x, double? y, double? z)
        {
            var sb = new StringBuilder("G0");
            if (x is double xv && !Same(_x, xv)) { sb.Append(" X"); sb.Append(Xyz(xv)); _x = xv; }
            if (y is double yv && !Same(_y, yv)) { sb.Append(" Y"); sb.Append(Xyz(yv)); _y = yv; }
            if (z is double zv && !Same(_z, zv)) { sb.Append(" Z"); sb.Append(Xyz(zv)); _z = zv; }
            if (sb.Length == 2) return;
            Line(sb.ToString());
        }

        public void Feed(double? x, double? y, double? z, double? f)
        {
            var sb = new StringBuilder("G1");
            if (x is double xv && !Same(_x, xv)) { sb.Append(" X"); sb.Append(Xyz(xv)); _x = xv; }
            if (y is double yv && !Same(_y, yv)) { sb.Append(" Y"); sb.Append(Xyz(yv)); _y = yv; }
            if (z is double zv && !Same(_z, zv)) { sb.Append(" Z"); sb.Append(Xyz(zv)); _z = zv; }
            if (f is double fv && !Same(_f, fv)) { sb.Append(" F"); sb.Append(FeedFmt(fv)); _f = fv; }
            if (sb.Length == 2) return;
            Line(sb.ToString());
        }

        public void Arc(bool cw, double x, double y, double r, double? f)
        {
            var sb = new StringBuilder(cw ? "G2" : "G3");
            sb.Append(" X"); sb.Append(Xyz(x));
            sb.Append(" Y"); sb.Append(Xyz(y));
            sb.Append(" R"); sb.Append(Xyz(r));
            if (f is double fv && !Same(_f, fv)) { sb.Append(" F"); sb.Append(FeedFmt(fv)); _f = fv; }
            _x = x;
            _y = y;
            Line(sb.ToString());
        }
    }
}
