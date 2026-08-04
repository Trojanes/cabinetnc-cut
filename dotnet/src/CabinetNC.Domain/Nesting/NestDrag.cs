namespace CabinetNC.Domain.Nesting;

using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Parts;

/// <summary>Port of src/render.js nest clamp / AABB resolve for drag drop.</summary>
public static class NestDrag
{
    public static double SnapMm(double v, double step = 10) =>
        Math.Round(v / step) * step;

    public static (double Ox, double Oy) ClampOnSheet(
        Panel panel,
        double ox,
        double oy,
        double rotDeg,
        double sheetW,
        double sheetH,
        double borderMm)
    {
        var (w, h) = SizeRotated(panel, rotDeg);
        var minX = borderMm;
        var minY = borderMm;
        var maxX = Math.Max(minX, sheetW - borderMm - w);
        var maxY = Math.Max(minY, sheetH - borderMm - h);
        return (Clamp(ox, minX, maxX), Clamp(oy, minY, maxY));
    }

    public static (double Ox, double Oy, bool Blocked) Resolve(
        Panel panel,
        string panelId,
        double ox,
        double oy,
        double rotDeg,
        int sheetIndex,
        IReadOnlyList<(string PanelId, int SheetIndex, double Ox, double Oy, double Rot)> others,
        IReadOnlyDictionary<string, Panel> byId,
        double sheetW,
        double sheetH,
        double spacingMm,
        double borderMm,
        (double Ox, double Oy) fallback,
        bool allowOverlap)
    {
        var clamped = ClampOnSheet(panel, ox, oy, rotDeg, sheetW, sheetH, borderMm);
        if (allowOverlap) return (clamped.Ox, clamped.Oy, false);

        var box = Aabb(panel, clamped.Ox, clamped.Oy, rotDeg);
        foreach (var op in others)
        {
            if (op.PanelId == panelId || op.SheetIndex != sheetIndex) continue;
            if (!byId.TryGetValue(op.PanelId, out var other)) continue;
            var ob = Aabb(other, op.Ox, op.Oy, op.Rot);
            if (AabbConflict(box, ob, spacingMm))
                return (fallback.Ox, fallback.Oy, true);
        }
        return (clamped.Ox, clamped.Oy, false);
    }

    public static (double MinX, double MinY, double MaxX, double MaxY) Aabb(
        Panel panel, double ox, double oy, double rotDeg)
    {
        var (w, h) = SizeRotated(panel, rotDeg);
        return (ox, oy, ox + w, oy + h);
    }

    static (double W, double H) SizeRotated(Panel panel, double rotDeg)
    {
        var pts = panel.Outline.Points;
        if (pts.Count < 2) return (0, 0);
        var w = pts.Max(p => p.X) - pts.Min(p => p.X);
        var h = pts.Max(p => p.Y) - pts.Min(p => p.Y);
        var r = ((int)Math.Round(rotDeg) % 360 + 360) % 360;
        return r is 90 or 270 ? (h, w) : (w, h);
    }

    static bool AabbConflict(
        (double MinX, double MinY, double MaxX, double MaxY) a,
        (double MinX, double MinY, double MaxX, double MaxY) b,
        double gap)
    {
        return !(a.MaxX + gap <= b.MinX || b.MaxX + gap <= a.MinX ||
                 a.MaxY + gap <= b.MinY || b.MaxY + gap <= a.MinY);
    }

    static double Clamp(double v, double lo, double hi) =>
        v < lo ? lo : v > hi ? hi : v;
}
