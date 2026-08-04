namespace CabinetNC.Domain.Manufacturing;

using System.Globalization;
using System.Text;
using CabinetNC.Domain;
using CabinetNC.Domain.Nesting;
using CabinetNC.Domain.Parts;

/// <summary>Minimal DXF R12 nest sheet export — port of src/dxf.js nestToDxf.</summary>
public static class NestDxfWriter
{
    public static string Write(
        CutPackage pkg,
        IReadOnlyList<NestPlacement> placements,
        int sheetIndex,
        bool includeFeatures = true)
    {
        var lines = new List<string>
        {
            "0", "SECTION", "2", "HEADER",
            "9", "$INSUNITS", "70", "4",
            "0", "ENDSEC",
            "0", "SECTION", "2", "ENTITIES",
        };

        var byId = pkg.Panels.ToDictionary(p => p.PanelId, p => p);
        foreach (var place in placements.Where(p => p.SheetIndex == sheetIndex))
        {
            if (!byId.TryGetValue(place.PanelId, out var panel)) continue;
            var outline = WorldOutline(panel, place);
            Polyline(lines, outline, "PANEL");
            if (!includeFeatures) continue;
            foreach (var f in panel.Features)
            {
                if (f.Kind.Contains("hole", StringComparison.OrdinalIgnoreCase) && f.DiameterMm is double d && d > 0)
                {
                    var (cx, cy) = WorldPoint(f.X, f.Y, panel, place);
                    Circle(lines, cx, cy, d / 2, "HOLE");
                }
                else if (f.Kind.Contains("groove", StringComparison.OrdinalIgnoreCase) && f.Path is { Count: >= 2 } path)
                {
                    for (var i = 0; i < path.Count - 1; i++)
                    {
                        var (x0, y0) = WorldPoint(path[i].X, path[i].Y, panel, place);
                        var (x1, y1) = WorldPoint(path[i + 1].X, path[i + 1].Y, panel, place);
                        Line(lines, x0, y0, x1, y1, "GROOVE");
                    }
                }
            }
        }

        lines.Add("0");
        lines.Add("ENDSEC");
        lines.Add("0");
        lines.Add("EOF");
        return string.Join("\n", lines) + "\n";
    }

    static List<(double X, double Y)> WorldOutline(Panel panel, NestPlacement place)
    {
        var pts = new List<(double X, double Y)>();
        var bounds = NestTransform.BoundsOf(panel);
        foreach (var p in panel.Outline.Points)
        {
            pts.Add(NestTransform.ToSheet(
                p.X, p.Y, bounds,
                place.OffsetX, place.OffsetY, place.RotationDeg));
        }
        return pts;
    }

    static (double X, double Y) WorldPoint(
        double x, double y, Panel panel, NestPlacement place)
    {
        return NestTransform.ToSheet(
            x, y, NestTransform.BoundsOf(panel),
            place.OffsetX, place.OffsetY, place.RotationDeg);
    }

    static void Polyline(List<string> lines, IReadOnlyList<(double X, double Y)> pts, string layer)
    {
        if (pts.Count < 2) return;
        lines.Add("0"); lines.Add("LWPOLYLINE");
        lines.Add("8"); lines.Add(layer);
        lines.Add("90"); lines.Add(pts.Count.ToString(CultureInfo.InvariantCulture));
        lines.Add("70"); lines.Add("1");
        foreach (var p in pts)
        {
            lines.Add("10"); lines.Add(Fmt(p.X));
            lines.Add("20"); lines.Add(Fmt(p.Y));
        }
    }

    static void Circle(List<string> lines, double cx, double cy, double r, string layer)
    {
        if (r <= 0) return;
        lines.Add("0"); lines.Add("CIRCLE");
        lines.Add("8"); lines.Add(layer);
        lines.Add("10"); lines.Add(Fmt(cx));
        lines.Add("20"); lines.Add(Fmt(cy));
        lines.Add("30"); lines.Add("0.0");
        lines.Add("40"); lines.Add(Fmt(r));
    }

    static void Line(List<string> lines, double x0, double y0, double x1, double y1, string layer)
    {
        lines.Add("0"); lines.Add("LINE");
        lines.Add("8"); lines.Add(layer);
        lines.Add("10"); lines.Add(Fmt(x0));
        lines.Add("20"); lines.Add(Fmt(y0));
        lines.Add("30"); lines.Add("0.0");
        lines.Add("11"); lines.Add(Fmt(x1));
        lines.Add("21"); lines.Add(Fmt(y1));
        lines.Add("31"); lines.Add("0.0");
    }

    static string Fmt(double n) =>
        (Math.Round(n * 1000) / 1000).ToString("0.000", CultureInfo.InvariantCulture);
}
