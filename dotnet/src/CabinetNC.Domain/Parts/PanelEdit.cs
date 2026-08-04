namespace CabinetNC.Domain.Parts;

using CabinetNC.Domain.Geometry;

/// <summary>Port of src/geom/panel.js edit ops — returns new Panel instances.</summary>
public static class PanelEdit
{
    public static (double MinX, double MinY, double MaxX, double MaxY, double W, double H) BBox(Panel panel)
    {
        var pts = panel.Outline.Points;
        if (pts.Count == 0) return (0, 0, 0, 0, 0, 0);
        var minX = pts.Min(p => p.X);
        var minY = pts.Min(p => p.Y);
        var maxX = pts.Max(p => p.X);
        var maxY = pts.Max(p => p.Y);
        return (minX, minY, maxX, maxY, maxX - minX, maxY - minY);
    }

    public static bool IsAxisAlignedRect(Panel panel)
    {
        var pts = panel.Outline.Points;
        if (pts.Count < 4) return false;
        var (minX, minY, maxX, maxY, w, h) = BBox(panel);
        if (w < 1e-6 || h < 1e-6) return false;
        var uniq = new HashSet<(long, long)>();
        foreach (var p in pts)
        {
            var qx = (long)Math.Round(p.X * 1000);
            var qy = (long)Math.Round(p.Y * 1000);
            uniq.Add((qx, qy));
            var onEdge =
                (Math.Abs(p.X - minX) < 1e-6 || Math.Abs(p.X - maxX) < 1e-6) &&
                (p.Y >= minY - 1e-6 && p.Y <= maxY + 1e-6)
                ||
                (Math.Abs(p.Y - minY) < 1e-6 || Math.Abs(p.Y - maxY) < 1e-6) &&
                (p.X >= minX - 1e-6 && p.X <= maxX + 1e-6);
            if (!onEdge) return false;
        }
        return uniq.Count == 4;
    }

    public static Panel MoveHole(Panel panel, string featureId, double x, double y)
    {
        var feats = panel.Features.Select(f =>
        {
            if (!IsHole(f) || f.FeatureId != featureId) return f;
            return CloneFeature(f, x: x, y: y);
        }).ToList();
        return ClonePanel(panel, feats);
    }

    public static Panel MoveGroovePoint(Panel panel, string featureId, int pointIndex, double x, double y)
    {
        var feats = panel.Features.Select(f =>
        {
            if (!IsGroove(f) || f.FeatureId != featureId || f.Path is null) return f;
            if (pointIndex < 0 || pointIndex >= f.Path.Count) return f;
            var path = f.Path.ToList();
            path[pointIndex] = new Point2(x, y);
            return CloneFeature(f, path: path);
        }).ToList();
        return ClonePanel(panel, feats);
    }

    public static Panel TranslateFeatures(Panel panel, double dx, double dy)
    {
        var feats = panel.Features.Select(f =>
        {
            if (IsHole(f)) return CloneFeature(f, x: f.X + dx, y: f.Y + dy);
            if (IsGroove(f) && f.Path is not null)
                return CloneFeature(f, path: f.Path.Select(p => new Point2(p.X + dx, p.Y + dy)).ToList());
            return f;
        }).ToList();
        return ClonePanel(panel, feats);
    }

    public static Panel RotatePanel(Panel panel, double deg)
    {
        var (minX, minY, maxX, maxY, _, _) = BBox(panel);
        var cx = (minX + maxX) / 2;
        var cy = (minY + maxY) / 2;
        var rad = deg * Math.PI / 180.0;
        var c = Math.Cos(rad);
        var s = Math.Sin(rad);
        Point2 Map(Point2 p)
        {
            var dx = p.X - cx;
            var dy = p.Y - cy;
            return new Point2(cx + dx * c - dy * s, cy + dx * s + dy * c);
        }

        var outline = new Outline
        {
            Points = panel.Outline.Points.Select(Map).ToList(),
            Closed = panel.Outline.Closed,
            Frame = panel.Outline.Frame,
        };
        var feats = panel.Features.Select(f =>
        {
            if (IsHole(f))
            {
                var q = Map(new Point2(f.X, f.Y));
                return CloneFeature(f, x: q.X, y: q.Y);
            }
            if (IsGroove(f) && f.Path is not null)
                return CloneFeature(f, path: f.Path.Select(Map).ToList());
            return f;
        }).ToList();
        return ClonePanel(panel, feats, outline);
    }

    public static Panel ResizeFromEdges(Panel panel, double minX, double minY, double maxX, double maxY)
    {
        var box = BBox(panel);
        var w0 = Math.Max(box.W, 1e-6);
        var h0 = Math.Max(box.H, 1e-6);
        var w1 = Math.Max(maxX - minX, 10);
        var h1 = Math.Max(maxY - minY, 10);
        Point2 Map(Point2 p)
        {
            var u = (p.X - box.MinX) / w0;
            var v = (p.Y - box.MinY) / h0;
            return new Point2(minX + u * w1, minY + v * h1);
        }

        var outline = new Outline
        {
            Points =
            [
                new(minX, minY),
                new(maxX, minY),
                new(maxX, maxY),
                new(minX, maxY),
            ],
            Closed = true,
            Frame = panel.Outline.Frame,
        };
        var feats = panel.Features.Select(f =>
        {
            if (IsHole(f))
            {
                var q = Map(new Point2(f.X, f.Y));
                return CloneFeature(f, x: q.X, y: q.Y);
            }
            if (IsGroove(f) && f.Path is not null)
                return CloneFeature(f, path: f.Path.Select(Map).ToList());
            return f;
        }).ToList();
        return ClonePanel(panel, feats, outline);
    }

    public static Panel AddVerticalHole(Panel panel, double x, double y, double diameterMm = 8, double? depthMm = null)
    {
        var id = NextId(panel, "H");
        var feats = panel.Features.ToList();
        feats.Add(new PanelFeature
        {
            FeatureId = id,
            Kind = "holeVertical",
            X = x,
            Y = y,
            DiameterMm = diameterMm,
            DepthMm = depthMm ?? panel.ThicknessMm,
        });
        return ClonePanel(panel, feats);
    }

    public static Panel AddVerticalGroove(Panel panel, IReadOnlyList<Point2> path, double widthMm = 6, double depthMm = 8)
    {
        if (path.Count < 2) return panel;
        var id = NextId(panel, "G");
        var feats = panel.Features.ToList();
        feats.Add(new PanelFeature
        {
            FeatureId = id,
            Kind = "grooveVertical",
            X = path[0].X,
            Y = path[0].Y,
            WidthMm = widthMm,
            DepthMm = depthMm,
            Path = path.ToList(),
        });
        return ClonePanel(panel, feats);
    }

    public static bool IsHole(PanelFeature f) =>
        f.Kind.Contains("hole", StringComparison.OrdinalIgnoreCase);

    public static bool IsGroove(PanelFeature f) =>
        f.Kind.Contains("groove", StringComparison.OrdinalIgnoreCase);

    static string NextId(Panel panel, string prefix)
    {
        var n = panel.Features.Count + 1;
        string id;
        do { id = $"{prefix}{n++}"; }
        while (panel.Features.Any(f => f.FeatureId == id));
        return id;
    }

    static Panel ClonePanel(Panel panel, IReadOnlyList<PanelFeature> feats, Outline? outline = null) =>
        new()
        {
            PanelId = panel.PanelId,
            Name = panel.Name,
            Material = panel.Material,
            ThicknessMm = panel.ThicknessMm,
            Quantity = panel.Quantity,
            AllowedRotations = panel.AllowedRotations,
            GrainDirection = panel.GrainDirection,
            Outline = outline ?? panel.Outline,
            Features = feats,
        };

    static PanelFeature CloneFeature(
        PanelFeature f,
        double? x = null,
        double? y = null,
        IReadOnlyList<Point2>? path = null) =>
        new()
        {
            FeatureId = f.FeatureId,
            Kind = f.Kind,
            X = x ?? f.X,
            Y = y ?? f.Y,
            DiameterMm = f.DiameterMm,
            DepthMm = f.DepthMm,
            WidthMm = f.WidthMm,
            Path = path ?? f.Path,
        };
}
