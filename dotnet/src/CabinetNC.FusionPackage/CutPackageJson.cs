namespace CabinetNC.FusionPackage;

using System.Text.Json;
using System.Text.Json.Serialization;
using CabinetNC.Domain;
using CabinetNC.Domain.Parts;

/// <summary>Serialize runtime CutPackage to flat JSON for project.db / legacy round-trip.</summary>
public static class CutPackageJson
{
    static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public static string Serialize(CutPackage pkg)
    {
        var dto = new
        {
            schema = pkg.SchemaName == CutPackage.WoodJobFormat ? CutPackage.Schema : pkg.SchemaName,
            schemaVersion = CutPackage.SchemaVersion,
            jobId = pkg.JobId,
            units = pkg.Units,
            sheets = pkg.Sheets.Select(s => new
            {
                sheetId = s.SheetId,
                material = s.Material,
                thicknessMm = s.ThicknessMm,
                widthMm = s.WidthMm,
                lengthMm = s.LengthMm,
                heightMm = s.LengthMm,
                marginMm = s.MarginMm,
                kerfMm = s.KerfMm,
                partClearanceMm = s.PartClearanceMm,
            }),
            panels = pkg.Panels.Select(p => new
            {
                panelId = p.PanelId,
                name = p.Name,
                material = p.Material,
                thicknessMm = p.ThicknessMm,
                quantity = p.Quantity,
                grainDirection = p.GrainDirection,
                allowedRotations = p.AllowedRotations,
                outline = new
                {
                    points = p.Outline.Points.Select(pt => new[] { pt.X, pt.Y }),
                    closed = p.Outline.Closed,
                    frame = p.Outline.Frame,
                },
                features = p.Features.Select(FeatureDto),
            }),
        };
        return JsonSerializer.Serialize(dto, Opts);
    }

    static object FeatureDto(PanelFeature f) => new
    {
        featureId = f.FeatureId,
        kind = f.Kind,
        x = f.X,
        y = f.Y,
        diameterMm = f.DiameterMm,
        depthMm = f.DepthMm,
        widthMm = f.WidthMm,
        path = f.Path?.Select(pt => new[] { pt.X, pt.Y }),
    };
}
