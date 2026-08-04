namespace CabinetNC.Domain.Geometry;

using CabinetNC.Domain.Parts;

/// <summary>Port of src/geom/view.js — panel-local mm ↔ screen, hit-test, drag apply.</summary>
public static class GeomInteraction
{
    const float HandlePx = 14f;

    public readonly record struct View(
        float Scale,
        float Ox,
        float Oy,
        double OriginX,
        double OriginY,
        double WorldW,
        double WorldH,
        double MinX,
        double MinY,
        double MaxX,
        double MaxY);

    public readonly record struct Hit(string Type, string? FeatureId = null, string? Edge = null, int PointIndex = -1);

    public static View BuildView(Panel panel, int cssW, int cssH)
    {
        var box = PanelEdit.BBox(panel);
        var padMm = Math.Max(Math.Max(box.W, box.H), 100) * 0.12 + 40;
        var worldW = Math.Max(box.W + padMm * 2, 100);
        var worldH = Math.Max(box.H + padMm * 2, 100);
        var originX = box.MinX - padMm;
        var originY = box.MinY - padMm;
        var scale = (float)Math.Min((cssW - 24) / worldW, (cssH - 24) / worldH);
        if (scale <= 0) scale = 1;
        var ox = (cssW - (float)(worldW * scale)) / 2f;
        var oy = (cssH - (float)(worldH * scale)) / 2f;
        return new View(scale, ox, oy, originX, originY, worldW, worldH, box.MinX, box.MinY, box.MaxX, box.MaxY);
    }

    public static (float Sx, float Sy) ToScreen(View v, double x, double y) =>
        (v.Ox + (float)((x - v.OriginX) * v.Scale),
         v.Oy + (float)((v.WorldH - (y - v.OriginY)) * v.Scale));

    public static (double Lx, double Ly) ToLocal(View v, float sx, float sy) =>
        (v.OriginX + (sx - v.Ox) / v.Scale,
         v.OriginY + v.WorldH - (sy - v.Oy) / v.Scale);

    public static Hit? HitTest(Panel panel, View view, float cssX, float cssY)
    {
        if (PanelEdit.IsAxisAlignedRect(panel))
        {
            foreach (var (id, hx, hy) in ResizeHandles(view.MinX, view.MinY, view.MaxX, view.MaxY))
            {
                var (sx, sy) = ToScreen(view, hx, hy);
                if (Near(cssX, cssY, sx, sy))
                    return new Hit("resize", Edge: id);
            }
        }

        foreach (var f in panel.Features)
        {
            if (PanelEdit.IsHole(f))
            {
                var (sx, sy) = ToScreen(view, f.X, f.Y);
                if (Near(cssX, cssY, sx, sy, Math.Max(HandlePx + 10, 16)))
                    return new Hit("hole", FeatureId: f.FeatureId);
            }
            else if (PanelEdit.IsGroove(f) && f.Path is { Count: > 0 } path)
            {
                for (var i = 0; i < path.Count; i++)
                {
                    var (sx, sy) = ToScreen(view, path[i].X, path[i].Y);
                    if (Near(cssX, cssY, sx, sy, Math.Max(HandlePx + 8, 14)))
                        return new Hit("groovePoint", FeatureId: f.FeatureId, PointIndex: i);
                }
            }
        }

        var (lx, ly) = ToLocal(view, cssX, cssY);
        if (panel.Outline.Points.Count >= 3 && PointInPoly(lx, ly, panel.Outline.Points))
            return new Hit("panel");
        return null;
    }

    public static Panel ApplyDrag(Panel panel, Hit drag, double localX, double localY)
    {
        if (drag.Type == "hole" && drag.FeatureId is not null)
            return PanelEdit.MoveHole(panel, drag.FeatureId, localX, localY);
        if (drag.Type == "groovePoint" && drag.FeatureId is not null)
            return PanelEdit.MoveGroovePoint(panel, drag.FeatureId, drag.PointIndex, localX, localY);
        if (drag.Type == "resize" && drag.Edge is not null)
        {
            var box = PanelEdit.BBox(panel);
            var minX = box.MinX;
            var minY = box.MinY;
            var maxX = box.MaxX;
            var maxY = box.MaxY;
            if (drag.Edge == "e") maxX = Math.Max(localX, minX + 10);
            if (drag.Edge == "w") minX = Math.Min(localX, maxX - 10);
            if (drag.Edge == "n") maxY = Math.Max(localY, minY + 10);
            if (drag.Edge == "s") minY = Math.Min(localY, maxY - 10);
            return PanelEdit.ResizeFromEdges(panel, minX, minY, maxX, maxY);
        }
        return panel;
    }

    static IEnumerable<(string Id, double X, double Y)> ResizeHandles(double minX, double minY, double maxX, double maxY)
    {
        var mx = (minX + maxX) / 2;
        var my = (minY + maxY) / 2;
        yield return ("e", maxX, my);
        yield return ("w", minX, my);
        yield return ("n", mx, maxY);
        yield return ("s", mx, minY);
    }

    static bool Near(float sx, float sy, float tx, float ty, float tol = HandlePx + 2)
    {
        var dx = sx - tx;
        var dy = sy - ty;
        return Math.Sqrt(dx * dx + dy * dy) <= tol;
    }

    static bool PointInPoly(double x, double y, IReadOnlyList<Point2> pts)
    {
        var inside = false;
        for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
        {
            var xi = pts[i].X;
            var yi = pts[i].Y;
            var xj = pts[j].X;
            var yj = pts[j].Y;
            var hit = yi > y != yj > y && x < (xj - xi) * (y - yi) / (yj - yi + 1e-12) + xi;
            if (hit) inside = !inside;
        }
        return inside;
    }
}
