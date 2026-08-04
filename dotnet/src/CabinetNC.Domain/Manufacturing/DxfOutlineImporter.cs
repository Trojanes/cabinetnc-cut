namespace CabinetNC.Domain.Manufacturing;

using System.Globalization;
using CabinetNC.Domain.Geometry;
using CabinetNC.Domain.Parts;

/// <summary>
/// Minimal DXF R12 LWPOLYLINE/LINE rectangle outline importer (Day 12).
/// Arcs are not fully supported — tessellation deferred; RC needs rectangles at minimum.
/// </summary>
public static class DxfOutlineImporter
{
    public static Panel? TryImportRectanglePanel(string dxfText, string panelId, double thicknessMm = 18)
    {
        var pts = ExtractPolylinePoints(dxfText);
        if (pts.Count < 3) return null;
        return new Panel
        {
            PanelId = panelId,
            ThicknessMm = thicknessMm,
            Outline = new Outline { Points = pts, Closed = true },
            Identity = new WorkpieceIdentity { WorkpieceId = panelId, SourceFormat = "dxf" },
        };
    }

    public static List<Point2> ExtractPolylinePoints(string dxf)
    {
        // Very small parser: collect consecutive 10/20 pairs under LWPOLYLINE or POLYLINE vertices
        var lines = dxf.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var pts = new List<Point2>();
        double? x = null;
        var inPoly = false;
        for (var i = 0; i < lines.Length - 1; i++)
        {
            var code = lines[i].Trim();
            var val = lines[i + 1].Trim();
            if (code == "0" && (val is "LWPOLYLINE" or "POLYLINE"))
            {
                inPoly = true;
                continue;
            }
            if (code == "0" && val is "SEQEND" or "ENDSEC" or "LINE")
            {
                if (val == "LINE")
                {
                    // ignore LINE entities for v1 rectangle path
                }
                if (val is "SEQEND" or "ENDSEC")
                    inPoly = false;
            }
            if (!inPoly) continue;
            if (code == "10")
            {
                if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var xv))
                    x = xv;
            }
            else if (code == "20" && x is double xx)
            {
                if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var yv))
                    pts.Add(new Point2(xx, yv));
                x = null;
            }
        }

        if (pts.Count >= 3) return pts;

        // Fallback: any 10/20 pairs in ENTITIES (some exporters)
        pts.Clear();
        x = null;
        var entities = false;
        for (var i = 0; i < lines.Length - 1; i++)
        {
            var code = lines[i].Trim();
            var val = lines[i + 1].Trim();
            if (code == "2" && val == "ENTITIES") entities = true;
            if (!entities) continue;
            if (code == "10")
            {
                if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var xv))
                    x = xv;
            }
            else if (code == "20" && x is double xx)
            {
                if (double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var yv))
                    pts.Add(new Point2(xx, yv));
                x = null;
            }
        }
        return pts;
    }
}
