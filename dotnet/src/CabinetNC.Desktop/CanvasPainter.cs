using CabinetNC.Compute.Contracts;
using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Manufacturing;
using CabinetNC.Domain.Nesting;
using CabinetNC.Domain.Parts;
using SkiaSharp;

namespace CabinetNC.Desktop;

/// <summary>Vite-parity Skia painter — geom dims/handles · nest grid/selection/ops overlay.</summary>
static class CanvasPainter
{
    public static void PaintGeom(SKCanvas canvas, int w, int h, Panel? panel, string? hoverHint)
    {
        canvas.Clear(new SKColor(0xF4, 0xF4, 0xF4));
        if (panel?.Outline.Points is not { Count: >= 2 })
        {
            DrawCentered(canvas, w, h, "选择左侧板件编辑几何");
            return;
        }

        var view = GeomInteraction.BuildView(panel, w, h);
        DrawGeomGrid(canvas, view);
        DrawOutline(canvas, view, panel, fill: new SKColor(0xE8, 0xF0, 0xFF), stroke: new SKColor(0x00, 0x66, 0xCC), 2f);

        var box = PanelEdit.BBox(panel);
        DrawDimH(canvas, view, box.MinX, box.MaxX, box.MinY, Fmt(box.W), outward: -1);
        DrawDimV(canvas, view, box.MinY, box.MaxY, box.MaxX, Fmt(box.H), outward: 1);

        foreach (var f in panel.Features)
        {
            if (PanelEdit.IsHole(f))
            {
                var (cx, cy) = GeomInteraction.ToScreen(view, f.X, f.Y);
                var r = Math.Max(3f, (float)((f.DiameterMm ?? 8) / 2.0) * view.Scale);
                using var hole = new SKPaint { Color = new SKColor(0x00, 0x00, 0xCC), IsStroke = true, StrokeWidth = 2, IsAntialias = true };
                canvas.DrawCircle(cx, cy, r, hole);
                DrawHandle(canvas, cx, cy, new SKColor(0x00, 0x00, 0xCC));
                DrawText(canvas, $"⌀{Fmt(f.DiameterMm ?? 0)}", cx + r + 4, cy + 4, 11, new SKColor(0x00, 0x00, 0xCC));
            }
            else if (PanelEdit.IsGroove(f) && f.Path is { Count: >= 2 } gp)
            {
                using var path = new SKPath();
                for (var i = 0; i < gp.Count; i++)
                {
                    var (sx, sy) = GeomInteraction.ToScreen(view, gp[i].X, gp[i].Y);
                    if (i == 0) path.MoveTo(sx, sy); else path.LineTo(sx, sy);
                }
                var lw = Math.Max(2f, (float)((f.WidthMm ?? 6) * view.Scale * 0.3));
                using var groove = new SKPaint { Color = new SKColor(0xCC, 0x00, 0x00), IsStroke = true, StrokeWidth = lw, IsAntialias = true };
                canvas.DrawPath(path, groove);
                foreach (var pt in gp)
                {
                    var (sx, sy) = GeomInteraction.ToScreen(view, pt.X, pt.Y);
                    DrawHandle(canvas, sx, sy, new SKColor(0xCC, 0x00, 0x00));
                }
            }
        }

        if (PanelEdit.IsAxisAlignedRect(panel))
        {
            foreach (var (hx, hy) in new (double, double)[]
                     {
                         (box.MaxX, (box.MinY + box.MaxY) / 2),
                         (box.MinX, (box.MinY + box.MaxY) / 2),
                         ((box.MinX + box.MaxX) / 2, box.MaxY),
                         ((box.MinX + box.MaxX) / 2, box.MinY),
                     })
            {
                var (sx, sy) = GeomInteraction.ToScreen(view, hx, hy);
                DrawSquareHandle(canvas, sx, sy, new SKColor(0x33, 0x33, 0x33));
            }
        }

        DrawText(canvas, panel.PanelId, 10, 18, 13, new SKColor(0x22, 0x22, 0x22), bold: true);
        DrawText(canvas, hoverHint ?? "Geom · 拖孔/槽端点/边手柄", 10, 36, 11, new SKColor(0x55, 0x55, 0x55));
    }

    public readonly record struct NestPaintOpts(
        float SheetW,
        float SheetH,
        float Pad,
        float Scale,
        string? SelectedId,
        IReadOnlySet<string> Locked,
        IReadOnlySet<string> Conflicts,
        IReadOnlyList<CutOp>? OpsOverlay,
        bool ShowOps,
        CamFrame? ActiveCamFrame);

    public static void PaintNest(
        SKCanvas canvas,
        int w,
        int h,
        IReadOnlyList<Panel> panels,
        IReadOnlyList<NestPlacementMsg> placements,
        NestPaintOpts opts)
    {
        canvas.Clear(new SKColor(0xF4, 0xF4, 0xF4));
        var pad = opts.Pad;
        var scale = opts.Scale;
        var sw = opts.SheetW;
        var sh = opts.SheetH;
        if (scale <= 0) return;

        float ToSx(double x) => pad + (float)x * scale;
        float ToSy(double y) => pad + (sh - (float)y) * scale;

        // sheet
        using (var fill = new SKPaint { Color = SKColors.White, IsAntialias = true })
        using (var stroke = new SKPaint { Color = SKColors.Black, IsStroke = true, StrokeWidth = 1, IsAntialias = true })
        {
            canvas.DrawRect(pad, pad, sw * scale, sh * scale, fill);
            DrawSheetGrid(canvas, pad, scale, sw, sh);
            canvas.DrawRect(pad, pad, sw * scale, sh * scale, stroke);
        }

        DrawDimHScreen(canvas, ToSx(0), ToSx(sw), ToSy(sh) - 14, Fmt(sw));
        DrawDimVScreen(canvas, ToSy(0), ToSy(sh), ToSx(0) - 8, Fmt(sh));

        var byId = panels.ToDictionary(p => p.PanelId);
        var drawList = placements.Where(p => p.SheetIndex == 0).ToList();
        if (opts.SelectedId is not null)
            drawList = drawList.OrderBy(p => p.PanelId == opts.SelectedId ? 1 : 0).ToList();

        foreach (var place in drawList)
        {
            if (!byId.TryGetValue(place.PanelId, out var panel)) continue;
            var active = opts.SelectedId == place.PanelId;
            var conflict = opts.Conflicts.Contains(place.PanelId);
            var locked = opts.Locked.Contains(place.PanelId);
            var bounds = NestTransform.BoundsOf(panel);

            SKColor fillC = conflict
                ? (active ? new SKColor(0xF5, 0xC6, 0xC6) : new SKColor(0xF0, 0xD0, 0xD0))
                : active ? new SKColor(0xCC, 0xDD, 0xEE) : new SKColor(0xEE, 0xEE, 0xEE);
            SKColor strokeC = conflict ? new SKColor(0xCC, 0x00, 0x00)
                : locked ? new SKColor(0xC0, 0x45, 0x2D)
                : active ? new SKColor(0x00, 0x66, 0xCC) : SKColors.Black;
            var lw = active || conflict || locked ? 2f : 1f;

            using var path = BuildWorldPath(panel, place, pad, scale, sh);
            using var fill = new SKPaint { Color = fillC, IsAntialias = true };
            using var stroke = new SKPaint { Color = strokeC, IsStroke = true, StrokeWidth = lw, IsAntialias = true };
            canvas.DrawPath(path, fill);
            canvas.DrawPath(path, stroke);

            // features on nest
            foreach (var f in panel.Features)
            {
                if (PanelEdit.IsHole(f))
                {
                    var (wx, wy) = NestTransform.ToSheet(
                        f.X, f.Y, bounds,
                        place.OffsetX, place.OffsetY, place.RotationDeg);
                    var cx = ToSx(wx);
                    var cy = ToSy(wy);
                    var r = Math.Max(1.5f, (float)((f.DiameterMm ?? 8) / 2) * scale);
                    using var hp = new SKPaint { Color = new SKColor(0x00, 0x00, 0xCC), IsStroke = true, StrokeWidth = 1, IsAntialias = true };
                    canvas.DrawCircle(cx, cy, r, hp);
                    if (active)
                        DrawText(canvas, $"⌀{Fmt(f.DiameterMm ?? 0)}", cx + r + 2, cy, 10, new SKColor(0x00, 0x00, 0xCC));
                }
                else if (PanelEdit.IsGroove(f) && f.Path is { Count: >= 2 } gp)
                {
                    using var gpath = new SKPath();
                    for (var i = 0; i < gp.Count; i++)
                    {
                        var (wx, wy) = NestTransform.ToSheet(
                            gp[i].X, gp[i].Y, bounds,
                            place.OffsetX, place.OffsetY, place.RotationDeg);
                        var sx = ToSx(wx);
                        var sy = ToSy(wy);
                        if (i == 0) gpath.MoveTo(sx, sy); else gpath.LineTo(sx, sy);
                    }
                    using var gpaint = new SKPaint
                    {
                        Color = new SKColor(0xCC, 0x00, 0x00),
                        IsStroke = true,
                        StrokeWidth = Math.Max(1f, (float)((f.WidthMm ?? 6) * scale * 0.3)),
                        IsAntialias = true,
                    };
                    canvas.DrawPath(gpath, gpaint);
                }
            }

            // label
            var aabb = NestDrag.Aabb(panel, place.OffsetX, place.OffsetY, place.RotationDeg);
            var lx = ToSx(aabb.MinX) + 2;
            var ly = ToSy(aabb.MaxY) + 12;
            var label = locked ? $"[锁] {place.PanelId}" : place.PanelId;
            DrawText(canvas, label, lx, ly, active ? 12 : 11, active ? new SKColor(0x00, 0x66, 0xCC) : SKColors.Black, bold: active);

            if (active)
            {
                DrawDimHScreen(canvas, ToSx(aabb.MinX), ToSx(aabb.MaxX), ToSy(aabb.MinY) + 12, Fmt(aabb.MaxX - aabb.MinX));
                DrawDimVScreen(canvas, ToSy(aabb.MinY), ToSy(aabb.MaxY), ToSx(aabb.MaxX) + 10, Fmt(aabb.MaxY - aabb.MinY));
            }
        }

        if (drawList.Count == 0)
            DrawText(canvas, "无摆位 — 点「应用并重排」或进入排版自动排", pad + 8, pad + 20, 12, new SKColor(0x66, 0x66, 0x66));

        if (opts.ShowOps && opts.OpsOverlay is { Count: > 0 } ops)
            PaintOpsOverlay(canvas, ops, ToSx, ToSy, scale, opts.ActiveCamFrame);
    }

    static void PaintOpsOverlay(
        SKCanvas canvas,
        IReadOnlyList<CutOp> ops,
        Func<double, float> toSx,
        Func<double, float> toSy,
        float scale,
        CamFrame? activeFrame)
    {
        foreach (var op in ops.Where(o => o.Placed))
        {
            var active = activeFrame?.Op == op;
            if (op.Op == "contour" && op.Path is { Count: >= 2 } path)
            {
                using var sk = new SKPath();
                for (var i = 0; i < path.Count; i++)
                {
                    var sx = toSx(path[i].X);
                    var sy = toSy(path[i].Y);
                    if (i == 0) sk.MoveTo(sx, sy); else sk.LineTo(sx, sy);
                }
                sk.Close();
                using var paint = new SKPaint
                {
                    Color = active ? new SKColor(0xFF, 0xC1, 0x07) : new SKColor(0x0A, 0xA4, 0x44),
                    IsStroke = true,
                    StrokeWidth = active ? 4f : 2f,
                    IsAntialias = true,
                    PathEffect = SKPathEffect.CreateDash([6, 4], 0),
                };
                canvas.DrawPath(sk, paint);
            }
            else if (op.Op == "drill" && op.SheetX is double sx0 && op.SheetY is double sy0)
            {
                var cx = toSx(sx0);
                var cy = toSy(sy0);
                var r = Math.Max(2f, (float)((op.DiameterMm ?? 8) / 2) * scale);
                using var paint = new SKPaint
                {
                    Color = active ? new SKColor(0xFF, 0xC1, 0x07) : new SKColor(0x00, 0x44, 0xFF),
                    IsStroke = true,
                    StrokeWidth = active ? 4 : 2,
                    IsAntialias = true,
                };
                canvas.DrawCircle(cx, cy, r, paint);
                canvas.DrawLine(cx - r, cy, cx + r, cy, paint);
                canvas.DrawLine(cx, cy - r, cx, cy + r, paint);
            }
            else if (op.Op == "groove" && op.Path is { Count: >= 2 } gpath)
            {
                using var sk = new SKPath();
                for (var i = 0; i < gpath.Count; i++)
                {
                    var sx = toSx(gpath[i].X);
                    var sy = toSy(gpath[i].Y);
                    if (i == 0) sk.MoveTo(sx, sy); else sk.LineTo(sx, sy);
                }
                using var paint = new SKPaint
                {
                    Color = active ? new SKColor(0xFF, 0xC1, 0x07) : new SKColor(0xEE, 0x66, 0x00),
                    IsStroke = true,
                    StrokeWidth = active ? 4f : 2.5f,
                    IsAntialias = true,
                };
                canvas.DrawPath(sk, paint);
            }
        }

        if (activeFrame is not null)
        {
            var cx = toSx(activeFrame.X);
            var cy = toSy(activeFrame.Y);
            using var marker = new SKPaint
            {
                Color = new SKColor(0xFF, 0xC1, 0x07),
                IsAntialias = true,
            };
            using var ring = new SKPaint
            {
                Color = SKColors.Black,
                IsStroke = true,
                StrokeWidth = 1.5f,
                IsAntialias = true,
            };
            canvas.DrawCircle(cx, cy, 5, marker);
            canvas.DrawCircle(cx, cy, 5, ring);
        }
    }

    public static SKPath BuildWorldPath(Panel panel, NestPlacementMsg place, float pad, float scale, float sheetH)
    {
        var path = new SKPath();
        var first = true;
        var bounds = NestTransform.BoundsOf(panel);
        foreach (var pt in panel.Outline.Points)
        {
            var (wx, wy) = NestTransform.ToSheet(
                pt.X, pt.Y, bounds,
                place.OffsetX, place.OffsetY, place.RotationDeg);
            var x = pad + (float)wx * scale;
            var y = pad + (sheetH - (float)wy) * scale;
            if (first) { path.MoveTo(x, y); first = false; }
            else path.LineTo(x, y);
        }
        path.Close();
        return path;
    }

    static void DrawGeomGrid(SKCanvas canvas, GeomInteraction.View view)
    {
        using var paint = new SKPaint { Color = new SKColor(0xE0, 0xE0, 0xE0), IsStroke = true, StrokeWidth = 1 };
        using var major = new SKPaint { Color = new SKColor(0xCC, 0xCC, 0xCC), IsStroke = true, StrokeWidth = 1 };
        const double step = 50;
        var x0 = Math.Floor(view.OriginX / step) * step;
        var y0 = Math.Floor(view.OriginY / step) * step;
        for (var x = x0; x <= view.OriginX + view.WorldW + step; x += step)
        {
            var (sx0, sy0) = GeomInteraction.ToScreen(view, x, view.OriginY);
            var (_, sy1) = GeomInteraction.ToScreen(view, x, view.OriginY + view.WorldH);
            canvas.DrawLine(sx0, sy0, sx0, sy1, Math.Abs(x % (step * 2)) < 0.01 ? major : paint);
        }
        for (var y = y0; y <= view.OriginY + view.WorldH + step; y += step)
        {
            var (sx0, sy0) = GeomInteraction.ToScreen(view, view.OriginX, y);
            var (sx1, _) = GeomInteraction.ToScreen(view, view.OriginX + view.WorldW, y);
            canvas.DrawLine(sx0, sy0, sx1, sy0, Math.Abs(y % (step * 2)) < 0.01 ? major : paint);
        }
    }

    static void DrawSheetGrid(SKCanvas canvas, float pad, float scale, float sw, float sh)
    {
        using var paint = new SKPaint { Color = new SKColor(0xE8, 0xE8, 0xE8), IsStroke = true, StrokeWidth = 1 };
        const float step = 50;
        for (float x = 0; x <= sw; x += step)
            canvas.DrawLine(pad + x * scale, pad, pad + x * scale, pad + sh * scale, paint);
        for (float y = 0; y <= sh; y += step)
            canvas.DrawLine(pad, pad + (sh - y) * scale, pad + sw * scale, pad + (sh - y) * scale, paint);
    }

    static void DrawOutline(SKCanvas canvas, GeomInteraction.View view, Panel panel, SKColor fill, SKColor stroke, float lw)
    {
        using var path = new SKPath();
        var pts = panel.Outline.Points;
        for (var i = 0; i < pts.Count; i++)
        {
            var (sx, sy) = GeomInteraction.ToScreen(view, pts[i].X, pts[i].Y);
            if (i == 0) path.MoveTo(sx, sy); else path.LineTo(sx, sy);
        }
        path.Close();
        using var f = new SKPaint { Color = fill, IsAntialias = true };
        using var s = new SKPaint { Color = stroke, IsStroke = true, StrokeWidth = lw, IsAntialias = true };
        canvas.DrawPath(path, f);
        canvas.DrawPath(path, s);
    }

    static void DrawHandle(SKCanvas canvas, float x, float y, SKColor color)
    {
        using var fill = new SKPaint { Color = color, IsAntialias = true };
        using var ring = new SKPaint { Color = SKColors.White, IsStroke = true, StrokeWidth = 1.5f, IsAntialias = true };
        canvas.DrawCircle(x, y, 5, fill);
        canvas.DrawCircle(x, y, 5, ring);
    }

    static void DrawSquareHandle(SKCanvas canvas, float x, float y, SKColor color)
    {
        using var fill = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawRect(x - 5, y - 5, 10, 10, fill);
        using var ring = new SKPaint { Color = SKColors.White, IsStroke = true, StrokeWidth = 1, IsAntialias = true };
        canvas.DrawRect(x - 5, y - 5, 10, 10, ring);
    }

    static void DrawDimH(SKCanvas canvas, GeomInteraction.View view, double x0, double x1, double y, string label, int outward)
    {
        var (a0, ay) = GeomInteraction.ToScreen(view, x0, y);
        var (a1, _) = GeomInteraction.ToScreen(view, x1, y);
        DrawDimHScreen(canvas, a0, a1, ay + outward * 14, label);
    }

    static void DrawDimV(SKCanvas canvas, GeomInteraction.View view, double y0, double y1, double x, string label, int outward)
    {
        var (ax, b0) = GeomInteraction.ToScreen(view, x, y0);
        var (_, b1) = GeomInteraction.ToScreen(view, x, y1);
        DrawDimVScreen(canvas, b0, b1, ax + outward * 14, label);
    }

    static void DrawDimHScreen(SKCanvas canvas, float a0, float a1, float y, string label)
    {
        using var p = new SKPaint { Color = new SKColor(0x33, 0x33, 0x33), IsStroke = true, StrokeWidth = 1, IsAntialias = true };
        canvas.DrawLine(a0, y - 4, a0, y + 4, p);
        canvas.DrawLine(a1, y - 4, a1, y + 4, p);
        canvas.DrawLine(a0, y, a1, y, p);
        DrawText(canvas, label, (a0 + a1) / 2 - 12, y - 3, 11, new SKColor(0x33, 0x33, 0x33));
    }

    static void DrawDimVScreen(SKCanvas canvas, float b0, float b1, float x, string label)
    {
        using var p = new SKPaint { Color = new SKColor(0x33, 0x33, 0x33), IsStroke = true, StrokeWidth = 1, IsAntialias = true };
        canvas.DrawLine(x - 4, b0, x + 4, b0, p);
        canvas.DrawLine(x - 4, b1, x + 4, b1, p);
        canvas.DrawLine(x, b0, x, b1, p);
        DrawText(canvas, label, x + 4, (b0 + b1) / 2, 11, new SKColor(0x33, 0x33, 0x33));
    }

    static void DrawText(SKCanvas canvas, string text, float x, float y, float size, SKColor color, bool bold = false)
    {
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", bold ? SKFontStyle.Bold : SKFontStyle.Normal), size);
        canvas.DrawText(text, x, y, SKTextAlign.Left, font, paint);
    }

    static void DrawCentered(SKCanvas canvas, int w, int h, string text)
    {
        using var paint = new SKPaint { Color = new SKColor(0x88, 0x88, 0x88), IsAntialias = true };
        using var font = new SKFont(SKTypeface.Default, 14);
        canvas.DrawText(text, w / 2f, h / 2f, SKTextAlign.Center, font, paint);
    }

    static string Fmt(double v) =>
        Math.Abs(v - Math.Round(v)) < 0.05 ? Math.Round(v).ToString("0") : v.ToString("0.0");
}
